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
        public static async Task<byte[]> LoadFileAsync(string filePath, IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found", filePath);
            }


            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var buffer = new byte[fileStream.Length];
                var bytesRead = 0;
                var bytesRemaining = buffer.Length;

                while (bytesRemaining > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var numRead = await fileStream.ReadAsync(buffer, bytesRead, bytesRemaining, cancellationToken);
                    if (numRead == 0)
                    {
                        // End of file
                        break;
                    }

                    bytesRead += numRead;
                    bytesRemaining -= numRead;

                    // Report progress
                    if (progress != null)
                    {
                        var percentComplete = (float)bytesRead / (float)buffer.Length;
                        progress.Report(percentComplete);
                    }
                }

                return buffer;
            }
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
