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
        public static async Task<byte[]> LoadFileAsync(string filePath, IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {


            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found", filePath);
            }

            var bytes = default(byte[]);

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
            {
                using (var binaryReader = new BinaryReader(fileStream))
                {
                    var totalBytes = (int)fileStream.Length;
                    var bytesProcessed = 0;
                    var buffer = new byte[4096];

                    while (bytesProcessed < totalBytes)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var bytesRead = await binaryReader.BaseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        if (bytesRead == 0)
                        {
                            break;
                        }

                        bytesProcessed += bytesRead;
                        if (progress != null)
                        {
                            var percentage = (float)bytesRead / totalBytes;
                            progress?.Report(percentage);
                        }
                    }

                    bytes = buffer;
                }
            }

            return bytes;
        }





        public static async Task<byte[]> DownloadFileAsync(string url, IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {
            if (!NetworkUtility.IsValidURL(url))
            {
                throw new FileNotFoundException("url is invalid", url);
            }

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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
                        if (cancellationToken.IsCancellationRequested)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                        if (bytesRead == 0)
                        {
                            break;
                        }

                        await memoryStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        bytesDownloaded += bytesRead;

                        if (progress != null)
                        {
                            var percentage = (float)bytesDownloaded / (float)totalBytes;
                            progress.Report(percentage);
                        }

                    } while (true);

                    return memoryStream.ToArray();
                }
            }
        }
    }
}
