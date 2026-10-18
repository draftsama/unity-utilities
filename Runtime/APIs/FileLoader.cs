using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Modules.Utilities;
using UnityEngine;

namespace Modules.Utilities
{
    public static class FileLoader
    {

        
        //not support audio files
        
        /// <summary>
        ///  Load file from local path
        /// </summary>
        /// <param name="_filePath">File path</param>
        /// <param name="_progress">Progress</param>
        /// <param name="_cancellationToken">Cancellation token</param>
        /// <returns>Returns a byte array containing the file data</returns>
        /// <exception cref="OperationCanceledException">Thrown when the operation is canceled</exception>
        /// <exception cref="Exception">Thrown when an error occurs during the file loading</exception>
        public static async Task<byte[]> LoadLocalFileAsync(string _filePath, IProgress<float> _progress = null, CancellationToken _cancellationToken = default)
        {

            
            if (!File.Exists(_filePath))
            {
                throw new FileNotFoundException("File not found", _filePath);
            }

            var bytes = default(byte[]);

            using (var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
            {
                using (var binaryReader = new BinaryReader(fileStream))
                {
                    var totalBytes = (int)fileStream.Length;
                    var bytesProcessed = 0;
                    var buffer = new byte[4096];

                    while (bytesProcessed < totalBytes)
                    {
                        _cancellationToken.ThrowIfCancellationRequested();

                        var bytesRead = await binaryReader.BaseStream.ReadAsync(buffer, 0, buffer.Length, _cancellationToken);
                        if (bytesRead == 0)
                        {
                            break;
                        }

                        bytesProcessed += bytesRead;
                        if (_progress != null)
                        {
                            var percentage = (float)bytesRead / totalBytes;
                            _progress?.Report(percentage);
                        }
                    }

                    bytes = buffer;
                }
            }

            return bytes;
        }

        /// <summary>
        /// Download file from url
        /// </summary>
        /// <param name="_url">URL of the file</param>
        /// <param name="_progress">Progress</param>
        /// <param name="_cancellationToken">Cancellation token</param>
        /// <returns>Returns a byte array containing the file data</returns>
        /// <exception cref="OperationCanceledException">Thrown when the operation is canceled</exception>
        /// <exception cref="Exception">Thrown when an error occurs during the download</exception>
        
        private const int DownloadHeaderTimeoutSeconds = 15;
        private const int DownloadStallTimeoutSeconds = 30;

        public static async Task<byte[]> DownloadFileAsync(string _url, IProgress<float> _progress = null, CancellationToken _cancellationToken = default)
        {
            if (!NetworkUtility.IsValidURL(_url))
            {
                throw new FileNotFoundException("url is invalid", _url);
            }

            // HttpClient.Timeout only covers waiting for the response headers here; the body is guarded per read below.
            using (var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(DownloadHeaderTimeoutSeconds) })
            {
                HttpResponseMessage response;
                try
                {
                    response = await httpClient.GetAsync(_url, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
                }
                catch (OperationCanceledException) when (!_cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"No response from {_url} within {DownloadHeaderTimeoutSeconds}s");
                }

                using (response)
                {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var memoryStream = new MemoryStream())
                {
                    var buffer = new byte[4096];
                    var bytesRead = default(int);
                    var bytesDownloaded = 0L;

                    do
                    {
                        if (_cancellationToken.IsCancellationRequested)
                        {
                            _cancellationToken.ThrowIfCancellationRequested();
                        }

                        using (var stallCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken))
                        {
                            stallCts.CancelAfter(TimeSpan.FromSeconds(DownloadStallTimeoutSeconds));
                            try
                            {
                                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, stallCts.Token);
                            }
                            catch (OperationCanceledException) when (!_cancellationToken.IsCancellationRequested)
                            {
                                throw new TimeoutException($"Download from {_url} stalled for {DownloadStallTimeoutSeconds}s");
                            }
                        }

                        if (bytesRead == 0)
                        {
                            break;
                        }

                        await memoryStream.WriteAsync(buffer, 0, bytesRead, _cancellationToken);
                        bytesDownloaded += bytesRead;

                        // Content-Length is absent for chunked responses, so there is nothing to compute a percentage from
                        if (_progress != null && totalBytes.HasValue && totalBytes.Value > 0)
                        {
                            var percentage = (float)bytesDownloaded / totalBytes.Value;
                            _progress.Report(percentage);
                        }

                    } while (true);

                    if (totalBytes.HasValue && bytesDownloaded != totalBytes.Value)
                    {
                        throw new IOException(
                            $"Incomplete download: received {bytesDownloaded} of {totalBytes.Value} bytes from {_url}");
                    }

                    return memoryStream.ToArray();
                }
                }
            }
        }
    }
}
