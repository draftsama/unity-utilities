using System;
using System.IO;

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
}
