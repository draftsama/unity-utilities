using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
#if PACKAGE_NEWTONSOFT_JSON_INSTALLED
using Newtonsoft.Json;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modules.Utilities
{
    /// <summary>
    /// A networking connector using TCP for reliable communication,
    /// with an optional UDP-based auto-discovery feature for local networks.
    /// </summary>
    public class TCPConnector : MonoBehaviour
    {
        #region Private Variables

        [Header("Connection Settings")]
        [SerializeField] public string m_Host = "127.0.0.1";
        [SerializeField] public int m_Port = 7777;
        [SerializeField] public bool m_IsServer = false;

        [Header("Behavior")]
        [SerializeField] public bool m_StartOnEnable = true;
        [SerializeField] public bool m_IsDebug = true;

        [Header("Data Transmission")]
        [SerializeField, Range(0, 10), Tooltip("Maximum number of retry attempts when data sending fails")]
        public int m_MaxRetryCount = 5;
        [SerializeField, Range(0, 5000), Tooltip("Delay in milliseconds between retry attempts")]
        public int m_RetryDelay = 100;

        [Header("Auto Discovery (UDP Broadcast)")]
        [SerializeField] public bool m_EnableDiscovery = true;

        [SerializeField, Range(0, 5000)] public int m_DiscoveryInterval = 1000; // milliseconds
        [SerializeField] public int m_DiscoveryPort = 7778;
        [SerializeField] public string m_DiscoveryMessage = "DiscoverServer";

        // --- Status ---
        [SerializeField] private bool m_IsRunning = false;
        [SerializeField] private bool m_IsConnected = false;

        // --- TCP Components ---
        private TcpListener _tcpListener; // Server
        private TcpClient _tcpClient;     // Client
        private readonly Dictionary<TcpClient, ConnectorInfo> m_Clients = new Dictionary<TcpClient, ConnectorInfo>();

        [SerializeField] private ConnectorInfo m_ServerInfo;

        // --- Events ---
        [SerializeField] private UnityEvent<ConnectorInfo> m_OnClientConnected = new UnityEvent<ConnectorInfo>();
        [SerializeField] private UnityEvent<ConnectorInfo> m_OnClientDisconnected = new UnityEvent<ConnectorInfo>();
        [SerializeField] private UnityEvent<ConnectorInfo> m_OnServerConnected = new UnityEvent<ConnectorInfo>();
        [SerializeField] private UnityEvent<ConnectorInfo> m_OnServerDisconnected = new UnityEvent<ConnectorInfo>();
        [SerializeField] private UnityEvent<PacketResponse> m_OnDataReceived = new UnityEvent<PacketResponse>();
        [SerializeField] private UnityEvent<ErrorInfo> m_OnError = new UnityEvent<ErrorInfo>();

        private CancellationTokenSource _cts;

        // --- Packet Structure ---
        private const int PACKET_HEADER_SIZE = 10; // 4-id, 4-sender, 2-type

        #endregion

        #region Public Variables
        public bool IsConnected => m_IsConnected;
        public bool IsRunning => m_IsRunning;
        public List<ConnectorInfo> ClientInfoList => m_Clients.Values.ToList();
        public ConnectorInfo ServerInfo => m_ServerInfo;

        /// <summary>
        /// Checks if the connection is healthy for data transmission
        /// </summary>
        /// <returns>True if connection is ready for sending data</returns>
        private bool IsConnectionHealthy()
        {
            if (!m_IsRunning) return false;

            if (m_IsServer)
            {
                lock (m_Clients)
                {
                    return m_Clients.Keys.Any(client => client != null && client.Connected);
                }
            }
            else
            {
                return m_IsConnected && _tcpClient != null && _tcpClient.Connected;
            }
        }

        /// <summary>
        /// Checks if an exception is an expected connection closure (not an error to report)
        /// </summary>
        /// <param name="ex">The exception to check</param>
        /// <returns>True if this is an expected connection closure</returns>
        private static bool IsExpectedConnectionClosure(Exception ex)
        {
            return ex switch
            {
                OperationCanceledException => true,
                ObjectDisposedException => true,
                SocketException socketEx => socketEx.SocketErrorCode == SocketError.OperationAborted ||
                                          socketEx.SocketErrorCode == SocketError.ConnectionAborted ||
                                          socketEx.SocketErrorCode == SocketError.ConnectionReset,
                IOException ioEx when ioEx.InnerException is SocketException innerSocketEx =>
                    innerSocketEx.SocketErrorCode == SocketError.OperationAborted ||
                    innerSocketEx.SocketErrorCode == SocketError.ConnectionAborted ||
                    innerSocketEx.SocketErrorCode == SocketError.ConnectionReset,
                _ => false
            };
        }
        #endregion

        #region Unity Methods

        private void Awake()
        {
            // Validate retry configuration
            if (m_MaxRetryCount < 0)
            {
                Debug.LogWarning($"[TCPConnector] MaxRetryCount cannot be negative. Setting to 0.");
                m_MaxRetryCount = 0;
            }

            if (m_RetryDelay < 0)
            {
                Debug.LogWarning($"[TCPConnector] RetryDelay cannot be negative. Setting to 0.");
                m_RetryDelay = 0;
            }
        }

        private void OnEnable()
        {
            if (m_StartOnEnable)
                StartConnection().Forget();
        }

        private void OnDisable()
        {
            StopConnection();
        }

        private void OnDestroy()
        {
            StopConnection();
        }

        #endregion

        #region Core Logic

        private void Log(object message)
        {
            if (!m_IsDebug) return;
            var prefix = m_IsServer ? "Server" : "Client";
            Debug.Log($"[TCP-{prefix}] {message}");
        }

        public async UniTask StartConnection()
        {
            if (m_IsRunning)
            {
                Log("Connection is already running.");
                return;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            if (m_IsServer)
            {
                m_IsRunning = true; // <<< Set IsRunning true for server here
                if (m_EnableDiscovery)
                {
                    BroadcastPresenceAsync(_cts.Token).Forget();
                }
                await StartServerAsync(_cts.Token);
            }
            else
            {
                m_IsRunning = true; // <<< Set IsRunning true for client here
                // <<< This is the new main client loop that handles reconnects
                ClientConnectionLoopAsync(_cts.Token).Forget();
            }
        }

        public void StopConnection()
        {
            Log("Stopping connection...");
            m_IsRunning = false; // <<< Set IsRunning false first
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (m_IsServer)
            {
                _tcpListener?.Stop();
                lock (m_Clients)
                {
                    foreach (var client in m_Clients.Keys)
                    {
                        client.Close();
                    }
                    m_Clients.Clear();
                }
                Log("Server and all client connections closed.");
            }
            else
            {
                _tcpClient?.Close();
                if (m_IsConnected)
                {
                    m_IsConnected = false;
                    m_OnServerDisconnected?.Invoke(m_ServerInfo);
                }
                Log("Client connection closed.");
            }
        }

        #endregion

        #region Server Methods

        private async UniTask StartServerAsync(CancellationToken token)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, m_Port);
                _tcpListener.Start();
                Log($"listening on TCP port {m_Port}.");

                while (!token.IsCancellationRequested)
                {
                    TcpClient connectedClient = await _tcpListener.AcceptTcpClientAsync().AsUniTask().AttachExternalCancellation(token);
                    HandleClientAsync(connectedClient, token).Forget();
                }
            }
            catch (OperationCanceledException)
            {
                Log("Server operation was canceled.");
            }
            catch (ObjectDisposedException)
            {
                // TcpListener was disposed - this is expected during shutdown
                Log("Server listener disposed.");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
            {
                // Socket operation was aborted - this is expected during shutdown
                Log("Server socket operation aborted.");
            }
            catch (Exception ex)
            {
                Log($"Server Error: {ex.GetType().Name} - {ex.Message}");
                ReportError(ErrorInfo.ErrorType.Connection, "Server failed to start or accept connections", ex);
            }
            finally
            {
                m_IsRunning = false;
                _tcpListener?.Stop();
            }
        }

        private async UniTask HandleClientAsync(TcpClient client, CancellationToken token)
        {
            var clientEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
            var clientInfo = new ConnectorInfo
            {
                id = client.GetHashCode(),
                ipAddress = clientEndPoint.Address.ToString(),
                port = clientEndPoint.Port,
                remoteEndPoint = clientEndPoint
            };

            lock (m_Clients) { m_Clients.Add(client, clientInfo); }
            Log($"Client connected: {clientInfo.ipAddress}:{clientInfo.port}");
            await UniTask.SwitchToMainThread();
            m_OnClientConnected?.Invoke(clientInfo);

            var stream = client.GetStream();
            try
            {
                await ReceiveDataLoopAsync(stream, clientInfo, token);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted ||
                                            ex.SocketErrorCode == SocketError.ConnectionAborted ||
                                            ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                // Connection was closed gracefully or aborted - don't log as error
                Log($"Client connection closed: {clientInfo.ipAddress}:{clientInfo.port}");
            }
            catch (IOException ex) when (ex.InnerException is SocketException socketEx &&
                                       (socketEx.SocketErrorCode == SocketError.OperationAborted ||
                                        socketEx.SocketErrorCode == SocketError.ConnectionAborted ||
                                        socketEx.SocketErrorCode == SocketError.ConnectionReset))
            {
                // Connection was closed gracefully or aborted - don't log as error
                Log($"Client connection closed: {clientInfo.ipAddress}:{clientInfo.port}");
            }
            catch (ObjectDisposedException)
            {
                // Stream was disposed - this is expected during shutdown
                Log($"Client stream disposed: {clientInfo.ipAddress}:{clientInfo.port}");
            }
            catch (Exception ex)
            {
                Log($"Unexpected client handling error: {ex.GetType().Name} - {ex.Message}");
                ReportError(ErrorInfo.ErrorType.DataTransmission, "Unexpected error handling client data", ex, clientInfo);
            }
            finally
            {
                Log($"Client disconnected: {clientInfo.ipAddress}:{clientInfo.port}");
                lock (m_Clients) { m_Clients.Remove(client); }
                client.Close();
                await UniTask.SwitchToMainThread();
                m_OnClientDisconnected?.Invoke(clientInfo);
            }
        }

        private async UniTask BroadcastPresenceAsync(CancellationToken token)
        {
            using var udpClient = new UdpClient { EnableBroadcast = true };
            var broadcastAddress = new IPEndPoint(IPAddress.Broadcast, m_DiscoveryPort);
            var messageBytes = Encoding.UTF8.GetBytes(m_DiscoveryMessage);

            Log($"Starting discovery broadcast on UDP port {m_DiscoveryPort}");
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await udpClient.SendAsync(messageBytes, messageBytes.Length, broadcastAddress);
                    await UniTask.Delay(m_DiscoveryInterval, cancellationToken: token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log($"UDP Broadcast Error: {ex.Message}");
                ReportError(ErrorInfo.ErrorType.Discovery, "UDP broadcast failed", ex);
            }
            finally { Log("Stopping discovery broadcast."); }
        }

        #endregion

        #region Client Methods

        // <<< NEW: The main loop for the client
        private async UniTask ClientConnectionLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                string serverIp = null;
                if (m_EnableDiscovery)
                {
                    Log($"Searching for server on UDP port {m_DiscoveryPort} ...");
                    serverIp = await ListenForServerAsync(token);
                }
                else
                {
                    serverIp = m_Host; // Use manual host if discovery is disabled
                }

                if (!string.IsNullOrEmpty(serverIp) && !token.IsCancellationRequested)
                {
                    Log($"Server found at {serverIp}. Attempting to connect...");
                    m_Host = serverIp;
                    // This method will now block until the connection is lost
                    await StartClientAsync(token);
                }

                // If connection fails or is lost, wait before retrying
                if (!token.IsCancellationRequested)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: token);
                    Log("Retrying connection...");
                }
            }
        }

        private async UniTask StartClientAsync(CancellationToken token)
        {
            // This method now only handles a single connection attempt and lifetime
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(m_Host, m_Port).AsUniTask().AttachExternalCancellation(token);

                if (_tcpClient.Connected)
                {
                    m_IsConnected = true; // Set connected status
                    var serverEndPoint = (IPEndPoint)_tcpClient.Client.RemoteEndPoint;
                    m_ServerInfo = new ConnectorInfo
                    {
                        id = _tcpClient.GetHashCode(),
                        ipAddress = serverEndPoint.Address.ToString(),
                        port = serverEndPoint.Port,
                        remoteEndPoint = serverEndPoint
                    };
                    Log($"Connected to server: {m_ServerInfo.ipAddress}:{m_ServerInfo.port}");
                    await UniTask.SwitchToMainThread();
                    m_OnServerConnected?.Invoke(m_ServerInfo);

                    var stream = _tcpClient.GetStream();
                    await ReceiveDataLoopAsync(stream, m_ServerInfo, token);
                }
            }
            catch (OperationCanceledException)
            {
                Log("Connection attempt canceled.");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted ||
                                            ex.SocketErrorCode == SocketError.ConnectionAborted ||
                                            ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                // Connection was closed gracefully or aborted - don't log as error during shutdown
                Log("Connection closed during client operation.");
            }
            catch (IOException ex) when (ex.InnerException is SocketException socketEx &&
                                       (socketEx.SocketErrorCode == SocketError.OperationAborted ||
                                        socketEx.SocketErrorCode == SocketError.ConnectionAborted ||
                                        socketEx.SocketErrorCode == SocketError.ConnectionReset))
            {
                // Connection was closed gracefully or aborted - don't log as error during shutdown
                Log("Connection closed during client operation.");
            }
            catch (ObjectDisposedException)
            {
                // TcpClient was disposed - this is expected during shutdown
                Log("Client disposed during operation.");
            }
            catch (Exception ex)
            {
                Log($"Unexpected client error: {ex.GetType().Name} - {ex.Message}");
                ReportError(ErrorInfo.ErrorType.Connection, "Unexpected client connection error", ex);
            }
            finally
            {
                if (m_IsConnected)
                {
                    Log("Disconnected from server.");
                    m_IsConnected = false;
                    await UniTask.SwitchToMainThread();
                    m_OnServerDisconnected?.Invoke(m_ServerInfo);
                }
                _tcpClient?.Close();
            }
        }

        private async UniTask<string> ListenForServerAsync(CancellationToken token)
        {
            using var udpClient = new UdpClient();
            // <<< Allow reusing the address to avoid issues on quick restarts
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, m_DiscoveryPort));

            try
            {
                var receiveResult = await udpClient.ReceiveAsync().AsUniTask().AttachExternalCancellation(token);

                var receivedMessage = Encoding.UTF8.GetString(receiveResult.Buffer);
                if (receivedMessage == m_DiscoveryMessage)
                {
                    return receiveResult.RemoteEndPoint.Address.ToString();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log($"UDP Listen Error: {ex.Message}");
                ReportError(ErrorInfo.ErrorType.Discovery, "UDP listen failed", ex);
            }
            return null;
        }

        #endregion

        #region Data Handling
        // ... (No changes in this region) ...
        private async UniTask ReceiveDataLoopAsync(NetworkStream stream, ConnectorInfo connectorInfo, CancellationToken token)
        {
            var lengthBuffer = new byte[4];
            while (!token.IsCancellationRequested && stream.CanRead)
            {
                try
                {
                    int bytesRead = await stream.ReadAsync(lengthBuffer, 0, lengthBuffer.Length, token);
                    if (bytesRead < 4) break; // Connection closed

                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                    if (messageLength <= 0 || messageLength > 1024 * 1024 * 4) // <<< Sanity check for message size (e.g., max 4MB)
                    {
                        Log($"Invalid message length received: {messageLength}. Disconnecting.");
                        ReportError(ErrorInfo.ErrorType.Protocol, $"Invalid message length: {messageLength}", null, connectorInfo);
                        break;
                    }

                    var dataBuffer = new byte[messageLength];
                    string operationId = Guid.NewGuid().ToString();

                    // Create initial packet response with receiving status
                    var packetResponse = new PacketResponse
                    {
                        status = PacketResponse.ReceiveStatus.Receiving,
                        totalBytes = messageLength,
                        processedBytes = 0,
                        remoteEndPoint = connectorInfo.remoteEndPoint,
                        operationId = operationId
                    };

                    int totalBytesRead = 0;
                    var lastProgressReport = System.DateTime.Now;
                    const int progressReportIntervalMs = 100; // Report progress every 100ms max

                    while (totalBytesRead < messageLength)
                    {
                        bytesRead = await stream.ReadAsync(dataBuffer, totalBytesRead, messageLength - totalBytesRead, token);
                        if (bytesRead == 0)
                        {
                            ReportError(ErrorInfo.ErrorType.DataTransmission, "Connection closed prematurely during data receive", null, connectorInfo, operationId);
                            throw new InvalidOperationException("Connection closed prematurely.");
                        }
                        totalBytesRead += bytesRead;

                        // Update progress in packet response
                        packetResponse.processedBytes = totalBytesRead;

                        // Throttle progress reports to avoid UI spam
                        var now = System.DateTime.Now;
                        if ((now - lastProgressReport).TotalMilliseconds >= progressReportIntervalMs ||
                            totalBytesRead >= messageLength)
                        {
                            lastProgressReport = now;
                            await UniTask.SwitchToMainThread();
                            // Log($"Received {totalBytesRead}/{messageLength} bytes from {connectorInfo.ipAddress}:{connectorInfo.port}");
                            m_OnDataReceived?.Invoke(packetResponse);
                        }

                        await UniTask.Yield();
                    }

                    // Parse the complete packet and update status
                    var finalPacketResponse = ReadPacket(dataBuffer, connectorInfo.remoteEndPoint);
                    if (finalPacketResponse != null)
                    {
                        // Copy progress information to final response
                        finalPacketResponse.status = PacketResponse.ReceiveStatus.Received;
                        finalPacketResponse.totalBytes = messageLength;
                        finalPacketResponse.processedBytes = totalBytesRead;
                        finalPacketResponse.operationId = operationId;

                        await UniTask.SwitchToMainThread();
                        Log($"Received complete packet: {finalPacketResponse.action} from {connectorInfo.ipAddress}:{connectorInfo.port}");
                        m_OnDataReceived?.Invoke(finalPacketResponse);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted ||
                                                 ex.SocketErrorCode == SocketError.ConnectionAborted ||
                                                 ex.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // Connection was closed gracefully or aborted - this is expected during shutdown
                    Log($"Connection closed: {connectorInfo.ipAddress}:{connectorInfo.port}");
                    break;
                }
                catch (IOException ex) when (ex.InnerException is SocketException socketEx &&
                                           (socketEx.SocketErrorCode == SocketError.OperationAborted ||
                                            socketEx.SocketErrorCode == SocketError.ConnectionAborted ||
                                            socketEx.SocketErrorCode == SocketError.ConnectionReset))
                {
                    // Connection was closed gracefully or aborted - this is expected during shutdown
                    Log($"Connection closed: {connectorInfo.ipAddress}:{connectorInfo.port}");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // Stream was disposed - this is expected during shutdown
                    Log($"Stream disposed for: {connectorInfo.ipAddress}:{connectorInfo.port}");
                    break;
                }
                catch (Exception ex)
                {
                    // Only log unexpected errors
                    Log($"Unexpected receive error: {ex.GetType().Name} - {ex.Message}");
                    ReportError(ErrorInfo.ErrorType.DataTransmission, "Unexpected data receive error", ex, connectorInfo);
                    break; // Exit the loop on error
                }
            }
        }
        public async UniTask SendDataAsync(ushort action, byte[] data, CancellationToken token = default)
        {
            await SendDataAsync(action, data, null, token);
        }

        /// <summary>
        /// Sends data with automatic retry logic. Will attempt to send up to MaxRetryCount times
        /// with RetryDelaySeconds delay between attempts if sending fails.
        /// 
        /// For servers: Attempts to send to all connected clients. If some clients fail, 
        /// it will retry only if ALL clients failed. Partial failures are logged but not retried.
        /// 
        /// For clients: Retries connection-level failures up to the retry limit.
        /// </summary>
        /// <param name="action">Action identifier</param>
        /// <param name="data">Data to send</param>
        /// <param name="progress">Optional progress reporter (0.0 to 1.0)</param>
        /// <param name="token">Cancellation token</param>
        public async UniTask SendDataAsync(ushort action, byte[] data, IProgress<float> progress, CancellationToken token = default)
        {
            if (!m_IsRunning) return;

            byte[] message = CreatePacket(action, data);
            int retryCount = 0;
            bool success = false;

            while (retryCount <= m_MaxRetryCount && !success && !token.IsCancellationRequested)
            {
                try
                {
                    // Check if we should even attempt to send
                    if (!IsConnectionHealthy())
                    {
                        throw new InvalidOperationException("Connection is not healthy for data transmission");
                    }

                    if (m_IsServer)
                    {
                        // Create a copy of the list to avoid issues if a client disconnects during the send
                        List<TcpClient> clientsToSend;
                        lock (m_Clients) { clientsToSend = m_Clients.Keys.ToList().Where(c => c.Connected).ToList(); }

                        if (clientsToSend.Count == 0)
                        {
                            Log("No connected clients to send data to");
                            return; // No clients to send to, but this isn't an error worth retrying
                        }

                        // Send to all clients, but handle individual client failures gracefully
                        var sendTasks = clientsToSend.Select(async client =>
                        {
                            try
                            {
                                await SendDataToStreamAsync(client.GetStream(), message, progress, token);
                                return true; // Success
                            }
                            catch (Exception ex)
                            {
                                Log($"Failed to send data to client {client.Client.RemoteEndPoint}: {ex.Message}");
                                return false; // Failed for this client
                            }
                        }).ToList();

                        var results = await UniTask.WhenAll(sendTasks);

                        // Check if at least one client received the data successfully
                        int successfulSends = results.Count(r => r);
                        if (successfulSends == 0)
                        {
                            throw new InvalidOperationException($"Failed to send data to any of {clientsToSend.Count} connected clients");
                        }
                        else if (successfulSends < results.Length)
                        {
                            Log($"Data sent successfully to {successfulSends}/{results.Length} clients");
                        }
                    }
                    else if (m_IsConnected && _tcpClient != null && _tcpClient.Connected)
                    {
                        await SendDataToStreamAsync(_tcpClient.GetStream(), message, progress, token);
                    }
                    else
                    {
                        throw new InvalidOperationException("Client is not connected");
                    }

                    success = true; // If we reach here, send was successful
                    if (retryCount > 0)
                    {
                        Log($"Data sent successfully after {retryCount} retries");
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Connection closed") ||
                                                           ex.Message.Contains("Stream disposed"))
                {
                    // Connection was closed during shutdown - don't retry, just exit gracefully
                    Log($"Send operation aborted due to connection closure");
                    break;
                }
                catch (OperationCanceledException)
                {
                    // Operation was cancelled - don't log as error
                    Log($"Send operation cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount > m_MaxRetryCount)
                    {
                        Log($"Send Data Failed after {m_MaxRetryCount} retries: {ex.Message}");
                        ReportError(ErrorInfo.ErrorType.DataTransmission, $"Data send failed after {m_MaxRetryCount} retries", ex);
                        break;
                    }
                    else
                    {
                        Log($"Send Data Failed (attempt {retryCount}/{m_MaxRetryCount}), retrying in {m_RetryDelay}ms: {ex.Message}");
                        await UniTask.Delay(m_RetryDelay, cancellationToken: token);
                    }
                }
            }
        }

        /// <summary>
        /// Sends data to a specific NetworkStream with progress reporting.
        /// This method handles the actual data transmission with chunking for progress updates.
        /// </summary>
        private async UniTask SendDataToStreamAsync(NetworkStream stream, byte[] message, IProgress<float> progress = null, CancellationToken token = default)
        {
            if (stream == null || !stream.CanWrite)
            {
                throw new InvalidOperationException("Stream is not available for writing");
            }

            const int chunkSize = 8192; // 8KB chunks for progress reporting
            int offset = 0;

            try
            {
                while (offset < message.Length && !token.IsCancellationRequested)
                {
                    int bytesToSend = Math.Min(chunkSize, message.Length - offset);
                    await stream.WriteAsync(message, offset, bytesToSend, token);
                    offset += bytesToSend;

                    // Report progress as percentage (0.0 to 1.0)
                    float progressPercentage = (float)offset / message.Length;
                    progress?.Report(progressPercentage);

                    await UniTask.Yield();
                }

                // Ensure all data is sent
                await stream.FlushAsync(token);
                Log($"Data sent successfully: {message.Length} bytes");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted ||
                                             ex.SocketErrorCode == SocketError.ConnectionAborted ||
                                             ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                // Connection was closed - don't log as error during shutdown
                throw new InvalidOperationException("Connection closed during send operation", ex);
            }
            catch (IOException ex) when (ex.InnerException is SocketException socketEx &&
                                       (socketEx.SocketErrorCode == SocketError.OperationAborted ||
                                        socketEx.SocketErrorCode == SocketError.ConnectionAborted ||
                                        socketEx.SocketErrorCode == SocketError.ConnectionReset))
            {
                // Connection was closed - don't log as error during shutdown
                throw new InvalidOperationException("Connection closed during send operation", ex);
            }
            catch (ObjectDisposedException ex)
            {
                // Stream was disposed - this is expected during shutdown
                throw new InvalidOperationException("Stream disposed during send operation", ex);
            }
            catch (Exception ex)
            {
                Log($"Stream write error at offset {offset}/{message.Length}: {ex.GetType().Name} - {ex.Message}");
                throw; // Re-throw to be handled by retry logic
            }
        }

        public async UniTask SendDataAsync<T>(ushort action, T data, CancellationToken token = default)
        {
            await SendDataAsync(action, data, null, token);
        }
        public async UniTask SendDataAsync<T>(ushort action, T data, IProgress<float> progress, CancellationToken token = default)
        {
            if (data == null) throw new ArgumentNullException(nameof(data), "Data cannot be null.");
            if (!m_IsRunning) return;

            byte[] serializedData;
            try
            {
#if PACKAGE_NEWTONSOFT_JSON_INSTALLED
                serializedData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
#else
                // Fallback to Unity's JsonUtility if Newtonsoft.Json is not available
                serializedData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data));
#endif
            }
            catch (Exception ex)
            {
                Log($"Serialization Error: {ex.Message}");
                ReportError(ErrorInfo.ErrorType.Serialization, "Data serialization failed", ex);
                return;
            }

            await SendDataAsync(action, serializedData, progress, token);
        }

        private byte[] CreatePacket(ushort action, byte[] data)
        {
            data ??= Array.Empty<byte>();
            byte[] packet = new byte[PACKET_HEADER_SIZE + data.Length];
            int offset = 0;

            int myId = m_IsServer ? -1 : _tcpClient?.GetHashCode() ?? 0;

            Buffer.BlockCopy(BitConverter.GetBytes(new System.Random().Next()), 0, packet, offset, 4); offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(myId), 0, packet, offset, 4); offset += 4;

            Buffer.BlockCopy(BitConverter.GetBytes(action), 0, packet, offset, 2);

            Buffer.BlockCopy(data, 0, packet, PACKET_HEADER_SIZE, data.Length);

            byte[] lengthPrefix = BitConverter.GetBytes(packet.Length);
            byte[] finalMessage = new byte[4 + packet.Length];

            Buffer.BlockCopy(lengthPrefix, 0, finalMessage, 0, 4);
            Buffer.BlockCopy(packet, 0, finalMessage, 4, packet.Length);

            return finalMessage;
        }

        private PacketResponse ReadPacket(byte[] data, IPEndPoint remoteEndPoint)
        {
            if (data == null || data.Length < PACKET_HEADER_SIZE) return null;
            int offset = 0;
            int messageId = BitConverter.ToInt32(data, offset); offset += 4;
            int senderId = BitConverter.ToInt32(data, offset); offset += 4;
            var action = BitConverter.ToUInt16(data, offset);

            var payload = new byte[data.Length - PACKET_HEADER_SIZE];
            Buffer.BlockCopy(data, PACKET_HEADER_SIZE, payload, 0, payload.Length);

            return new PacketResponse
            {
                messageId = messageId,
                senderId = senderId,
                action = action,
                data = payload,
                remoteEndPoint = remoteEndPoint,
                status = PacketResponse.ReceiveStatus.None, // Will be set by caller
                totalBytes = data.Length,
                processedBytes = data.Length
            };
        }
        #endregion

        #region Nested Classes & Enums

        [Serializable] public struct ConnectorInfo { public int id; public string ipAddress; public int port; public IPEndPoint remoteEndPoint; }

        [Serializable]
        public class PacketResponse
        {
            public enum ReceiveStatus { None, Receiving, Received }

            public int messageId;
            public int senderId;
            public ushort action;
            public byte[] data;
            public IPEndPoint remoteEndPoint;

            // Progress tracking fields
            public ReceiveStatus status = ReceiveStatus.None;
            public int totalBytes;
            public int processedBytes;
            public float progressPercentage => totalBytes > 0 ? (float)processedBytes / totalBytes : 0f;
            public bool isCompleted => status == ReceiveStatus.Received;
            public string operationId;
        }

        [Serializable]
        public class ErrorInfo
        {
            public enum ErrorType
            {
                Connection,        // Connection related errors
                DataTransmission,  // Send/Receive errors
                Protocol,          // Protocol/packet format errors
                Discovery,         // UDP discovery errors
                Serialization,     // JSON serialization errors
                General            // General/Unknown errors
            }

            public ErrorType errorType;
            public string message;
            public string exception;
            public ConnectorInfo connectorInfo;
            public System.DateTime timestamp;
            public string operationId; // Link to specific operation if applicable

            public ErrorInfo(ErrorType type, string msg, Exception ex = null, ConnectorInfo? connector = null, string opId = null)
            {
                errorType = type;
                message = msg;
                exception = ex?.ToString() ?? "";
                connectorInfo = connector ?? new ConnectorInfo();
                timestamp = System.DateTime.Now;
                operationId = opId ?? "";
            }
        }

        #endregion

        #region Event Handlers
        public IUniTaskAsyncEnumerable<PacketResponse> OnDataReceived(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<PacketResponse>(m_OnDataReceived, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnClientConnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnClientConnected, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnClientDisconnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnClientDisconnected, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnServerConnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnServerConnected, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnServerDisconnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnServerDisconnected, token);
        #endregion

        #region Error Reporting

        private void ReportError(ErrorInfo.ErrorType errorType, string message, Exception exception = null, ConnectorInfo? connectorInfo = null, string operationId = null)
        {
            var errorInfo = new ErrorInfo(errorType, message, exception, connectorInfo, operationId);

            if (m_IsDebug)
            {
                var prefix = m_IsServer ? "Server" : "Client";
                Debug.LogError($"[TCP-{prefix}] {errorType}: {message}");
                if (exception != null)
                {
                    Debug.LogException(exception);
                }
            }

            // Invoke error event on main thread
            UniTask.Create(async () =>
            {
                await UniTask.SwitchToMainThread();
                m_OnError?.Invoke(errorInfo);
            }).Forget();
        }

        #endregion
    }
}


