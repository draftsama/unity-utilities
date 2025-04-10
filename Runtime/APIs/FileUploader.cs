using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.Utilities
{
    public class FileUploader
    {
        
        public static async Task<string> UploadFile(string _url, string _key, string _filePath,
            CancellationToken _token,Dictionary<string,string> _fromData, IProgress<UploadProgress> _progress = null,
            string _method = "POST")
        {
            using (var fs = File.OpenRead(_filePath))
            {
                using (var streamContent = new ProgressableStreamContent(fs, _progress))
                {
                    var fileInfo = new FileInfo(_filePath);
                    using (var client = new HttpClient())
                    {
                        using (var content = new MultipartFormDataContent())
                        {
                            content.Add(streamContent, _key, fileInfo.Name);
                            foreach (var data in _fromData)
                            {
                                var text = new StringContent(data.Value, Encoding.UTF8, "application/json");
                                content.Add(text,data.Key);
                            }
                            
                            using (
                                var message = _method.Equals("POST")
                                    ? await client.PutAsync(_url, content, _token)
                                    : await client.PostAsync(_url, content, _token))
                            {
                                var input = await message.Content.ReadAsStringAsync();

                                streamContent?.Dispose();
                                fs?.Dispose();

                                return input;
                            }
                        }
                    }
                }
            }
        }
    }

    public class UploadProgress
    {
        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="UploadProgress"/> class.
        /// </summary>
        /// <param name="bytesTransfered">
        /// The bytes transfered.
        /// </param>
        /// <param name="totalBytes">
        /// The total bytes.
        /// </param>
        public UploadProgress(long bytesTransfered, long? totalBytes)
        {
            this.BytesTransfered = bytesTransfered;
            this.TotalBytes = totalBytes;
            if (totalBytes.HasValue)
            {
                this.ProgressPercentage = (int)((float)bytesTransfered / totalBytes.Value * 100);
            }
        }

        #endregion

        #region Public Properties

        /// <summary>
        ///     Gets the bytes transfered.
        /// </summary>
        public long BytesTransfered { get; private set; }

        /// <summary>
        ///     Gets the progress percentage.
        /// </summary>
        public int ProgressPercentage { get; private set; }

        /// <summary>
        ///     Gets the total bytes.
        /// </summary>
        public long? TotalBytes { get; private set; }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        ///     Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        ///     A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("{0}% ({1} / {2})", this.ProgressPercentage, this.BytesTransfered,
                this.TotalBytes);
        }

        #endregion
    }
    
     public class ProgressableStreamContent : StreamContent
    {
        #region Constants

        /// <summary>
        ///     The default buffer size.
        /// </summary>
        private const int DefaultBufferSize = 4096;

        #endregion

        #region Fields

        /// <summary>
        ///     The buffer size.
        /// </summary>
        private readonly int bufferSize;

        /// <summary>
        ///     The progress.
        /// </summary>
        private readonly IProgress<UploadProgress> progress;

        /// <summary>
        ///     The stream to write.
        /// </summary>
        private readonly Stream streamToWrite;

        /// <summary>
        ///     The content consumed.
        /// </summary>
        private bool contentConsumed;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressableStreamContent"/> class.
        /// </summary>
        /// <param name="streamToWrite">
        /// The stream to write.
        /// </param>
        /// <param name="downloader">
        /// The downloader.
        /// </param>
        public ProgressableStreamContent(Stream streamToWrite, IProgress<UploadProgress> downloader)
            : this(streamToWrite, DefaultBufferSize, downloader)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressableStreamContent"/> class.
        /// </summary>
        /// <param name="streamToWrite">
        /// The stream to write.
        /// </param>
        /// <param name="bufferSize">
        /// Size of the buffer.
        /// </param>
        /// <param name="progress">
        /// The progress.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Stream to write must not be null.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Buffer size cannot be less than 0.
        /// </exception>
        public ProgressableStreamContent(Stream streamToWrite, int bufferSize, IProgress<UploadProgress> progress)
            : base(streamToWrite, bufferSize)
        {
            if (streamToWrite == null)
            {
                throw new ArgumentNullException("streamToWrite");
            }

            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException("bufferSize");
            }

            this.streamToWrite = streamToWrite;
            this.bufferSize = bufferSize;
            this.progress = progress;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="T:System.Net.Http.HttpContent"/> and optionally disposes
        ///     of the managed resources.
        /// </summary>
        /// <param name="disposing">
        /// true to release both managed and unmanaged resources; false to releases only unmanaged
        ///     resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.streamToWrite.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Serialize the HTTP content to a stream as an asynchronous operation.
        /// </summary>
        /// <param name="stream">
        /// The target stream.
        /// </param>
        /// <param name="context">
        /// Information about the transport (channel binding token, for example). This parameter may be null.
        /// </param>
        /// <returns>
        /// Returns <see cref="T:System.Threading.Tasks.Task"/>.The task object representing the asynchronous operation.
        /// </returns>
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            this.PrepareContent();

            var buffer = new byte[this.bufferSize];
            var size = this.streamToWrite.Length;
            var uploaded = 0;

            using (this.streamToWrite)
            {
                while (true)
                {
                    var length = this.streamToWrite.Read(buffer, 0, buffer.Length);
                    if (length <= 0)
                    {
                        break;
                    }

                    uploaded += length;
                    this.progress.Report(new UploadProgress(uploaded, size));
                    await stream.WriteAsync(buffer, 0, length);
                }
            }
        }

        /// <summary>
        /// Determines whether the HTTP content has a valid length in bytes.
        /// </summary>
        /// <param name="length">
        /// The length in bytes of the HHTP content.
        /// </param>
        /// <returns>
        /// Returns <see cref="T:System.Boolean"/>.true if <paramref name="length"/> is a valid length; otherwise, false.
        /// </returns>
        protected override bool TryComputeLength(out long length)
        {
            length = this.streamToWrite.Length;
            return true;
        }

        /// <summary>
        ///     Prepares the content.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">The stream has already been read.</exception>
        private void PrepareContent()
        {
            if (this.contentConsumed)
            {
                // If the content needs to be written to a target stream a 2nd time, then the stream must support
                // seeking (e.g. a FileStream), otherwise the stream can't be copied a second time to a target 
                // stream (e.g. a NetworkStream).
                if (this.streamToWrite.CanSeek)
                {
                    this.streamToWrite.Position = 0;
                }
                else
                {
                    throw new InvalidOperationException("The stream has already been read.");
                }
            }

            this.contentConsumed = true;
        }

        #endregion
    }
}