using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.Utilities
{
    public sealed class FileTransferServerOptions
    {
        public string RootFolder;
        public int Port = 55600;
        public string BindAddress = string.Empty;
        public long MaxFileSizeBytes = 512L * 1024 * 1024;
        public int MaxConcurrentTransfers = 4;
        public bool AllowUpload = true;
        public bool AllowDownload = true;
        public int BufferSize = 80 * 1024;
        public bool VerifyHash = true;
        public int IdleTimeoutMs = 30000;
        public string SharedSecret = string.Empty;
    }

    /// <summary>
    /// Accepts one transfer per connection. Plain C#: no UnityEngine reference, so it can be
    /// driven from editor tooling or tests without a GameObject.
    /// </summary>
    public sealed class FileTransferServer : IDisposable
    {
        private readonly FileTransferServerOptions _options;
        private readonly SemaphoreSlim _slots;

        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private int _activeTransfers;

        public FileTransferServer(FileTransferServerOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _options = options;
            _slots = new SemaphoreSlim(Math.Max(1, options.MaxConcurrentTransfers));
        }

        public bool IsRunning { get; private set; }
        public int Port { get; private set; }
        public int ActiveTransfers => Volatile.Read(ref _activeTransfers);

        public event Action<FileTransferResult> FileReceived;
        public event Action<FileTransferResult> FileServed;
        public event Action<FileTransferProgress> Progress;
        public event Action<string> Log;

        public void Start()
        {
            if (IsRunning) return;

            if (string.IsNullOrEmpty(_options.RootFolder))
                throw new InvalidOperationException("FileTransferServerOptions.RootFolder must be set");

            Directory.CreateDirectory(_options.RootFolder);

            var address = IPAddress.Any;
            if (!string.IsNullOrEmpty(_options.BindAddress) && !IPAddress.TryParse(_options.BindAddress, out address))
                address = IPAddress.Any;

            _listener = new TcpListener(address, _options.Port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

            _cts = new CancellationTokenSource();
            IsRunning = true;

            // Fire-and-forget by design: the loop owns its own lifetime and swallows its
            // terminal exceptions, so nothing here needs to observe the task.
            _ = AcceptLoopAsync(_cts.Token);

            Write($"listening on {address}:{Port}, root={_options.RootFolder}");
        }

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;

            try { if (_cts != null) _cts.Cancel(); } catch { }
            try { if (_listener != null) _listener.Stop(); } catch { }

            try { if (_cts != null) _cts.Dispose(); } catch { }
            _cts = null;
            _listener = null;

            Write("stopped");
        }

        public void Dispose()
        {
            Stop();
            _slots.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { return; }
                catch (InvalidOperationException) { return; }
                catch (NullReferenceException) { return; }

                // Each connection runs independently so a slow transfer never blocks accept.
                // HandleConnectionAsync catches everything, so the task cannot fault.
                _ = HandleConnectionAsync(client, ct);
            }
        }

        private async Task HandleConnectionAsync(TcpClient client, CancellationToken ct)
        {
            var holdsSlot = false;
            NetworkStream stream = null;

            try
            {
                client.ReceiveTimeout = _options.IdleTimeoutMs;
                client.SendTimeout = _options.IdleTimeoutMs;
                stream = client.GetStream();

                FileTransferFrame request;
                try
                {
                    request = await FileTransferProtocol.ReadFrameAsync(stream, ct).ConfigureAwait(false);
                }
                catch (InvalidDataException ex)
                {
                    await RespondAsync(stream, FileTransferStatus.BadRequest, ex.Message, ct).ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrEmpty(_options.SharedSecret) &&
                    !string.Equals(_options.SharedSecret, request.Meta.secret, StringComparison.Ordinal))
                {
                    await RespondAsync(stream, FileTransferStatus.BadRequest, "shared secret mismatch", ct).ConfigureAwait(false);
                    return;
                }

                holdsSlot = _slots.Wait(0);
                if (!holdsSlot)
                {
                    await RespondAsync(stream, FileTransferStatus.Busy, "server is at its concurrent transfer limit", ct).ConfigureAwait(false);
                    return;
                }

                Interlocked.Increment(ref _activeTransfers);

                switch (request.Opcode)
                {
                    case FileTransferOpcode.Upload:
                        await HandleUploadAsync(stream, request, ct).ConfigureAwait(false);
                        break;
                    case FileTransferOpcode.Download:
                        await HandleDownloadAsync(stream, request, ct).ConfigureAwait(false);
                        break;
                    default:
                        await RespondAsync(stream, FileTransferStatus.BadRequest, $"unknown opcode {request.Code}", ct).ConfigureAwait(false);
                        break;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Write($"connection error: {ex.Message}");
            }
            finally
            {
                if (holdsSlot)
                {
                    Interlocked.Decrement(ref _activeTransfers);
                    _slots.Release();
                }

                try { if (stream != null) stream.Dispose(); } catch { }
                try { client.Dispose(); } catch { }
            }
        }

        private async Task HandleUploadAsync(NetworkStream stream, FileTransferFrame request, CancellationToken ct)
        {
            var name = request.Meta.name;

            if (!_options.AllowUpload)
            {
                await RespondAsync(stream, FileTransferStatus.Rejected, "uploads are disabled on this server", ct).ConfigureAwait(false);
                return;
            }

            string target;
            string pathError;
            if (!FileTransferProtocol.TryResolveSafePath(_options.RootFolder, request.Meta.folder, name, out target, out pathError))
            {
                await RespondAsync(stream, FileTransferStatus.BadRequest, pathError, ct).ConfigureAwait(false);
                return;
            }

            var size = request.Meta.size;
            if (size < 0)
            {
                await RespondAsync(stream, FileTransferStatus.BadRequest, "negative size", ct).ConfigureAwait(false);
                return;
            }

            if (size > _options.MaxFileSizeBytes)
            {
                await RespondAsync(stream, FileTransferStatus.TooLarge,
                    $"{size}B exceeds the {_options.MaxFileSizeBytes}B limit", ct).ConfigureAwait(false);
                return;
            }

            await RespondAsync(stream, FileTransferStatus.Ok, null, ct).ConfigureAwait(false);

            var partPath = target + ".part";
            var wantHash = (request.Flags & FileTransferProtocol.FLAG_HASH) != 0;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target));

                byte[] digest;
                using (var file = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, _options.BufferSize, true))
                {
                    digest = await FileTransferProtocol.PumpAsync(
                        stream, file, size, _options.BufferSize, wantHash, name,
                        new Progress<FileTransferProgress>(p => RaiseProgress(p)), ct).ConfigureAwait(false);
                }

                if (wantHash)
                {
                    var trailer = new byte[FileTransferProtocol.HASH_SIZE];
                    await FileTransferProtocol.ReadExactAsync(stream, trailer, 0, trailer.Length, ct).ConfigureAwait(false);

                    if (!FileTransferProtocol.HashEquals(digest, trailer))
                    {
                        SafeDelete(partPath);
                        await RespondAsync(stream, FileTransferStatus.HashMismatch,
                            $"expected {FileTransferProtocol.ToHex(trailer)}, computed {FileTransferProtocol.ToHex(digest)}", ct).ConfigureAwait(false);
                        return;
                    }
                }

                if (File.Exists(target)) File.Delete(target);
                File.Move(partPath, target);

                await RespondAsync(stream, FileTransferStatus.Ok, null, ct).ConfigureAwait(false);

                var result = FileTransferResult.Ok(name, target, size, stopwatch.Elapsed.TotalSeconds);
                Write($"received {result}");
                RaiseReceived(result);
            }
            catch (Exception ex)
            {
                SafeDelete(partPath);
                Write($"upload of '{name}' failed: {ex.Message}");
                await TryRespondAsync(stream, FileTransferStatus.ServerError, ex.Message).ConfigureAwait(false);
                RaiseReceived(FileTransferResult.Fail(FileTransferStatus.ServerError, name, ex.Message));
            }
        }

        private async Task HandleDownloadAsync(NetworkStream stream, FileTransferFrame request, CancellationToken ct)
        {
            var name = request.Meta.name;

            if (!_options.AllowDownload)
            {
                await RespondAsync(stream, FileTransferStatus.Rejected, "downloads are disabled on this server", ct).ConfigureAwait(false);
                return;
            }

            string source;
            string pathError;
            if (!FileTransferProtocol.TryResolveSafePath(_options.RootFolder, request.Meta.folder, name, out source, out pathError))
            {
                await RespondAsync(stream, FileTransferStatus.BadRequest, pathError, ct).ConfigureAwait(false);
                return;
            }

            if (!File.Exists(source))
            {
                await RespondAsync(stream, FileTransferStatus.NotFound, $"'{name}' is not on the server", ct).ConfigureAwait(false);
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                using (var file = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, _options.BufferSize, true))
                {
                    var size = file.Length;
                    var flags = _options.VerifyHash ? FileTransferProtocol.FLAG_HASH : (byte)0;

                    await FileTransferProtocol.WriteFrameAsync(stream, (byte)FileTransferStatus.Ok, flags,
                        new FileTransferMeta { size = size }, ct).ConfigureAwait(false);

                    var digest = await FileTransferProtocol.PumpAsync(
                        file, stream, size, _options.BufferSize, _options.VerifyHash, name,
                        new Progress<FileTransferProgress>(p => RaiseProgress(p)), ct).ConfigureAwait(false);

                    if (digest != null)
                    {
                        await stream.WriteAsync(digest, 0, digest.Length, ct).ConfigureAwait(false);
                        await stream.FlushAsync(ct).ConfigureAwait(false);
                    }

                    var result = FileTransferResult.Ok(name, source, size, stopwatch.Elapsed.TotalSeconds);
                    Write($"served {result}");
                    RaiseServed(result);
                }
            }
            catch (Exception ex)
            {
                Write($"download of '{name}' failed: {ex.Message}");
                RaiseServed(FileTransferResult.Fail(FileTransferStatus.ServerError, name, ex.Message));
            }
        }

        private static Task RespondAsync(Stream stream, FileTransferStatus status, string error, CancellationToken ct)
        {
            var meta = string.IsNullOrEmpty(error) ? null : new FileTransferMeta { error = error };
            return FileTransferProtocol.WriteFrameAsync(stream, (byte)status, 0, meta, ct);
        }

        /// <summary>Best-effort error reply on a connection that may already be gone.</summary>
        private static async Task TryRespondAsync(Stream stream, FileTransferStatus status, string error)
        {
            try { await RespondAsync(stream, status, error, CancellationToken.None).ConfigureAwait(false); }
            catch { }
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private void RaiseProgress(FileTransferProgress progress)
        {
            var handler = Progress;
            if (handler != null) handler(progress);
        }

        private void RaiseReceived(FileTransferResult result)
        {
            var handler = FileReceived;
            if (handler != null) handler(result);
        }

        private void RaiseServed(FileTransferResult result)
        {
            var handler = FileServed;
            if (handler != null) handler(result);
        }

        private void Write(string message)
        {
            var handler = Log;
            if (handler != null) handler(message);
        }
    }
}
