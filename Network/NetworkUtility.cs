using System;
using System.Net;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Modules.Utilities
{
    public static class NetworkUtility
    {
        public static bool Ping(string _hostname = "google.com")
        {
            try
            {
                //I strongly recommend to check Ping, Ping.Send & PingOptions on microsoft C# docu or other C# info source
                //in this block you configure the ping call to your host or server in order to check if there is network connection.
         
                //from https://stackoverflow.com/questions/55461884/how-to-ping-for-ipv4-only
                //from https://stackoverflow.com/questions/49069381/why-ping-timeout-is-not-working-correctly
                //and from https://stackoverflow.com/questions/2031824/what-is-the-best-way-to-check-for-internet-connectivity-using-net
         
         
                System.Net.NetworkInformation.Ping myPing = new System.Net.NetworkInformation.Ping();
         
                byte[] buffer = new byte[32]; //array that contains data to be sent with the ICMP echo
                int timeout = 10000; //in milliseconds
                System.Net.NetworkInformation.PingOptions pingOptions = new System.Net.NetworkInformation.PingOptions(64, true);

                string ip = GetIPAddress(_hostname);
                if (string.IsNullOrEmpty(ip)) return false;
                
                System.Net.NetworkInformation.PingReply reply = myPing.Send(ip, timeout, buffer, pingOptions); //the same method can be used without the timeout, data buffer & pingOptions overloadd but this works for me
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    return true;
                }
                else if(reply.Status == System.Net.NetworkInformation.IPStatus.TimedOut) //to handle the timeout scenario
                {
                    return false;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e) //To catch any exception of the method
            {
                Debug.Log(e);
                return false;
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
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) //filter just the IPv4 IPs
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

        
        
    }
}