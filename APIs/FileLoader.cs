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
        
        public static async Task<byte[]> DownloadFileAsync(string _url, IProgress<float> _progress = null, CancellationToken _cancellationToken = default)
        {
            if (!NetworkUtility.IsValidURL(_url))
            {
                throw new FileNotFoundException("url is invalid", _url);
            }

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(_url, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
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

                        bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, _cancellationToken);

                        if (bytesRead == 0)
                        {
                            break;
                        }

                        await memoryStream.WriteAsync(buffer, 0, bytesRead, _cancellationToken);
                        bytesDownloaded += bytesRead;

                        if (_progress != null)
                        {
                            var percentage = (float)bytesDownloaded / (float)totalBytes;
                            _progress.Report(percentage);
                        }

                    } while (true);

                    return memoryStream.ToArray();
                }
            }
        }
    }
}
