using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.Utilities
{
    public sealed class FileTransferClientOptions
    {
        public string Host = "127.0.0.1";
        public int Port = 55600;
        public string DownloadFolder;
        public int ConnectTimeoutMs = 5000;
        public int BufferSize = 80 * 1024;
        public bool VerifyHash = true;
        public int IdleTimeoutMs = 30000;
        public string SharedSecret = string.Empty;
    }

    /// <summary>
    /// Dials the server, performs one transfer, closes. Plain C#: no UnityEngine reference.
    /// Never throws for network or protocol conditions — those come back as a
    /// <see cref="FileTransferResult"/> with a non-Ok status.
    /// </summary>
    public sealed class FileTransferClient
    {
        private readonly FileTransferClientOptions _options;

        public FileTransferClient(FileTransferClientOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _options = options;
        }

        public event Action<string> Log;

        public async Task<FileTransferResult> UploadAsync(
            string localPath,
            string remoteFolder = "",
            IProgress<FileTransferProgress> progress = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(localPath)) throw new ArgumentException("localPath is required", nameof(localPath));

            var name = Path.GetFileName(localPath);

            if (!File.Exists(localPath))
                return FileTransferResult.Fail(FileTransferStatus.NotFound, name, $"local file '{localPath}' does not exist");

            var size = new FileInfo(localPath).Length;
            var stopwatch = Stopwatch.StartNew();
            TcpClient client = null;

            try
            {
                client = await ConnectAsync(ct).ConfigureAwait(false);
                using (var stream = client.GetStream())
                {
                    var flags = _options.VerifyHash ? FileTransferProtocol.FLAG_HASH : (byte)0;

                    await FileTransferProtocol.WriteFrameAsync(stream, (byte)FileTransferOpcode.Upload, flags,
                        new FileTransferMeta
                        {
                            name = name,
                            folder = remoteFolder ?? string.Empty,
                            size = size,
                            secret = NullIfEmpty(_options.SharedSecret),
                        }, ct).ConfigureAwait(false);

                    var accept = await FileTransferProtocol.ReadFrameAsync(stream, ct).ConfigureAwait(false);
                    if (accept.Status != FileTransferStatus.Ok)
                        return FileTransferResult.Fail(accept.Status, name, accept.Meta.error ?? accept.Status.ToString());

                    byte[] digest;
                    using (var file = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, _options.BufferSize, true))
                    {
                        digest = await FileTransferProtocol.PumpAsync(
                            file, stream, size, _options.BufferSize, _options.VerifyHash, name, progress, ct).ConfigureAwait(false);
                    }

                    if (digest != null)
                    {
                        await stream.WriteAsync(digest, 0, digest.Length, ct).ConfigureAwait(false);
                        await stream.FlushAsync(ct).ConfigureAwait(false);
                    }

                    var final = await FileTransferProtocol.ReadFrameAsync(stream, ct).ConfigureAwait(false);
                    if (final.Status != FileTransferStatus.Ok)
                        return FileTransferResult.Fail(final.Status, name, final.Meta.error ?? final.Status.ToString());

                    var result = FileTransferResult.Ok(name, localPath, size, stopwatch.Elapsed.TotalSeconds);
                    Write($"uploaded {result}");
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                return FileTransferResult.Fail(FileTransferStatus.Cancelled, name, "cancelled");
            }
            catch (SocketException ex)
            {
                return FileTransferResult.Fail(FileTransferStatus.ConnectionFailed, name, ex.Message);
            }
            catch (TimeoutException ex)
            {
                return FileTransferResult.Fail(FileTransferStatus.ConnectionFailed, name, ex.Message);
            }
            catch (Exception ex)
            {
                return FileTransferResult.Fail(FileTransferStatus.ServerError, name, ex.Message);
            }
            finally
            {
                try { if (client != null) client.Dispose(); } catch { }
            }
        }

        public async Task<FileTransferResult> DownloadAsync(
            string remoteName,
            string remoteFolder = "",
            string localSavePath = null,
            IProgress<FileTransferProgress> progress = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(remoteName)) throw new ArgumentException("remoteName is required", nameof(remoteName));

            var name = remoteName;
            var target = string.IsNullOrEmpty(localSavePath)
                ? Path.Combine(_options.DownloadFolder ?? string.Empty, Path.GetFileName(remoteName))
                : localSavePath;

            var partPath = target + ".part";
            var stopwatch = Stopwatch.StartNew();
            TcpClient client = null;

            try
            {
                client = await ConnectAsync(ct).ConfigureAwait(false);
                using (var stream = client.GetStream())
                {
                    await FileTransferProtocol.WriteFrameAsync(stream, (byte)FileTransferOpcode.Download, 0,
                        new FileTransferMeta
                        {
                            name = remoteName,
                            folder = remoteFolder ?? string.Empty,
                            secret = NullIfEmpty(_options.SharedSecret),
                        }, ct).ConfigureAwait(false);

                    var accept = await FileTransferProtocol.ReadFrameAsync(stream, ct).ConfigureAwait(false);
                    if (accept.Status != FileTransferStatus.Ok)
                        return FileTransferResult.Fail(accept.Status, name, accept.Meta.error ?? accept.Status.ToString());

                    var size = accept.Meta.size;
                    var wantHash = (accept.Flags & FileTransferProtocol.FLAG_HASH) != 0;

                    var directory = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                    byte[] digest;
                    using (var file = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, _options.BufferSize, true))
                    {
                        digest = await FileTransferProtocol.PumpAsync(
                            stream, file, size, _options.BufferSize, wantHash, name, progress, ct).ConfigureAwait(false);
                    }

                    if (wantHash)
                    {
                        var trailer = new byte[FileTransferProtocol.HASH_SIZE];
                        await FileTransferProtocol.ReadExactAsync(stream, trailer, 0, trailer.Length, ct).ConfigureAwait(false);

                        if (!FileTransferProtocol.HashEquals(digest, trailer))
                        {
                            SafeDelete(partPath);
                            return FileTransferResult.Fail(FileTransferStatus.HashMismatch, name,
                                $"expected {FileTransferProtocol.ToHex(trailer)}, computed {FileTransferProtocol.ToHex(digest)}");
                        }
                    }

                    if (File.Exists(target)) File.Delete(target);
                    File.Move(partPath, target);

                    var result = FileTransferResult.Ok(name, target, size, stopwatch.Elapsed.TotalSeconds);
                    Write($"downloaded {result}");
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                SafeDelete(partPath);
                return FileTransferResult.Fail(FileTransferStatus.Cancelled, name, "cancelled");
            }
            catch (SocketException ex)
            {
                SafeDelete(partPath);
                return FileTransferResult.Fail(FileTransferStatus.ConnectionFailed, name, ex.Message);
            }
            catch (TimeoutException ex)
            {
                SafeDelete(partPath);
                return FileTransferResult.Fail(FileTransferStatus.ConnectionFailed, name, ex.Message);
            }
            catch (Exception ex)
            {
                SafeDelete(partPath);
                return FileTransferResult.Fail(FileTransferStatus.ServerError, name, ex.Message);
            }
            finally
            {
                try { if (client != null) client.Dispose(); } catch { }
            }
        }

        private async Task<TcpClient> ConnectAsync(CancellationToken ct)
        {
            var client = new TcpClient();
            client.ReceiveTimeout = _options.IdleTimeoutMs;
            client.SendTimeout = _options.IdleTimeoutMs;

            try
            {
                // ConnectAsync has no timeout overload on netstandard2.1, so race it.
                var connect = client.ConnectAsync(_options.Host, _options.Port);
                var timeout = Task.Delay(_options.ConnectTimeoutMs, ct);

                if (await Task.WhenAny(connect, timeout).ConfigureAwait(false) != connect)
                {
                    ct.ThrowIfCancellationRequested();
                    throw new TimeoutException($"connect to {_options.Host}:{_options.Port} timed out after {_options.ConnectTimeoutMs}ms");
                }

                await connect.ConfigureAwait(false);
                return client;
            }
            catch
            {
                try { client.Dispose(); } catch { }
                throw;
            }
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private void Write(string message)
        {
            var handler = Log;
            if (handler != null) handler(message);
        }
    }
}
