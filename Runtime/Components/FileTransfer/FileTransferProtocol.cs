using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Modules.Utilities
{
    public enum FileTransferOpcode : byte
    {
        Upload = 1,
        Download = 2,
    }

    public enum FileTransferStatus : byte
    {
        Ok = 0,
        Rejected = 1,
        NotFound = 2,
        ServerError = 3,
        TooLarge = 4,
        Busy = 5,
        BadRequest = 6,
        HashMismatch = 7,

        // Produced locally, never seen on the wire.
        Cancelled = 8,
        ConnectionFailed = 9,
    }

    public readonly struct FileTransferProgress
    {
        public readonly string Name;
        public readonly long Transferred;
        public readonly long Total;
        public readonly float BytesPerSecond;

        public FileTransferProgress(string name, long transferred, long total, float bytesPerSecond)
        {
            Name = name;
            Transferred = transferred;
            Total = total;
            BytesPerSecond = bytesPerSecond;
        }

        public float Percent => Total > 0 ? (float)Transferred / Total : 0f;
    }

    public sealed class FileTransferResult
    {
        public bool IsSuccess;
        public FileTransferStatus Status;
        public string Name;
        public string LocalPath;
        public long Bytes;
        public double ElapsedSeconds;
        public string Error;

        public static FileTransferResult Ok(string name, string localPath, long bytes, double seconds)
        {
            return new FileTransferResult
            {
                IsSuccess = true,
                Status = FileTransferStatus.Ok,
                Name = name,
                LocalPath = localPath,
                Bytes = bytes,
                ElapsedSeconds = seconds,
            };
        }

        public static FileTransferResult Fail(FileTransferStatus status, string name, string error)
        {
            return new FileTransferResult
            {
                IsSuccess = false,
                Status = status,
                Name = name,
                Error = error,
            };
        }

        public override string ToString()
        {
            return IsSuccess
                ? $"OK {Name} ({Bytes}B in {ElapsedSeconds:0.00}s)"
                : $"{Status} {Name}: {Error}";
        }
    }

    /// <summary>Bridge notification payload sent over DataTransceiver.</summary>
    public sealed class FileOffer
    {
        public string Name;
        public string Folder;
        public long Size;
    }

    /// <summary>
    /// Frame metadata. One class serves every frame type; unused fields stay null or zero
    /// and are dropped by NullValueHandling.Ignore.
    /// </summary>
    public sealed class FileTransferMeta
    {
        public string name;
        public string folder;
        public long size;
        public string secret;
        public string error;
    }

    public static partial class FileTransferProtocol
    {
        /// <summary>'F','T','X','1' read back as a little-endian uint32.</summary>
        public const uint MAGIC = 0x31585446;

        public const byte VERSION = 1;
        public const int HEADER_SIZE = 12;
        public const int MAX_META_BYTES = 65536;
        public const int HASH_SIZE = 32;

        /// <summary>Header flag: a 32-byte SHA-256 trailer follows the payload.</summary>
        public const byte FLAG_HASH = 0x01;

        /// <summary>
        /// Path separators rejected in names on every platform. ':' is included because
        /// Windows reads it as a drive or alternate-stream separator while Unix does not.
        /// </summary>
        private static readonly char[] SEPARATOR_CHARS = { '/', '\\', ':' };

        public static byte[] BuildHeader(byte code, byte flags, int metaLen)
        {
            if (metaLen < 0) throw new ArgumentOutOfRangeException(nameof(metaLen));

            var header = new byte[HEADER_SIZE];
            BitConverter.GetBytes(MAGIC).CopyTo(header, 0);
            header[4] = VERSION;
            header[5] = code;
            header[6] = flags;
            header[7] = 0;
            BitConverter.GetBytes((uint)metaLen).CopyTo(header, 8);
            return header;
        }

        public static bool TryParseHeader(byte[] header, out byte code, out byte flags, out int metaLen)
        {
            code = 0;
            flags = 0;
            metaLen = 0;

            if (header == null || header.Length < HEADER_SIZE) return false;
            if (BitConverter.ToUInt32(header, 0) != MAGIC) return false;
            if (header[4] != VERSION) return false;

            var len = BitConverter.ToUInt32(header, 8);
            if (len > MAX_META_BYTES) return false;

            code = header[5];
            flags = header[6];
            metaLen = (int)len;
            return true;
        }

        /// <summary>
        /// Resolves a network-supplied folder/name pair to an absolute path guaranteed to sit
        /// under <paramref name="root"/>. Both inputs are treated as hostile.
        /// </summary>
        public static bool TryResolveSafePath(string root, string folder, string name, out string fullPath, out string error)
        {
            fullPath = null;
            error = null;

            if (string.IsNullOrEmpty(root))
            {
                error = "server root folder is not configured";
                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                error = "file name is empty";
                return false;
            }

            // Checked explicitly rather than through Path.GetFileName or GetInvalidFileNameChars,
            // because both are platform-dependent: on Unix a backslash is a legal filename
            // character, so "sub\evil.txt" would survive there and be read as a path on Windows.
            // A wire protocol has to validate the same way on every OS.
            if (name.IndexOfAny(SEPARATOR_CHARS) >= 0)
            {
                error = "file name must not contain a path";
                return false;
            }

            if (name == "." || name == "..")
            {
                error = "file name must not be a directory reference";
                return false;
            }

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                error = "file name contains invalid characters";
                return false;
            }

            folder = folder ?? string.Empty;

            if (Path.IsPathRooted(folder))
            {
                error = "folder must be relative";
                return false;
            }

            var segments = folder.Split('/', '\\');
            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i] == "..")
                {
                    error = "folder must not traverse upward";
                    return false;
                }
            }

            string rootFull;
            string combined;
            try
            {
                rootFull = Path.GetFullPath(root);
                combined = Path.GetFullPath(Path.Combine(rootFull, folder, name));
            }
            catch (Exception ex)
            {
                error = "could not resolve path: " + ex.Message;
                return false;
            }

            var prefix = rootFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? rootFull
                : rootFull + Path.DirectorySeparatorChar;

            // Authoritative check. The tests above fail fast with clearer messages, but this is
            // the one that actually guarantees containment.
            if (!combined.StartsWith(prefix, StringComparison.Ordinal))
            {
                error = "path escapes the server root folder";
                return false;
            }

            fullPath = combined;
            return true;
        }
    }

    public readonly struct FileTransferFrame
    {
        public readonly byte Code;
        public readonly byte Flags;
        public readonly FileTransferMeta Meta;

        public FileTransferFrame(byte code, byte flags, FileTransferMeta meta)
        {
            Code = code;
            Flags = flags;
            Meta = meta;
        }

        public FileTransferStatus Status => (FileTransferStatus)Code;
        public FileTransferOpcode Opcode => (FileTransferOpcode)Code;
    }

    public static partial class FileTransferProtocol
    {
        private const int PROGRESS_INTERVAL_MS = 100;

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
        };

        public static async Task WriteFrameAsync(Stream stream, byte code, byte flags, FileTransferMeta meta, CancellationToken ct)
        {
            var metaBytes = meta == null
                ? Array.Empty<byte>()
                : Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(meta, _jsonSettings));

            if (metaBytes.Length > MAX_META_BYTES)
                throw new InvalidDataException($"frame metadata is {metaBytes.Length}B, over the {MAX_META_BYTES}B limit");

            var header = BuildHeader(code, flags, metaBytes.Length);

            await stream.WriteAsync(header, 0, header.Length, ct).ConfigureAwait(false);
            if (metaBytes.Length > 0)
                await stream.WriteAsync(metaBytes, 0, metaBytes.Length, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }

        public static async Task<FileTransferFrame> ReadFrameAsync(Stream stream, CancellationToken ct)
        {
            var header = new byte[HEADER_SIZE];
            await ReadExactAsync(stream, header, 0, HEADER_SIZE, ct).ConfigureAwait(false);

            if (!TryParseHeader(header, out var code, out var flags, out var metaLen))
                throw new InvalidDataException("bad frame header (magic, version, or metadata length)");

            FileTransferMeta meta = null;
            if (metaLen > 0)
            {
                var metaBytes = new byte[metaLen];
                await ReadExactAsync(stream, metaBytes, 0, metaLen, ct).ConfigureAwait(false);
                meta = JsonConvert.DeserializeObject<FileTransferMeta>(Encoding.UTF8.GetString(metaBytes));
            }

            return new FileTransferFrame(code, flags, meta ?? new FileTransferMeta());
        }

        public static async Task ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken ct)
        {
            var read = 0;
            while (read < count)
            {
                var n = await stream.ReadAsync(buffer, offset + read, count - read, ct).ConfigureAwait(false);
                if (n <= 0)
                    throw new EndOfStreamException($"connection closed after {read} of {count} expected bytes");
                read += n;
            }
        }

        /// <summary>
        /// Moves exactly <paramref name="total"/> bytes from source to destination through one
        /// reusable buffer, hashing as it goes so neither side reads the payload twice.
        /// </summary>
        public static async Task<byte[]> PumpAsync(
            Stream source,
            Stream destination,
            long total,
            int bufferSize,
            bool computeHash,
            string displayName,
            IProgress<FileTransferProgress> progress,
            CancellationToken ct)
        {
            if (bufferSize <= 0) bufferSize = 81920;

            var buffer = new byte[bufferSize];
            var hasher = computeHash ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;

            try
            {
                var moved = 0L;
                var stopwatch = Stopwatch.StartNew();
                var lastReportMs = (long)-PROGRESS_INTERVAL_MS;

                while (moved < total)
                {
                    ct.ThrowIfCancellationRequested();

                    var want = (int)Math.Min(buffer.Length, total - moved);
                    var n = await source.ReadAsync(buffer, 0, want, ct).ConfigureAwait(false);
                    if (n <= 0)
                        throw new EndOfStreamException($"stream ended after {moved} of {total} bytes");

                    await destination.WriteAsync(buffer, 0, n, ct).ConfigureAwait(false);
                    if (hasher != null) hasher.AppendData(buffer, 0, n);
                    moved += n;

                    if (progress == null) continue;

                    var elapsedMs = stopwatch.ElapsedMilliseconds;
                    if (elapsedMs - lastReportMs < PROGRESS_INTERVAL_MS && moved < total) continue;

                    lastReportMs = elapsedMs;
                    var seconds = elapsedMs / 1000f;
                    var rate = seconds > 0f ? moved / seconds : 0f;
                    progress.Report(new FileTransferProgress(displayName, moved, total, rate));
                }

                await destination.FlushAsync(ct).ConfigureAwait(false);
                return hasher != null ? hasher.GetHashAndReset() : null;
            }
            finally
            {
                if (hasher != null) hasher.Dispose();
            }
        }

        public static bool HashEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        public static string ToHex(byte[] bytes)
        {
            if (bytes == null) return string.Empty;

            var sb = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
