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

        [SerializeField, Range(1000, 10000), Tooltip("Buffer size for network operations")]
        public int m_BufferSize = 8192;
        [SerializeField, Range(1, 100), Tooltip("Maximum concurrent connections for server")]
        public int m_MaxConcurrentConnections = 50;
        [SerializeField, Tooltip("Enable TCP keep-alive to detect dead connections")]
        public bool m_EnableKeepAlive = true;

        [Header("Real-time Mode")]
        [SerializeField, Tooltip("Enable real-time mode for low-latency data transmission (unreliable but fast)")]
        public bool m_EnableRealTimeMode = false;
        [SerializeField, Range(7000, 9999), Tooltip("Real-time communication port")]
        public int m_RealTimePort = 7779;
        [SerializeField, Range(1, 1000), Tooltip("Maximum real-time packet size in bytes")]
        public int m_MaxRealTimePacketSize = 512;

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
        [Header("Status (Read Only)")]
        [SerializeField] private bool m_IsRunning = false;
        [SerializeField] private bool m_IsConnected = false;

        // --- TCP Components ---
        private TcpListener _tcpListener; // Server
        private TcpClient _tcpClient;     // Client
        private readonly Dictionary<TcpClient, ConnectorInfo> m_Clients = new Dictionary<TcpClient, ConnectorInfo>();

        // --- Real-time Components ---
        private UdpClient _realTimeServer; // Server real-time listener
        private UdpClient _realTimeClient; // Client real-time sender
        private readonly Dictionary<IPEndPoint, ConnectorInfo> m_RealTimeClients = new Dictionary<IPEndPoint, ConnectorInfo>();

        [Header("Connected Server Info")]
        [SerializeField] public ConnectorInfo m_ServerInfo;

        // --- Events ---
        [Header("Events")]
        [SerializeField] public UnityEvent<ConnectorInfo> m_OnClientConnected = new UnityEvent<ConnectorInfo>();
        [SerializeField] public UnityEvent<ConnectorInfo> m_OnClientDisconnected = new UnityEvent<ConnectorInfo>();
        [SerializeField] public UnityEvent<ConnectorInfo> m_OnServerConnected = new UnityEvent<ConnectorInfo>();
        [SerializeField] public UnityEvent<ConnectorInfo> m_OnServerDisconnected = new UnityEvent<ConnectorInfo>();
        [SerializeField] public UnityEvent<PacketResponse> m_OnDataReceived = new UnityEvent<PacketResponse>();
        [SerializeField] public UnityEvent<PacketResponse> m_OnRealTimeDataReceived = new UnityEvent<PacketResponse>(); // Separate real-time event
        [SerializeField] public UnityEvent<ErrorInfo> m_OnError = new UnityEvent<ErrorInfo>();

        private CancellationTokenSource _cts;

        // --- Performance Optimizations ---
        private readonly Queue<byte[]> _bufferPool = new Queue<byte[]>();
        private readonly object _bufferPoolLock = new object();

        // --- Packet Structure ---
        private const int PACKET_HEADER_SIZE = 10; // 4-id, 4-sender, 2-type

        #endregion

        #region Public Variables
        public bool IsConnected => m_IsConnected;
        public bool IsRunning => m_IsRunning;
        public bool IsRealTimeEnabled => m_EnableRealTimeMode;
        public List<ConnectorInfo> ClientInfoList => m_Clients.Values.ToList();
        public List<ConnectorInfo> RealTimeClientInfoList => m_RealTimeClients.Values.ToList();
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
                    return m_Clients.Keys.Any(client => client != null && IsClientConnected(client));
                }
            }
            else
            {
                return m_IsConnected && _tcpClient != null && IsClientConnected(_tcpClient);
            }
        }

        /// <summary>
        /// Checks if a TCP client is truly connected (not just the Connected property)
        /// </summary>
        private bool IsClientConnected(TcpClient client)
        {
            try
            {
                if (client?.Client == null) return false;

                // Use Socket.Poll to check if connection is still alive
                var socket = client.Client;
                bool part1 = socket.Poll(1000, SelectMode.SelectRead);
                bool part2 = (socket.Available == 0);

                // If poll returns true and no data available, connection is closed
                return !(part1 && part2) && socket.Connected;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a buffer from the pool or creates a new one
        /// </summary>
        private byte[] GetBuffer(int minSize)
        {
            lock (_bufferPoolLock)
            {
                while (_bufferPool.Count > 0)
                {
                    var buffer = _bufferPool.Dequeue();
                    if (buffer.Length >= minSize)
                    {
                        return buffer;
                    }
                }
            }

            // Create new buffer if none available or too small
            return new byte[Math.Max(minSize, m_BufferSize)];
        }

        /// <summary>
        /// Returns a buffer to the pool for reuse
        /// </summary>
        private void ReturnBuffer(byte[] buffer)
        {
            if (buffer == null || buffer.Length < m_BufferSize) return;

            lock (_bufferPoolLock)
            {
                // Limit pool size to prevent memory bloat
                if (_bufferPool.Count < 10)
                {
                    _bufferPool.Enqueue(buffer);
                }
            }
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
                
                // Start real-time server if enabled
                if (m_EnableRealTimeMode)
                {
                    StartRealTimeServerAsync(_cts.Token).Forget();
                }
                
                await StartServerAsync(_cts.Token);
            }
            else
            {
                m_IsRunning = true; // <<< Set IsRunning true for client here
                
                // Start real-time client if enabled
                if (m_EnableRealTimeMode)
                {
                    StartRealTimeClientAsync(_cts.Token).Forget();
                }
                
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
                _realTimeServer?.Close();
                _realTimeServer?.Dispose();
                _realTimeServer = null;
                
                lock (m_Clients)
                {
                    foreach (var client in m_Clients.Keys)
                    {
                        client.Close();
                    }
                    m_Clients.Clear();
                }
                
                lock (m_RealTimeClients)
                {
                    m_RealTimeClients.Clear();
                }
                
                Log("Server and all client connections closed.");
            }
            else
            {
                _tcpClient?.Close();
                _realTimeClient?.Close();
                _realTimeClient?.Dispose();
                _realTimeClient = null;
                
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
                _tcpListener.Start(m_MaxConcurrentConnections); // Limit backlog
                
                // Create server info for consistent ID mapping
                m_ServerInfo = new ConnectorInfo
                {
                    id = _tcpListener.GetHashCode(),
                    ipAddress = "0.0.0.0", // Server listens on all interfaces
                    port = m_Port,
                    remoteEndPoint = new IPEndPoint(IPAddress.Any, m_Port)
                };
                
                Log($"listening on TCP port {m_Port} (max {m_MaxConcurrentConnections} concurrent connections).");
                Log($"Server info created: ID={m_ServerInfo.id}, Port={m_ServerInfo.port}");

                while (!token.IsCancellationRequested)
                {
                    TcpClient connectedClient = await _tcpListener.AcceptTcpClientAsync().AsUniTask().AttachExternalCancellation(token);

                    // Configure TCP settings for performance
                    ConfigureTcpClient(connectedClient);

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

        /// <summary>
        /// Configures TCP client settings for optimal performance
        /// </summary>
        private void ConfigureTcpClient(TcpClient tcpClient)
        {
            try
            {
                var socket = tcpClient.Client;

                // Enable TCP Keep-Alive to detect dead connections
                if (m_EnableKeepAlive)
                {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    // Set keep-alive parameters (2 hours idle, 1 second interval, 9 probes)
                    var keepAliveValues = new byte[12];
                    BitConverter.GetBytes(1).CopyTo(keepAliveValues, 0); // Enable
                    BitConverter.GetBytes(7200000).CopyTo(keepAliveValues, 4); // 2 hours in ms
                    BitConverter.GetBytes(1000).CopyTo(keepAliveValues, 8); // 1 second in ms
                    socket.IOControl(IOControlCode.KeepAliveValues, keepAliveValues, null);
                }

                // Optimize buffer sizes
                socket.ReceiveBufferSize = m_BufferSize;
                socket.SendBufferSize = m_BufferSize;

                // Disable Nagle's algorithm for low latency (trade bandwidth for speed)
                socket.NoDelay = true;

                // Set linger option to close immediately
                socket.LingerState = new LingerOption(false, 0);

                Log($"TCP client configured: KeepAlive={m_EnableKeepAlive}, BufferSize={m_BufferSize}, NoDelay=true");
            }
            catch (Exception ex)
            {
                Log($"Failed to configure TCP client: {ex.Message}");
            }
        }

        #endregion

        #region Real-time Methods

        /// <summary>
        /// Starts real-time server for low-latency data transmission
        /// </summary>
        private async UniTask StartRealTimeServerAsync(CancellationToken token)
        {
            try
            {
                _realTimeServer = new UdpClient(m_RealTimePort);
                _realTimeServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                
                Log($"Real-time server listening on port {m_RealTimePort} for real-time data");

                while (!token.IsCancellationRequested)
                {
                    var result = await _realTimeServer.ReceiveAsync().AsUniTask().AttachExternalCancellation(token);
                    ProcessRealTimeDataAsync(result.Buffer, result.RemoteEndPoint, token).Forget();
                }
            }
            catch (OperationCanceledException)
            {
                Log("Real-time server operation was canceled.");
            }
            catch (ObjectDisposedException)
            {
                Log("Real-time server disposed.");
            }
            catch (Exception ex)
            {
                Log($"Real-time Server Error: {ex.GetType().Name} - {ex.Message}");
                ReportError(ErrorInfo.ErrorType.Connection, "Real-time server failed", ex);
            }
            finally
            {
                _realTimeServer?.Close();
                _realTimeServer?.Dispose();
                _realTimeServer = null;
            }
        }

        /// <summary>
        /// Starts real-time client for low-latency data transmission
        /// </summary>
        private async UniTask StartRealTimeClientAsync(CancellationToken token)
        {
            try
            {
                // Use a specific local port for real-time client communication
                int clientRealTimePort = m_RealTimePort + 100; // Offset to avoid conflicts
                _realTimeClient = new UdpClient(clientRealTimePort);
                _realTimeClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                
                Log($"Real-time client bound to port {clientRealTimePort} for communication with {m_Host}:{m_RealTimePort}");

                // Start listening for real-time data from server on the same port
                StartRealTimeClientReceiveLoopAsync(token).Forget();

                await UniTask.CompletedTask;
            }
            catch (Exception ex)
            {
                Log($"Real-time Client Error: {ex.GetType().Name} - {ex.Message}");
                ReportError(ErrorInfo.ErrorType.Connection, "Real-time client setup failed", ex);
            }
        }

        /// <summary>
        /// Real-time client receive loop to listen for server responses
        /// </summary>
        private async UniTask StartRealTimeClientReceiveLoopAsync(CancellationToken token)
        {
            try
            {
                Log($"Real-time client listening for server responses");

                while (!token.IsCancellationRequested && _realTimeClient != null)
                {
                    var result = await _realTimeClient.ReceiveAsync().AsUniTask().AttachExternalCancellation(token);
                    ProcessRealTimeDataAsync(result.Buffer, result.RemoteEndPoint, token).Forget();
                }
            }
            catch (OperationCanceledException)
            {
                Log("Real-time client receive loop was canceled.");
            }
            catch (ObjectDisposedException)
            {
                Log("Real-time client receive disposed.");
            }
            catch (Exception ex)
            {
                Log($"Real-time Client Receive Error: {ex.GetType().Name} - {ex.Message}");
                ReportError(ErrorInfo.ErrorType.Connection, "Real-time client receive failed", ex);
            }
        }

        /// <summary>
        /// Processes incoming real-time data
        /// </summary>
        private async UniTask ProcessRealTimeDataAsync(byte[] data, IPEndPoint remoteEndPoint, CancellationToken token)
        {
            try
            {
                Log($"Processing real-time data from {remoteEndPoint.Address}:{remoteEndPoint.Port} ({data.Length} bytes)");

                // Register real-time client if not already known
                lock (m_RealTimeClients)
                {
                    if (!m_RealTimeClients.ContainsKey(remoteEndPoint))
                    {
                        var clientInfo = new ConnectorInfo
                        {
                            id = remoteEndPoint.GetHashCode(),
                            ipAddress = remoteEndPoint.Address.ToString(),
                            port = remoteEndPoint.Port,
                            remoteEndPoint = remoteEndPoint
                        };
                        m_RealTimeClients[remoteEndPoint] = clientInfo;
                        Log($"New real-time client registered: {clientInfo.ipAddress}:{clientInfo.port}");
                    }
                }

                // Parse real-time packet (simpler than TCP - no length prefix)
                var packetResponse = ReadRealTimePacket(data, remoteEndPoint);
                if (packetResponse != null)
                {
                    packetResponse.status = PacketResponse.ReceiveStatus.Received;
                    packetResponse.totalBytes = data.Length;
                    packetResponse.processedBytes = data.Length;

                    Log($"Real-time packet parsed successfully: Action={packetResponse.action}, Data length={packetResponse.data?.Length ?? 0}");

                    await UniTask.SwitchToMainThread();
                    m_OnRealTimeDataReceived?.Invoke(packetResponse);
                }
                else
                {
                    Log($"Failed to parse real-time packet from {remoteEndPoint}");
                }
            }
            catch (Exception ex)
            {
                Log($"Real-time data processing error: {ex.Message}");
                ReportError(ErrorInfo.ErrorType.DataTransmission, "Real-time data processing failed", ex);
            }
        }

        /// <summary>
        /// Sends data via real-time transmission (unreliable but fast) - suitable for real-time updates
        /// </summary>
        /// <param name="action">Action identifier</param>
        /// <param name="data">Data to send (should be small, < 512 bytes recommended)</param>
        /// <param name="token">Cancellation token</param>
        public async UniTask SendRealTimeDataAsync(ushort action, byte[] data, CancellationToken token = default)
        {
            if (!m_EnableRealTimeMode)
            {
                Log("Real-time mode is not enabled");
                return;
            }

            if (!m_IsRunning) 
            {
                Log("Connection is not running - cannot send real-time data");
                return;
            }

            if (data != null && data.Length > m_MaxRealTimePacketSize)
            {
                Log($"Real-time data too large ({data.Length} bytes). Max allowed: {m_MaxRealTimePacketSize}. Use TCP for large data.");
                return;
            }

            try
            {
                byte[] packet = CreateRealTimePacket(action, data);
                Log($"Sending real-time packet: Action={action}, Data length={data?.Length ?? 0}, Packet size={packet.Length}");

                if (m_IsServer)
                {
                    // Server sends to all known real-time clients using their original endpoints
                    List<IPEndPoint> clients;
                    lock (m_RealTimeClients)
                    {
                        clients = m_RealTimeClients.Keys.ToList();
                    }

                    if (clients.Count == 0)
                    {
                        Log("No real-time clients to send data to");
                        return;
                    }

                    foreach (var client in clients)
                    {
                        // Send back to the same endpoint that sent to us
                        await _realTimeServer.SendAsync(packet, packet.Length, client);
                        Log($"Real-time packet sent to client {client.Address}:{client.Port}");
                    }

                    Log($"Real-time data sent to {clients.Count} clients ({packet.Length} bytes)");
                }
                else
                {
                    // Client sends to server
                    if (_realTimeClient != null)
                    {
                        await _realTimeClient.SendAsync(packet, packet.Length, m_Host, m_RealTimePort);
                        Log($"Real-time data sent to server {m_Host}:{m_RealTimePort} ({packet.Length} bytes)");
                    }
                    else
                    {
                        Log("Real-time client not initialized");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Real-time send error: {ex.Message}");
                ReportError(ErrorInfo.ErrorType.DataTransmission, "Real-time data send failed", ex);
            }
        }

        /// <summary>
        /// Sends serialized object via real-time transmission
        /// </summary>
        public async UniTask SendRealTimeDataAsync<T>(ushort action, T data, CancellationToken token = default)
        {
            if (data == null) throw new ArgumentNullException(nameof(data), "Data cannot be null.");

            byte[] serializedData;
            try
            {
                // Use the same intelligent serialization logic as TCP
                serializedData = SerializeData(data);
            }
            catch (Exception ex)
            {
                Log($"Real-time Serialization Error: {ex.Message}");
                ReportError(ErrorInfo.ErrorType.Serialization, "Real-time data serialization failed", ex);
                return;
            }

            await SendRealTimeDataAsync(action, serializedData, token);
        }

        /// <summary>
        /// Creates the core packet structure (shared between TCP and UDP)
        /// </summary>
        private byte[] CreateCorePacket(ushort action, byte[] data)
        {
            data ??= Array.Empty<byte>();
            byte[] packet = new byte[PACKET_HEADER_SIZE + data.Length];
            int offset = 0;

            // For consistent ID mapping between senderId and ConnectorInfo.id:
            // - Server uses its TcpListener's hash code (consistent server ID)
            // - Client uses its TcpClient's hash code
            int myId;
            if (m_IsServer)
            {
                myId = _tcpListener?.GetHashCode() ?? -1;
            }
            else
            {
                myId = _tcpClient?.GetHashCode() ?? _realTimeClient?.Client?.GetHashCode() ?? 0;
            }

            Buffer.BlockCopy(BitConverter.GetBytes(new System.Random().Next()), 0, packet, offset, 4); offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(myId), 0, packet, offset, 4); offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(action), 0, packet, offset, 2);

            Buffer.BlockCopy(data, 0, packet, PACKET_HEADER_SIZE, data.Length);

            return packet;
        }

        /// <summary>
        /// Reads the core packet structure (shared between TCP and UDP)
        /// </summary>
        private PacketResponse ReadCorePacket(byte[] data, int dataLength, IPEndPoint remoteEndPoint)
        {
            if (data == null || dataLength < PACKET_HEADER_SIZE) return null;
            
            int offset = 0;
            int messageId = BitConverter.ToInt32(data, offset); offset += 4;
            int senderId = BitConverter.ToInt32(data, offset); offset += 4;
            var action = BitConverter.ToUInt16(data, offset);

            int payloadLength = dataLength - PACKET_HEADER_SIZE;
            var payload = new byte[payloadLength];
            if (payloadLength > 0)
            {
                Buffer.BlockCopy(data, PACKET_HEADER_SIZE, payload, 0, payloadLength);
            }

            return new PacketResponse
            {
                messageId = messageId,
                senderId = senderId,
                action = action,
                data = payload,
                remoteEndPoint = remoteEndPoint,
                status = PacketResponse.ReceiveStatus.None,
                totalBytes = dataLength,
                processedBytes = dataLength
            };
        }

        /// <summary>
        /// Creates a simple real-time packet (without length prefix since UDP preserves boundaries)
        /// </summary>
        private byte[] CreateRealTimePacket(ushort action, byte[] data)
        {
            // Real-time packets don't need length prefix - just return the core packet
            return CreateCorePacket(action, data);
        }

        /// <summary>
        /// Reads real-time packet (simpler than TCP since UDP preserves message boundaries)
        /// </summary>
        private PacketResponse ReadRealTimePacket(byte[] data, IPEndPoint remoteEndPoint)
        {
            // Real-time packets don't have length prefix - read directly
            return ReadCorePacket(data, data?.Length ?? 0, remoteEndPoint);
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

                // Configure client before connecting
                _tcpClient.ReceiveBufferSize = m_BufferSize;
                _tcpClient.SendBufferSize = m_BufferSize;

                await _tcpClient.ConnectAsync(m_Host, m_Port).AsUniTask().AttachExternalCancellation(token);

                if (_tcpClient.Connected)
                {
                    // Configure TCP settings after connection
                    ConfigureTcpClient(_tcpClient);

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
                    if (messageLength <= 0 || messageLength > 1024 * 1024 * 16) // Increased max to 16MB
                    {
                        Log($"Invalid message length received: {messageLength}. Disconnecting.");
                        ReportError(ErrorInfo.ErrorType.Protocol, $"Invalid message length: {messageLength}", null, connectorInfo);
                        break;
                    }

                    // Use buffer pool for large messages
                    var dataBuffer = GetBuffer(messageLength);
                    string operationId = Guid.NewGuid().ToString();

                    try
                    {
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
                            int remainingBytes = messageLength - totalBytesRead;
                            int bufferSize = Math.Min(m_BufferSize, remainingBytes);

                            bytesRead = await stream.ReadAsync(dataBuffer, totalBytesRead, bufferSize, token);
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
                                m_OnDataReceived?.Invoke(packetResponse);
                            }

                            // Only yield for large messages to reduce overhead
                            if (messageLength > m_BufferSize)
                            {
                                await UniTask.Yield();
                            }
                        }

                        // Parse the complete packet and update status
                        var finalPacketResponse = ReadPacket(dataBuffer, messageLength, connectorInfo.remoteEndPoint);
                        if (finalPacketResponse != null)
                        {
                            // Copy progress information to final response
                            finalPacketResponse.status = PacketResponse.ReceiveStatus.Received;
                            finalPacketResponse.totalBytes = messageLength;
                            finalPacketResponse.processedBytes = totalBytesRead;
                            finalPacketResponse.operationId = operationId;

                            await UniTask.SwitchToMainThread();
                            Log($"Received complete packet: {finalPacketResponse.action} from {connectorInfo.ipAddress}:{connectorInfo.port} ({messageLength} bytes)");
                            m_OnDataReceived?.Invoke(finalPacketResponse);
                        }
                    }
                    finally
                    {
                        // Return buffer to pool
                        ReturnBuffer(dataBuffer);
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
                    ReportError(ErrorInfo.ErrorType.DataTransmission, $"Data receive failed: {ex.Message}", ex, connectorInfo);
                    break;
                }
            }
        }
        public async UniTask SendDataAsync(ushort action, byte[] data, CancellationToken token = default)
        {
            await SendDataAsync(action, data, (IProgress<float>)null, token);
        }

        /// <summary>
        /// Sends data with automatic retry logic. Will attempt to send up to MaxRetryCount times
        /// with RetryDelaySeconds delay between attempts if sending fails.
        /// 
        /// For servers: Sends to all connected clients.
        /// For clients: Sends to server only.
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
                        await SendAsServerAsync(message, progress, token);
                    }
                    else
                    {
                        // Client always sends to server
                        if (m_IsConnected && _tcpClient != null && _tcpClient.Connected)
                        {
                            await SendDataToStreamAsync(_tcpClient.GetStream(), message, progress, token);
                        }
                        else
                        {
                            throw new InvalidOperationException("Client is not connected");
                        }
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
        /// Handles server-side message sending to all connected clients
        /// </summary>
        private async UniTask SendAsServerAsync(byte[] message, IProgress<float> progress, CancellationToken token)
        {
            await SendAsServerAsync(message, null, progress, token);
        }

        /// <summary>
        /// Handles server-side message sending to specific clients or all clients
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="targetClientIds">Target client IDs. If null, sends to all clients</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="token">Cancellation token</param>
        private async UniTask SendAsServerAsync(byte[] message, int[] targetClientIds, IProgress<float> progress, CancellationToken token)
        {
            List<TcpClient> clientsToSend;

            lock (m_Clients)
            {
                if (targetClientIds == null || targetClientIds.Length == 0)
                {
                    // Send to all connected clients
                    clientsToSend = m_Clients.Keys.Where(c => c.Connected).ToList();
                }
                else
                {
                    // Send to specific clients
                    clientsToSend = m_Clients.Where(kvp =>
                        kvp.Key.Connected &&
                        targetClientIds.Contains(kvp.Value.id))
                        .Select(kvp => kvp.Key)
                        .ToList();
                }
            }

            if (clientsToSend.Count == 0)
            {
                string targetInfo = targetClientIds == null ? "all clients" : $"targeted clients [{string.Join(", ", targetClientIds)}]";
                Log($"No connected clients to send data to ({targetInfo})");
                return;
            }

            // Send to selected clients
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
                string targetInfo = targetClientIds == null ? $"{clientsToSend.Count} connected clients" : $"targeted clients [{string.Join(", ", targetClientIds)}]";
                throw new InvalidOperationException($"Failed to send data to any of {targetInfo}");
            }
            else if (successfulSends < results.Length)
            {
                string targetInfo = targetClientIds == null ? "clients" : $"targeted clients [{string.Join(", ", targetClientIds)}]";
                Log($"Data sent successfully to {successfulSends}/{results.Length} {targetInfo}");
            }
            else
            {
                string targetInfo = targetClientIds == null ? "all clients" : $"targeted clients [{string.Join(", ", targetClientIds)}]";
                Log($"Data sent successfully to {successfulSends} {targetInfo}");
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

            // Use configured buffer size for optimal performance
            int chunkSize = Math.Min(m_BufferSize, message.Length);
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

                    // Only yield on larger messages to reduce overhead
                    if (message.Length > m_BufferSize)
                    {
                        await UniTask.Yield();
                    }
                }

                // Ensure all data is sent
                await stream.FlushAsync(token);
                Log($"Data sent successfully: {message.Length} bytes (chunks: {chunkSize})");
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
            await SendDataAsync(action, data, (IProgress<float>)null, token);
        }

        public async UniTask SendDataAsync<T>(ushort action, T data, IProgress<float> progress, CancellationToken token = default)
        {
            if (data == null) throw new ArgumentNullException(nameof(data), "Data cannot be null.");
            if (!m_IsRunning) return;

            byte[] serializedData;
            try
            {
                // Handle primitive types and basic types directly
                serializedData = SerializeData(data);
            }
            catch (Exception ex)
            {
                Log($"Serialization Error: {ex.Message}");
                ReportError(ErrorInfo.ErrorType.Serialization, "Data serialization failed", ex);
                return;
            }

            await SendDataAsync(action, serializedData, progress, token);
        }

        /// <summary>
        /// Serializes data based on type - primitives directly, complex objects via JSON
        /// </summary>
        private byte[] SerializeData<T>(T data)
        {
            try
            {
                byte[] bytes = UnityConverter.ToBytes(data);
                Log($"Serializing {typeof(T).Name}: {data} ({bytes.Length} bytes)");
                return bytes;
            }
            catch (Exception ex)
            {
                Log($"Serialization failed for {typeof(T).Name}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Serializes complex objects as JSON
        /// </summary>
        private byte[] SerializeAsJson<T>(T data)
        {
            string jsonString;
            
#if PACKAGE_NEWTONSOFT_JSON_INSTALLED
            jsonString = JsonConvert.SerializeObject(data);
#else
            jsonString = JsonUtility.ToJson(data);
#endif
            
            var bytes = Encoding.UTF8.GetBytes(jsonString);
            Log($"Serializing {typeof(T).Name} as JSON: '{jsonString}' ({bytes.Length} bytes)");
            return bytes;
        }

        /// <summary>
        /// Server-only: Sends data to specific clients by their IDs
        /// This method is only effective when called from a server instance
        /// </summary>
        /// <param name="action">Action identifier</param>
        /// <param name="data">Data to send</param>
        /// <param name="targetClientIds">Target client IDs</param>
        /// <param name="token">Cancellation token</param>
        public async UniTask SendDataToClientsAsync(ushort action, byte[] data, int[] targetClientIds, CancellationToken token = default)
        {
            await SendDataToClientsAsync(action, data, targetClientIds, null, token);
        }

        /// <summary>
        /// Server-only: Sends data to specific clients by their IDs with progress reporting
        /// This method is only effective when called from a server instance
        /// </summary>
        /// <param name="action">Action identifier</param>
        /// <param name="data">Data to send</param>
        /// <param name="targetClientIds">Target client IDs</param>
        /// <param name="progress">Optional progress reporter (0.0 to 1.0)</param>
        /// <param name="token">Cancellation token</param>
        public async UniTask SendDataToClientsAsync(ushort action, byte[] data, int[] targetClientIds, IProgress<float> progress, CancellationToken token = default)
        {
            if (!m_IsRunning) return;

            // Only servers can target specific clients
            if (!m_IsServer)
            {
                Log("SendDataToClientsAsync is only available for servers");
                return;
            }

            if (targetClientIds == null || targetClientIds.Length == 0)
            {
                Log("No target client IDs specified - use SendDataAsync for broadcasting to all clients");
                return;
            }

            // Server-side targeting logic
            byte[] message = CreatePacket(action, data);
            int retryCount = 0;
            bool success = false;

            while (retryCount <= m_MaxRetryCount && !success && !token.IsCancellationRequested)
            {
                try
                {
                    if (!IsConnectionHealthy())
                    {
                        throw new InvalidOperationException("Connection is not healthy for data transmission");
                    }

                    await SendAsServerAsync(message, targetClientIds, progress, token);
                    success = true;

                    if (retryCount > 0)
                    {
                        Log($"Data sent successfully to targeted clients after {retryCount} retries");
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Connection closed") ||
                                                           ex.Message.Contains("Stream disposed"))
                {
                    Log($"Send operation aborted due to connection closure");
                    break;
                }
                catch (OperationCanceledException)
                {
                    Log($"Send operation cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount > m_MaxRetryCount)
                    {
                        Log($"Send Data to targeted clients failed after {m_MaxRetryCount} retries: {ex.Message}");
                        ReportError(ErrorInfo.ErrorType.DataTransmission, $"Targeted data send failed after {m_MaxRetryCount} retries", ex);
                        break;
                    }
                    else
                    {
                        Log($"Send Data to targeted clients failed (attempt {retryCount}/{m_MaxRetryCount}), retrying in {m_RetryDelay}ms: {ex.Message}");
                        await UniTask.Delay(m_RetryDelay, cancellationToken: token);
                    }
                }
            }
        }

        /// <summary>
        /// Server-only: Sends serialized object to specific clients by their IDs
        /// This method is only effective when called from a server instance
        /// </summary>
        public async UniTask SendDataToClientsAsync<T>(ushort action, T data, int[] targetClientIds, CancellationToken token = default)
        {
            await SendDataToClientsAsync(action, data, targetClientIds, null, token);
        }

        /// <summary>
        /// Server-only: Sends serialized object to specific clients by their IDs with progress reporting
        /// This method is only effective when called from a server instance
        /// </summary>
        public async UniTask SendDataToClientsAsync<T>(ushort action, T data, int[] targetClientIds, IProgress<float> progress, CancellationToken token = default)
        {
            if (data == null) throw new ArgumentNullException(nameof(data), "Data cannot be null.");
            if (!m_IsRunning) return;

            // Only servers can target specific clients
            if (!m_IsServer)
            {
                Log("SendDataToClientsAsync is only available for servers");
                return;
            }

            if (targetClientIds == null || targetClientIds.Length == 0)
            {
                Log("No target client IDs specified - use SendDataAsync for broadcasting to all clients");
                return;
            }

            byte[] serializedData;
            try
            {
#if PACKAGE_NEWTONSOFT_JSON_INSTALLED
                serializedData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
#else
                serializedData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data));
#endif
            }
            catch (Exception ex)
            {
                Log($"Serialization Error: {ex.Message}");
                ReportError(ErrorInfo.ErrorType.Serialization, "Data serialization failed", ex);
                return;
            }

            await SendDataToClientsAsync(action, serializedData, targetClientIds, progress, token);
        }

        /// <summary>
        /// Creates a TCP packet with length prefix for stream protocol
        /// </summary>
        private byte[] CreatePacket(ushort action, byte[] data)
        {
            // Get the core packet structure
            byte[] packet = CreateCorePacket(action, data);

            // TCP needs length prefix for stream protocol
            byte[] lengthPrefix = BitConverter.GetBytes(packet.Length);
            byte[] finalMessage = new byte[4 + packet.Length];

            Buffer.BlockCopy(lengthPrefix, 0, finalMessage, 0, 4);
            Buffer.BlockCopy(packet, 0, finalMessage, 4, packet.Length);

            Log($"Created packet: Action={action}, Data={data?.Length ?? 0} bytes, Packet={packet.Length} bytes, Final={finalMessage.Length} bytes");
            return finalMessage;
        }

        /// <summary>
        /// Reads TCP packet (assumes length prefix has already been processed)
        /// </summary>
        private PacketResponse ReadPacket(byte[] data, int dataLength, IPEndPoint remoteEndPoint)
        {
            // TCP packets don't include the length prefix in the data buffer passed here
            // The length prefix is processed separately in the receive loop
            return ReadCorePacket(data, dataLength, remoteEndPoint);
        }
        #endregion

        #region Nested Classes & Enums

        /// <summary>
        /// Contains connection information for a client or server.
        /// The 'id' field is consistent with PacketResponse.senderId for proper identification:
        /// - For Server: id = TcpListener.GetHashCode()
        /// - For Client: id = TcpClient.GetHashCode()  
        /// - For Real-time: id = IPEndPoint.GetHashCode()
        /// </summary>
        [Serializable] public struct ConnectorInfo { public int id; public string ipAddress; public int port; public IPEndPoint remoteEndPoint; }

        [Serializable]
        public class PacketResponse
        {
            public enum ReceiveStatus { None, Receiving, Received }

            public int messageId;
            /// <summary>
            /// ID of the sender. This value matches ConnectorInfo.id for proper identification:
            /// - Server packets: senderId = TcpListener.GetHashCode() (matches server's ConnectorInfo.id)
            /// - Client packets: senderId = TcpClient.GetHashCode() (matches client's ConnectorInfo.id)
            /// </summary>
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

            public T GetData<T>()
            {
                if (data == null || data.Length == 0) return default;
                
                try
                {
                    // Use the intelligent deserialization from UnityConverter
                    return UnityConverter.FromBytes<T>(data);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to deserialize data to {typeof(T).Name}: {ex.Message}");
                    
                    // Fallback to JSON deserialization for complex objects
                    try
                    {
#if PACKAGE_NEWTONSOFT_JSON_INSTALLED
                        return JsonConvert.DeserializeObject<T>(UnityConverter.GetString(data));
#else
                        return JsonUtility.FromJson<T>(UnityConverter.GetString(data));
#endif
                    }
                    catch (Exception jsonEx)
                    {
                        Debug.LogError($"Fallback JSON deserialization also failed: {jsonEx.Message}");
                        return default;
                    }
                }
            }

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
        public IUniTaskAsyncEnumerable<PacketResponse> OnRealTimeDataReceived(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<PacketResponse>(m_OnRealTimeDataReceived, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnClientConnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnClientConnected, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnClientDisconnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnClientDisconnected, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnServerConnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnServerConnected, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnServerDisconnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnServerDisconnected, token);

        public IUniTaskAsyncEnumerable<ErrorInfo> OnError(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ErrorInfo>(m_OnError, token);

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
        private string targetClientIdsText = ""; // For comma-separated client IDs

        // Foldout states
        private bool eventsExpanded = false;
        private bool settingsExpanded = true;
        private bool performanceExpanded = false;
        private bool udpExpanded = false;

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

            // --- Performance Box ---
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;

            performanceExpanded = EditorGUILayout.Foldout(performanceExpanded, "Performance Settings", true);

            if (performanceExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_BufferSize"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_MaxConcurrentConnections"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_EnableKeepAlive"));
                
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_MaxRetryCount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_RetryDelay"));
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();

            // --- Real-time Mode Box ---
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;

            udpExpanded = EditorGUILayout.Foldout(udpExpanded, "Real-time Mode", true);

            if (udpExpanded)
            {
                var enableRealTime = serializedObject.FindProperty("m_EnableRealTimeMode");
                EditorGUILayout.PropertyField(enableRealTime);
                
                EditorGUI.BeginDisabledGroup(!enableRealTime.boolValue);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_RealTimePort"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_MaxRealTimePacketSize"));
                EditorGUI.EndDisabledGroup();
                
                if (enableRealTime.boolValue)
                {
                    EditorGUILayout.HelpBox("Real-time mode provides low-latency communication but packets may be lost. Recommended for frequent updates like player positions.", MessageType.Info);
                }
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
                EditorGUILayout.Toggle("Real-time Enabled", tcpConnector.IsRealTimeEnabled);
                if (!isServer.boolValue)
                {
                    EditorGUILayout.Toggle("Is Connected", isConnectedProp.boolValue);
                }

                if (isServer.boolValue && tcpConnector.ClientInfoList.Count > 0)
                {
                    EditorGUILayout.LabelField($"TCP Clients: {tcpConnector.ClientInfoList.Count}");
                    EditorGUILayout.LabelField($"Real-time Clients: {tcpConnector.RealTimeClientInfoList.Count}");

                    // Show client details
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("TCP Client Details:", EditorStyles.miniLabel);
                    foreach (var client in tcpConnector.ClientInfoList)
                    {
                        EditorGUILayout.LabelField($"• TCP {client.id}: {client.ipAddress}:{client.port}", EditorStyles.miniLabel);
                    }
                    
                    if (tcpConnector.RealTimeClientInfoList.Count > 0)
                    {
                        EditorGUILayout.LabelField("Real-time Client Details:", EditorStyles.miniLabel);
                        foreach (var client in tcpConnector.RealTimeClientInfoList)
                        {
                            EditorGUILayout.LabelField($"• Real-time {client.id}: {client.ipAddress}:{client.port}", EditorStyles.miniLabel);
                        }
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
                    if (GUILayout.Button("Send TCP Message"))
                    {
                        tcpConnector.SendDataAsync(testAction, testMessage).Forget();
                        Debug.Log($"[TCPConnector] Sent TCP test message: Action={testAction}, Message='{testMessage}'");
                    }

                    // Real-time Test Button
                    EditorGUI.BeginDisabledGroup(!tcpConnector.IsRealTimeEnabled);
                    GUI.backgroundColor = Color.cyan;
                    if (GUILayout.Button("Send Real-time Message"))
                    {
                        tcpConnector.SendRealTimeDataAsync(testAction, testMessage).Forget();
                        Debug.Log($"[TCPConnector] Sent Real-time test message: Action={testAction}, Message='{testMessage}'");
                    }
                    EditorGUI.EndDisabledGroup();

                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button("Send Raw Bytes"))
                    {
                        byte[] rawData = System.Text.Encoding.UTF8.GetBytes(testMessage);
                        tcpConnector.SendDataAsync(testAction, rawData).Forget();
                        Debug.Log($"[TCPConnector] Sent raw bytes: Action={testAction}, Bytes={rawData.Length}");
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();

                    // Server-only targeting section
                    if (isServer.boolValue && tcpConnector.ClientInfoList.Count > 0)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Target Specific Clients (Server Only)", EditorStyles.boldLabel);

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Client IDs:", GUILayout.Width(70));
                        targetClientIdsText = EditorGUILayout.TextField(targetClientIdsText, GUILayout.ExpandWidth(true));
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.LabelField("Available clients:", EditorStyles.miniLabel);
                        foreach (var client in tcpConnector.ClientInfoList)
                        {
                            EditorGUILayout.LabelField($"• ID: {client.id} ({client.ipAddress}:{client.port})", EditorStyles.miniLabel);
                        }

                        EditorGUILayout.BeginHorizontal();
                        GUI.backgroundColor = Color.magenta;
                        if (GUILayout.Button("Send to Targeted Clients"))
                        {
                            try
                            {
                                int[] targetIds = targetClientIdsText.Split(',')
                                    .Where(s => !string.IsNullOrWhiteSpace(s))
                                    .Select(s => int.Parse(s.Trim()))
                                    .ToArray();

                                if (targetIds.Length > 0)
                                {
                                    tcpConnector.SendDataToClientsAsync(testAction, testMessage, targetIds).Forget();
                                    Debug.Log($"[TCPConnector] Sent targeted message: Action={testAction}, Targets=[{string.Join(", ", targetIds)}], Message='{testMessage}'");
                                }
                                else
                                {
                                    Debug.LogWarning("[TCPConnector] No valid client IDs specified");
                                }
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"[TCPConnector] Failed to parse client IDs: {ex.Message}");
                            }
                        }
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();
                    }

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
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnRealTimeDataReceived"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnError"));
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif