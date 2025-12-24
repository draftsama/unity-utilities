using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Modules.Utilities
{
    public class FTPConnector : MonoBehaviour
    {

        [Header("FTP Settings")]
        [Tooltip("Example: ftp://127.0.0.1:2121 (Don't forget the port if it's not 21)")]
        [SerializeField] private string host = "ftp://your-ftp-server.com:2121";
        [SerializeField] private string username = "user";
        [SerializeField] private string password = "password";

        [Header("Public URL Settings")]
        [Tooltip("The base HTTP/HTTPS URL that corresponds to the FTP upload directory.")]
        [SerializeField] private string httpBaseUrl = "http://your-website.com";


       /// <summary>
    /// Updates FTP credentials and the optional public base URL at runtime.
    /// </summary>
    /// <param name="newHost">The FTP host URL (e.g., ftp://127.0.0.1:2121).</param>
    /// <param name="newUsername">FTP Username.</param>
    /// <param name="newPassword">FTP Password.</param>
    /// <param name="newHttpBaseUrl">Optional: The base URL for public access.</param>
    public void SetCredentials(string newHost, string newUsername, string newPassword, string newHttpBaseUrl = "")
    {
        // Remove trailing slash to prevent double slashes in URI generation
        this.host = newHost.TrimEnd('/');
        this.username = newUsername;
        this.password = newPassword;
        
        if (!string.IsNullOrEmpty(newHttpBaseUrl))
        {
            this.httpBaseUrl = newHttpBaseUrl.TrimEnd('/');
        }
        
        Debug.Log($"[FTP] Credentials updated. Host: {this.host}");
    }

    /// <summary>
    /// Uploads a local file to a specific remote folder on the FTP server asynchronously.
    /// </summary>
    /// <param name="localFilePath">Full path to the local file on the device.</param>
    /// <param name="remoteFileName">The name of the file to save on the server (e.g., image.png).</param>
    /// <param name="remoteFolder">The target folder path on the server (e.g., public_html/qrcode).</param>
    /// <param name="progress">Optional: Progress reporter (0.0 to 1.0).</param>
    /// <returns>The generated public URL of the uploaded file, or null if the upload fails or base URL is missing.</returns>
    public async UniTask<string> UploadFileAsync(string localFilePath, string remoteFileName, string remoteFolder = "", IProgress<float> progress = null)
    {
        // 1. Validation: Check if the local file exists
        if (!File.Exists(localFilePath))
        {
            Debug.LogError($"[FTP] File not found: {localFilePath}");
            return null;
        }

        // Capture current credentials to ensure thread safety
        string currentHost = host;
        string currentUser = username;
        string currentPass = password;
        string currentBaseUrl = httpBaseUrl;

        // Clean the remote folder path (remove leading/trailing slashes)
        string cleanFolder = remoteFolder.Trim('/');
        
        // Construct the full FTP path: ftp://host:port/folder/filename
        string uploadPath = string.IsNullOrEmpty(cleanFolder) ? remoteFileName : $"{cleanFolder}/{remoteFileName}";
        string targetUri = $"{currentHost}/{uploadPath}";

        Debug.Log($"[FTP] Uploading to: {targetUri}");

        // 2. Execution: Run on ThreadPool to avoid blocking the Unity Main Thread
        await UniTask.RunOnThreadPool(async () =>
        {
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(targetUri);
                request.Method = WebRequestMethods.Ftp.UploadFile;
                request.Credentials = new NetworkCredential(currentUser, currentPass);
                request.UseBinary = true;
                request.KeepAlive = false;

                // Open file stream and request stream
                using (FileStream fs = new FileStream(localFilePath, FileMode.Open, FileAccess.Read))
                using (Stream requestStream = await request.GetRequestStreamAsync())
                {
                    byte[] buffer = new byte[8192]; // 8KB buffer size
                    long totalBytes = fs.Length;
                    long uploadedBytes = 0;
                    int read;

                    // Write file data to the request stream
                    while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        await requestStream.WriteAsync(buffer, 0, read);
                        uploadedBytes += read;

                        // Report progress back to the caller
                        progress?.Report((float)uploadedBytes / totalBytes);
                    }
                }
                Debug.Log($"[FTP] Upload Complete: {remoteFileName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FTP] Upload Error: {ex.Message}");
                throw; // Re-throw exception to be handled by the caller
            }
        });

        // 3. URL Generation: Construct the public URL
        if (string.IsNullOrEmpty(currentBaseUrl)) return null;

        // NOTE: Standard web servers usually map 'public_html' to the root domain.
        // If your URL structure doesn't include 'public_html', we remove it here.
        string urlFolder = cleanFolder.Replace("public_html/", "").Replace("public_html", "");
        
        // Ensure clean slashes for the final URL
        string finalUrl = $"{currentBaseUrl.TrimEnd('/')}/{urlFolder}/{remoteFileName}";
        
        // Clean up any double slashes that might have occurred from empty folders
        finalUrl = finalUrl.Replace("://", "###").Replace("//", "/").Replace("###", "://");

        return finalUrl;
    }

    /// <summary>
    /// Downloads a file from the FTP server to a local path asynchronously.
    /// </summary>
    /// <param name="remotePath">The full path of the file on the FTP server (e.g., public_html/qrcode/image.png).</param>
    /// <param name="localSavePath">Full local path where the file will be saved.</param>
    /// <param name="progress">Optional: Progress reporter (0.0 to 1.0).</param>
    public async UniTask DownloadFileAsync(string remotePath, string localSavePath, IProgress<float> progress = null)
    {
        string currentHost = host;
        string currentUser = username;
        string currentPass = password;

        await UniTask.RunOnThreadPool(async () =>
        {
            try
            {
                string targetUri = $"{currentHost}/{remotePath}";
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(targetUri);
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.Credentials = new NetworkCredential(currentUser, currentPass);
                request.UseBinary = true;

                // Attempt to get file size for progress calculation
                long totalBytes = await GetFileSizeAsync(targetUri, currentUser, currentPass);

                using (FtpWebResponse response = (FtpWebResponse)await request.GetResponseAsync())
                using (Stream responseStream = response.GetResponseStream())
                using (FileStream fs = new FileStream(localSavePath, FileMode.Create))
                {
                    byte[] buffer = new byte[8192];
                    int read;
                    long downloadedBytes = 0;

                    while ((read = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, read);
                        downloadedBytes += read;

                        if (totalBytes > 0)
                        {
                            progress?.Report((float)downloadedBytes / totalBytes);
                        }
                    }
                }

                Debug.Log($"[FTP] Download Complete: {localSavePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FTP] Download Error: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// Helper method to retrieve the file size from the FTP server.
    /// </summary>
    private async UniTask<long> GetFileSizeAsync(string targetUri, string user, string pass)
    {
        try
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(targetUri);
            request.Method = WebRequestMethods.Ftp.GetFileSize;
            request.Credentials = new NetworkCredential(user, pass);
            
            using (FtpWebResponse response = (FtpWebResponse)await request.GetResponseAsync())
            {
                return response.ContentLength;
            }
        }
        catch
        {
            return -1; // Return -1 if unable to retrieve size
        }
    }

        // ... (DownloadFileAsync remains the same) ...
        // public string filepathTest = "";

        // [Button]
        // public async Task TestUpload()
        // {
        //     string localPath = filepathTest;
        //     string remoteFileName = Path.GetFileName(localPath);

        //     string targetFolder = "public_html/qrcode";

        //     try
        //     {

        //         var result = await UploadFileAsync(localPath, remoteFileName, targetFolder, new Progress<float>(p =>
        //          {
        //              Debug.Log($"[FTP] Upload Progress: {p * 100f}%");
        //          }));

        //         if (!string.IsNullOrEmpty(result))
        //         {
        //             Debug.Log($"[FTP] File uploaded successfully. Public URL: {result}");
        //         }
        //         else
        //         {
        //             Debug.LogWarning("[FTP] Upload succeeded but no public URL generated (check httpBaseUrl).");
        //         }

        //     }
        //     catch (Exception ex)
        //     {
        //         Debug.LogError($"[FTP] Test Upload Failed: {ex.Message}");
        //     }

           
           
        // }

    }



}