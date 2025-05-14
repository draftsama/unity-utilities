using System;
using System.Net;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Net.Sockets;
using Cysharp.Threading.Tasks;

using System.Net.NetworkInformation;
using Ping = System.Net.NetworkInformation.Ping;
using System.Collections.Generic;



namespace Modules.Utilities
{
    public static class NetworkUtility
    {
        // <summary>
        // Check if the device is connected to the internet
        // </summary>

        public static async UniTask<bool> IsInternetAvailable()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    using (var cts = new CancellationTokenSource(10000))
                    {
                        var result = await client.GetAsync("http://www.google.com", cts.Token);
                        return result.IsSuccessStatusCode;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
                return false;
            }
        }

        /// <summary>
        /// Ping a host or IP address to check if it is reachable
        /// </summary>
        /// <param name="_hostnameOrIpAddress">The hostname or IP address to ping</param>
        /// <param name="_timeout">The timeout in milliseconds</param>
        /// <returns>True if the host is reachable, false otherwise</returns>
        /// <remarks>Note: This method uses the System.Net.NetworkInformation.Ping class</remarks>
        /// <remarks>Note: This method is asynchronous and returns a UniTask</remarks>
        /// <remarks>Note: This method may throw an exception if the ping fails</remarks>
        public static async UniTask<bool> Ping(string _hostnameOrIpAddress = "google.com", int _timeout = 10000)
        {
            var ping = new Ping();

            try
            {
                byte[] buffer = new byte[32]; //array that contains data to be sent with the ICMP echo
                PingOptions pingOptions = new PingOptions(64, true);

                var reply = await ping.SendPingAsync(_hostnameOrIpAddress, _timeout, buffer, pingOptions);
                return reply.Status == IPStatus.Success;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                ping.Dispose();
            }


        }



        public static string GetIPAddress(string _hostname = "google.com")
        {
            IPHostEntry host = new IPHostEntry();
            try
            {
                host = Dns.GetHostEntry("google.com"); //Get the IP host entry from your host/server
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
            finally { }


            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork) //filter just the IPv4 IPs
                {                                                                      //you can play around with this and get all the IP arrays (if any)
                    return ip.ToString();                                              //and check the connection with all of then if needed
                }
            }
            return string.Empty;
        }

        public static bool IsValidURL(string URL)
        {
            string Pattern = @"^(?:http(s)?:\/\/)?[\w.-]+(?:\.[\w\.-]+)+[\w\-\._~:/?#[\]@!\$&'\(\)\*\+,;=.]+$";
            Regex Rgx = new Regex(Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            return Rgx.IsMatch(URL);
        }

        public static string GetLocalIPv4()
        {
            string localIP = "";
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip.ToString();
                    break; // Remove this line if you want all local IPs
                }
            }
            return localIP;
        }

        public static async UniTask<List<string>> ScanLAN(string baseIP = "192.168.1." , int timeout = 1000)
        {
    

            // Create a list to store the alive IPs
            List<string> aliveIPs = new List<string>();
            List<UniTask> tasks = new List<UniTask>();

            for (int i = 1; i < 255; i++)
            {
                string ip = $"{baseIP}{i}";
                tasks.Add(Ping(ip, timeout).ContinueWith(t =>
                {
                    if (t)
                        aliveIPs.Add(ip);
                }));
            }

            await UniTask.WhenAll(tasks);
            return aliveIPs;
        }
       





    }
}