#if UNITY_EDITOR
namespace Modules.Utilities
{
    [CustomEditor(typeof(TCPConnector))]
    public class TCPConnectorEditor : UnityEditor.Editor
    {
        // Test message variables
        private string testMessage = "Hello World!";
        private ushort testAction = 1;

        // Foldout states
        private bool eventsExpanded = false;
        private bool settingsExpanded = true;
        private bool dataTransmissionExpanded = false;

        public override void OnInspectorGUI()
        {

            var tcpConnector = (TCPConnector)target;
            if (tcpConnector == null) return;

            serializedObject.Update();

            var isServer = serializedObject.FindProperty("m_IsServer");

            // --- UI for Server/Client toggle ---
            EditorGUILayout.BeginHorizontal();
            GUI.color = isServer.boolValue ? Color.green : Color.gray;
            if (GUILayout.Button("Server")) { isServer.boolValue = true; }
            GUI.color = isServer.boolValue ? Color.gray : Color.green;
            if (GUILayout.Button("Client")) { isServer.boolValue = false; }
            EditorGUILayout.EndHorizontal();
            GUI.color = Color.white;
            EditorGUILayout.Space();

            // --- Draw properties based on mode ---
            var enableDiscovery = serializedObject.FindProperty("m_EnableDiscovery");

            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            settingsExpanded = EditorGUILayout.Foldout(settingsExpanded, "Settings", true);

            if (settingsExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_IsDebug"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_StartOnEnable"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Port"));


                EditorGUILayout.PropertyField(enableDiscovery);
                // Only show Host field if discovery is disabled
                if (!enableDiscovery.boolValue)
                {
                    if (!isServer.boolValue)
                     EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Host"));
                }
                else
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DiscoveryPort"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DiscoveryMessage"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DiscoveryInterval"));
                }



            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;

            dataTransmissionExpanded = EditorGUILayout.Foldout(dataTransmissionExpanded, "Data Transmission", true);

            if (dataTransmissionExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_MaxRetryCount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_RetryDelay"));
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();



            EditorGUILayout.Space();

            // --- Status Box ---
            if (Application.isPlaying)
            {
                var isRunningProp = serializedObject.FindProperty("m_IsRunning");
                var isConnectedProp = serializedObject.FindProperty("m_IsConnected");

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
                EditorGUILayout.Toggle("Is Running", isRunningProp.boolValue);
                if (!isServer.boolValue)
                {
                    EditorGUILayout.Toggle("Is Connected", isConnectedProp.boolValue);
                }

                if (isServer.boolValue && tcpConnector.ClientInfoList.Count > 0)
                {
                    EditorGUILayout.LabelField($"Connected Clients: {tcpConnector.ClientInfoList.Count}");

                    // Show client details
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Client Details:", EditorStyles.miniLabel);
                    foreach (var client in tcpConnector.ClientInfoList)
                    {
                        EditorGUILayout.LabelField($"• {client.ipAddress}:{client.port}", EditorStyles.miniLabel);
                    }
                }
                EditorGUILayout.EndVertical();

                // --- Test Message Box (only show when connected) ---
                bool canSendMessage = (isServer.boolValue && tcpConnector.ClientInfoList.Count > 0) ||
                                     (!isServer.boolValue && tcpConnector.IsConnected);

                if (canSendMessage)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField("Test Message", EditorStyles.boldLabel);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Action ID:", GUILayout.Width(70));
                    testAction = (ushort)EditorGUILayout.IntField(testAction, GUILayout.Width(60));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Message:", GUILayout.Width(70));
                    testMessage = EditorGUILayout.TextField(testMessage);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space();

                    EditorGUILayout.BeginHorizontal();
                    GUI.backgroundColor = Color.yellow;
                    if (GUILayout.Button("Send Text Message"))
                    {
                        tcpConnector.SendDataAsync(testAction, testMessage).Forget();
                        Debug.Log($"[TCPConnector] Sent test message: Action={testAction}, Message='{testMessage}'");
                    }

                    if (GUILayout.Button("Send Raw Bytes"))
                    {
                        byte[] rawData = System.Text.Encoding.UTF8.GetBytes(testMessage);
                        tcpConnector.SendDataAsync(testAction, rawData).Forget();
                        Debug.Log($"[TCPConnector] Sent raw bytes: Action={testAction}, Bytes={rawData.Length}");
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space();


                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.Space();

                // --- Actions Box ---
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
                if (!isRunningProp.boolValue)
                {
                    GUI.backgroundColor = Color.cyan;
                    if (GUILayout.Button("Start Connection")) { tcpConnector.StartConnection().Forget(); }
                }
                else
                {
                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("Stop Connection")) { tcpConnector.StopConnection(); }
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();
            // --- Events Box ---
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;

            eventsExpanded = EditorGUILayout.Foldout(eventsExpanded, "Events", true);

            if (eventsExpanded)
            {
                if (isServer.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnClientConnected"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnClientDisconnected"));
                }
                else
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnServerConnected"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnServerDisconnected"));
                }
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnDataReceived"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnError"));
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif