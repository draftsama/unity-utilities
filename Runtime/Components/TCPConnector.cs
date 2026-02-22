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
using Newtonsoft.Json;

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
        [SerializeField, Tooltip("IP address or hostname to connect to (for client) or bind to (for server)")]
        public string m_Host = "127.0.0.1";
        [Header("Network Behavior")]

        [SerializeField, Tooltip("TCP port number for main connection")]
        public int m_Port = 54321;
        [SerializeField, Tooltip("Enable server mode (listen for connections) or client mode (connect to server)")]
        public bool m_IsServer = false;

        [SerializeField, Tooltip("Automatically start connection when component is enabled")]
        public bool m_StartOnEnable = true;
        [SerializeField, Tooltip("Enable debug logging in console")]
        public bool m_IsDebug = true;

        [SerializeField, Tooltip("Enable logging for data sending operations")]
        public bool m_LogSend = true;
        [SerializeField, Tooltip("Enable logging for data receiving operations")]
        public bool m_LogReceive = true;


        [SerializeField, Range(1000, 10000), Tooltip("Buffer size for network operations")]
        public int m_BufferSize = 8192;
        [SerializeField, Range(1, 100), Tooltip("Maximum concurrent connections for server")]
        public int m_MaxConcurrentConnections = 8;
        [SerializeField, Range(5000, 120000), Tooltip("Send operation timeout in milliseconds (default 30s)")]
        public int m_SendTimeout = 30000;
        [SerializeField, Tooltip("Enable TCP keep-alive to detect dead connections")]
        public bool m_EnableKeepAlive = true;

        [Header("Retry Settings")]
        [SerializeField, Range(0, 10), Tooltip("Maximum retry attempts")]
        public int m_MaxRetryCount = 3;
        [SerializeField, Range(0, 3000), Tooltip("Delay between retries (ms)")]
        public int m_RetryDelay = 1000;

        [Header("Connection Health")]
        [SerializeField, Tooltip("Enable heartbeat monitoring to detect dead connections")]
        public bool m_EnableHeartbeat = true;
        [SerializeField, Range(5000, 60000), Tooltip("Interval between heartbeat pings (milliseconds)")]
        public int m_HeartbeatInterval = 30000; // 30 seconds
        [SerializeField, Range(10000, 180000), Tooltip("Connection timeout if no heartbeat response (milliseconds)")]
        public int m_HeartbeatTimeout = 90000; // 90 seconds

        [Header("Auto Reconnection (Client Mode)")]
        [SerializeField, Tooltip("Enable automatic reconnection when connection is lost (client mode only)")]
        public bool m_EnableAutoReconnect = true;
        [SerializeField, Range(1, 20), Tooltip("Maximum reconnection attempts (0 = infinite)")]
        public int m_ReconnectMaxAttempts = 5;
        [SerializeField, Tooltip("Reconnection delay pattern in milliseconds (exponential backoff)")]
        public int[] m_ReconnectDelays = new int[] { 1000, 2000, 5000, 10000, 30000 }; // 1s, 2s, 5s, 10s, 30s

        [Header("Security (Encryption)")]
        [SerializeField, Tooltip("Enable AES-256 encryption for all data transmission")]
        public bool m_EnableEncryption = false;
        [SerializeField, Tooltip("Pre-shared encryption key (32 bytes for AES-256). Leave empty for auto-generation.")]
        public string m_EncryptionKey = ""; // Hex string or base64

        [Header("Security (Authentication)")]
        [SerializeField, Tooltip("Enable authentication (server validates connecting clients)")]
        public bool m_EnableAuthentication = false;
        [SerializeField, Tooltip("Server authentication password (clients must provide this to connect)")]
        public string m_AuthPassword = "";
        [SerializeField, Range(3000, 30000), Tooltip("Authentication timeout in milliseconds")]
        public int m_AuthTimeout = 10000; // 10 seconds

        [Header("Connection Metrics")]
        [SerializeField, Tooltip("Enable connection metrics tracking (bandwidth, messages, uptime)")]
        public bool m_EnableMetrics = true;
        [SerializeField, Range(1000, 60000), Tooltip("Interval for metrics reporting events (milliseconds)")]
        public int m_MetricsReportInterval = 5000; // 5 seconds

        [Header("Rate Limiting")]
        [SerializeField, Tooltip("Enable rate limiting to prevent message spam (server-side)")]
        public bool m_EnableRateLimit = true;
        [SerializeField, Range(1, 1000), Tooltip("Maximum messages per second per client")]
        public int m_RateLimit = 100; // messages per second
        [SerializeField, Range(1, 500), Tooltip("Burst size (max messages in single burst)")]
        public int m_BurstSize = 50; // allow short bursts

        [Header("Message Priority Queues")]
        [SerializeField, Tooltip("Enable priority-based message queuing (high priority messages sent first)")]
        public bool m_EnablePriorityQueue = false;
        [SerializeField, Range(10, 1000), Tooltip("Maximum messages in queue per priority level")]
        public int m_QueueMaxSize = 100;

        [Header("Graceful Shutdown")]
        [SerializeField, Tooltip("Enable graceful shutdown (notify connections before closing)")]
        public bool m_EnableGracefulShutdown = true;
        [SerializeField, Range(1000, 10000), Tooltip("Maximum time to wait for shutdown completion (milliseconds)")]
        public int m_ShutdownTimeout = 3000; // 3 seconds

        [Header("Protocol Version Checking")]
        [SerializeField, Tooltip("Enable protocol version checking (reject incompatible versions)")]
        public bool m_EnableVersionCheck = true;
        [SerializeField, Tooltip("Protocol version (Major.Minor format, e.g., 2.5)")]
        public string m_ProtocolVersion = "2.5";

        [Header("Auto Discovery (UDP Broadcast)")]
        [SerializeField, Tooltip("Enable automatic server discovery via UDP broadcast")]
        public bool m_EnableDiscovery = true;

        [SerializeField, Range(0, 5000), Tooltip("Interval between discovery broadcasts (milliseconds)")]
        public int m_DiscoveryInterval = 1000; // milliseconds
        [SerializeField, Tooltip("UDP port for server discovery broadcasts")]
        public int m_DiscoveryPort = 54322;
        [SerializeField, Tooltip("Message string sent during server discovery")]
        public string m_DiscoveryMessage = "DiscoverServer";

        // --- Status ---
        [Header("Status (Read Only)")]
        [SerializeField] private bool m_IsRunning = false;
        [SerializeField] private bool m_IsConnected = false;

        // --- TCP Components ---
        private TcpListener _tcpListener; // Server
        private TcpClient _tcpClient;     // Client
        private readonly Dictionary<TcpClient, ConnectorInfo> m_Clients = new Dictionary<TcpClient, ConnectorInfo>();

        // --- Handshake State Tracking ---
        private readonly Dictionary<TcpClient, ConnectionState> _pendingConnections = new Dictionary<TcpClient, ConnectionState>();
        private readonly Dictionary<string, UniTaskCompletionSource<PacketResponse>> _handshakeWaiters = new Dictionary<string, UniTaskCompletionSource<PacketResponse>>();
        private readonly object _handshakeLock = new object();

        [Header("Connected Server Info")]
        [SerializeField] public ConnectorInfo m_ServerInfo;


        // --- Events ---
        [Header("Events")]
        [SerializeField] public UnityEvent<ConnectorInfo> m_OnClientConnected = new UnityEvent<ConnectorInfo>();
        [SerializeField] public UnityEvent<ConnectorInfo> m_OnClientDisconnected = new UnityEvent<ConnectorInfo>();
        [SerializeField] public UnityEvent<ConnectorInfo> m_OnServerConnected = new UnityEvent<ConnectorInfo>();
        [SerializeField] public UnityEvent<ConnectorInfo> m_OnServerDisconnected = new UnityEvent<ConnectorInfo>();
        [SerializeField] public UnityEvent<PacketResponse> m_OnDataReceived = new UnityEvent<PacketResponse>();
        [SerializeField] public UnityEvent<ErrorInfo> m_OnError = new UnityEvent<ErrorInfo>();
        [SerializeField] public UnityEvent<List<ConnectorInfo>> m_OnClientListUpdated = new UnityEvent<List<ConnectorInfo>>();
        [SerializeField] public UnityEvent<int> m_OnReconnecting = new UnityEvent<int>(); // Parameter: attempt number
        [SerializeField] public UnityEvent<ConnectorInfo> m_OnReconnected = new UnityEvent<ConnectorInfo>();
        [SerializeField] public UnityEvent m_OnReconnectFailed = new UnityEvent(); // All attempts exhausted
        [SerializeField] public UnityEvent<ConnectorInfo> m_OnAuthSuccess = new UnityEvent<ConnectorInfo>(); // Authentication succeeded
        [SerializeField] public UnityEvent<ConnectorInfo, string> m_OnAuthFailed = new UnityEvent<ConnectorInfo, string>(); // Authentication failed (client, reason)
        [SerializeField] public UnityEvent<ConnectionMetrics> m_OnMetricsUpdated = new UnityEvent<ConnectionMetrics>(); // Periodic metrics report
        [SerializeField] public UnityEvent<ConnectorInfo, int> m_OnRateLimitExceeded = new UnityEvent<ConnectorInfo, int>(); // (client, message count)
        [SerializeField] public UnityEvent<ConnectorInfo, string, string> m_OnVersionMismatch = new UnityEvent<ConnectorInfo, string, string>(); // (connector, localVersion, remoteVersion)

        private CancellationTokenSource _cts;

        // --- Performance Optimizations ---
        private readonly Queue<byte[]> _bufferPool = new Queue<byte[]>();
        private readonly object _bufferPoolLock = new object();

        // Cache component name for thread-safe logging
        private string _cachedName;

        // Optimize memory allocation
        private const int MAX_BUFFER_POOL_SIZE = 50; // Increased from 20 to 50 for better performance
        private const int BUFFER_REUSE_THRESHOLD = 4096; // ใช้ buffer ซ้ำถ้า >= 4KB

        // Optimize progress reporting (per-instance)
        private long _lastProgressTicks = 0;

        // --- Packet Structure ---
        private const int PACKET_HEADER_SIZE = 6; // 4-messageId, 2-action

        // --- Reserved Action Numbers ---
        // ⚠️ WARNING: Actions 65525-65535 are RESERVED for internal protocol use.
        // User applications MUST NOT use these action numbers.
        // Valid user action range: 0-65522 or 0x0000-0xFFF2.
        private const ushort RESERVED_ACTION_MIN = 65523; 
        private const ushort RESERVED_ACTION_MAX = 65535; 

        // --- Handshake Protocol ---
        private const ushort ACTION_CONNECTION_HELLO = 65535;
        private const ushort ACTION_CONNECTION_ACK = 65534;
        private const ushort ACTION_CONNECTION_READY = 65533;
        private const int HANDSHAKE_TIMEOUT_MS = 5000; // 5 seconds

        // --- Client Sync Protocol ---
        private const ushort ACTION_CLIENT_LIST_FULL = 65530;
        private const ushort ACTION_CLIENT_JOINED = 65531;
        private const ushort ACTION_CLIENT_LEFT = 65532;
        private const ushort ACTION_RELAY_MESSAGE = 65529; // Client-to-client relay via server

        // --- Heartbeat Protocol ---
        private const ushort ACTION_HEARTBEAT_PING = 65528;
        private const ushort ACTION_HEARTBEAT_PONG = 65527;

        // --- Authentication Protocol ---
        private const ushort ACTION_AUTH_REQUEST = 65526; // Server → Client: Request authentication
        private const ushort ACTION_AUTH_RESPONSE = 65525; // Client → Server: Send credentials

        // --- Graceful Disconnect Protocol ---
        private const ushort ACTION_DISCONNECT_NOTIFICATION = 65524; // Graceful disconnect notification

        // --- Protocol Version Checking ---
        private const ushort ACTION_VERSION_REQUEST = 65523; // Server → Client: Request protocol version
        private const ushort ACTION_VERSION_RESPONSE = 65522; // Client → Server: Send protocol version

        // --- Client List Tracking ---
        private List<ConnectorInfo> m_SyncedClientList = new List<ConnectorInfo>();
        private readonly object _syncedClientListLock = new object();
        private int m_MyClientId = -1; // Client's own server-assigned ID (received from server sync)

        // --- Client ID Generation ---
        private static int _nextClientId = 0; // Thread-safe sequential ID counter

        // --- Connection Health Tracking ---
        private readonly Dictionary<TcpClient, long> _lastHeartbeatTicks = new Dictionary<TcpClient, long>();
        private readonly object _heartbeatLock = new object();
        private long _lastServerHeartbeatTicks = 0; // Client-side: track server heartbeat

        // --- Reconnection Tracking ---
        private int _reconnectAttempts = 0;
        private bool _isReconnecting = false;

        // --- Encryption ---
        private byte[] _encryptionKeyBytes;
        private System.Security.Cryptography.Aes _aesProvider;
        private readonly object _encryptionLock = new object();

        // --- Authentication ---
        private string _authPasswordHash; // SHA256 hash of password
        private readonly Dictionary<TcpClient, bool> _authenticatedClients = new Dictionary<TcpClient, bool>();

        // --- Connection Metrics ---
        private readonly Dictionary<TcpClient, ConnectionMetrics> _clientMetrics = new Dictionary<TcpClient, ConnectionMetrics>();
        private ConnectionMetrics _serverMetrics; // Client mode: tracks connection to server
        private readonly object _metricsLock = new object();

        // --- Rate Limiting ---
        private readonly Dictionary<TcpClient, TokenBucket> _rateLimiters = new Dictionary<TcpClient, TokenBucket>();
        private readonly object _rateLimitLock = new object();

        // --- Priority Message Queues ---
        private readonly Dictionary<MessagePriority, Queue<QueuedMessage>> _sendQueue = new Dictionary<MessagePriority, Queue<QueuedMessage>>();
        private readonly object _queueLock = new object();
        private bool _isProcessingQueue = false;

        #endregion

        #region Public Variables
        
        /// <summary>
        /// Maximum action number that can be used by user applications.
        /// Actions 0-65522 are available for user use.
        /// Actions 65523-65535 are reserved for internal protocol.
        /// </summary>
        public const ushort MaxUserAction = 65522;
        
        public bool IsConnected => m_IsConnected;
        public bool IsRunning => m_IsRunning;
        public int ConnectedClientCount => m_Clients.Count;
        public ConnectorInfo ServerInfo => m_ServerInfo;
        public int MyClientId => m_MyClientId; // Client's server-assigned ID (for self-filtering)

        /// <summary>
        /// Checks if an action number can be used by user applications.
        /// Returns false if the action is in the reserved range (65529-65535).
        /// </summary>
        public static bool IsValidUserAction(ushort action)
        {
            return action <= MaxUserAction;
        }

        /// <summary>
        /// Gets connection metrics for a specific client (server mode) or server connection (client mode).
        /// Returns null if metrics are disabled or client not found.
        /// </summary>
        public ConnectionMetrics GetConnectionMetrics(int clientId = -1)
        {
            if (!m_EnableMetrics) return null;

            lock (_metricsLock)
            {
                if (m_IsServer)
                {
                    // Server mode: find client by ID
                    foreach (var kvp in _clientMetrics)
                    {
                        if (kvp.Value.ClientId == clientId)
                        {
                            kvp.Value.UpdateRates();
                            return kvp.Value;
                        }
                    }
                    return null;
                }
                else
                {
                    // Client mode: return server connection metrics
                    if (_serverMetrics != null)
                    {
                        _serverMetrics.UpdateRates();
                        return _serverMetrics;
                    }
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets all connection metrics (server mode only).
        /// Returns empty list if metrics are disabled or in client mode.
        /// </summary>
        public List<ConnectionMetrics> GetAllConnectionMetrics()
        {
            if (!m_EnableMetrics || !m_IsServer) return new List<ConnectionMetrics>();

            lock (_metricsLock)
            {
                var result = new List<ConnectionMetrics>(_clientMetrics.Count);
                foreach (var metrics in _clientMetrics.Values)
                {
                    metrics.UpdateRates();
                    result.Add(metrics);
                }
                return result;
            }
        }



        /// <summary>
        /// Gets list of connected clients (creates new list each call - use sparingly)
        /// </summary>
        public List<ConnectorInfo> GetClientInfoList()
        {
            lock (m_Clients)
            {
                return m_Clients.Values.ToList();
            }
        }

        /// <summary>
        /// Gets the synchronized list of all connected clients.
        /// In server mode, returns current server's client list.
        /// In client mode, returns the synced list received from server.
        /// </summary>
        public List<ConnectorInfo> GetAllConnectedClientsInfo()
        {
            lock (_syncedClientListLock)
            {
                return new List<ConnectorInfo>(m_SyncedClientList);
            }
        }


        public bool TryGetClientInfoById(int id, out ConnectorInfo clientInfo)
        {
            lock (m_Clients)
            {
                foreach (var client in m_Clients.Values)
                {
                    if (client.id == id)
                    {
                        clientInfo = client;
                        return true;
                    }
                }
                clientInfo = default(ConnectorInfo);
                return false;
            }
        }

        /// <summary>
        /// Gets a buffer from the pool or creates a new one (Optimized)
        /// </summary>
        private byte[] GetBuffer(int minSize)
        {
            lock (_bufferPoolLock)
            {
                // Try to reuse existing buffer
                while (_bufferPool.Count > 0)
                {
                    var buffer = _bufferPool.Dequeue();
                    if (buffer.Length >= minSize)
                    {
                        return buffer;
                    }
                }
            }

            // Create new buffer only when needed - use exact size for small buffers
            int actualSize = minSize < BUFFER_REUSE_THRESHOLD ? minSize : Math.Max(minSize, m_BufferSize);
            return new byte[actualSize];
        }

        /// <summary>
        /// Returns a buffer to the pool for reuse (Optimized)
        /// </summary>
        private void ReturnBuffer(byte[] buffer)
        {
            if (buffer == null || buffer.Length < BUFFER_REUSE_THRESHOLD) return;

            lock (_bufferPoolLock)
            {
                // Limit pool size to prevent memory bloat
                if (_bufferPool.Count < MAX_BUFFER_POOL_SIZE)
                {
                    _bufferPool.Enqueue(buffer);
                }
            }
        }

        #endregion

        #region Unity Methods

        private void Awake()
        {
            // Cache the name on the main thread for thread-safe logging
            _cachedName = name;

            // Initialize encryption if enabled
            if (m_EnableEncryption)
            {
                InitializeEncryption();
            }

            // Initialize authentication if enabled
            if (m_EnableAuthentication)
            {
                InitializeAuthentication();
            }

            // Initialize priority queues if enabled
            if (m_EnablePriorityQueue)
            {
                InitializePriorityQueues();
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

            // Dispose encryption provider
            lock (_encryptionLock)
            {
                _aesProvider?.Dispose();
                _aesProvider = null;
            }
        }



        #endregion

        #region Core Logic

        private void Log(object message)
        {
            if (!m_IsDebug) return;
            var prefix = m_IsServer ? "Server" : "Client";
            var logMessage = $"[{prefix}][{_cachedName}] {message}";
            Debug.Log(logMessage);

        }

        /// <summary>
        /// Logs network information for debugging build vs editor differences
        /// </summary>

        public async UniTask StartConnection()
        {
            if (m_IsRunning)
            {
                Log("Connection is already running.");
                return;
            }


            Log("Starting connection...");

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;


            try
            {
                m_IsRunning = true;
                // For Trigger network stack initialization (important for macOS security permissions)
                Dns.GetHostEntry(Dns.GetHostName());


                if (m_IsServer)
                {
                    await StartServerAsync(_cts.Token);
                }
                else
                {
                    await StartClientAsync(token);
                }
            }
            catch (Exception ex)
            {
                Log($"Error starting connection: {ex.Message}");
                m_IsRunning = false;
                ReportError(ErrorInfo.ErrorType.Connection, $"Failed to start connection: {ex.Message}", ex);
            }
            finally
            {
                m_IsRunning = false;
            }
        }

        public void StopConnection()
        {
            Log("Stopping connection...");
            m_IsRunning = false;

            // --- Graceful Shutdown: Send disconnect notification ---
            if (m_EnableGracefulShutdown)
            {
                try
                {
                    byte[] disconnectPacket = CreatePacket(ACTION_DISCONNECT_NOTIFICATION, Array.Empty<byte>());
                    
                    if (m_IsServer)
                    {
                        // Server: Notify all connected clients
                        Log($"📢 Sending graceful disconnect notification to {m_Clients.Count} client(s)...");
                        lock (m_Clients)
                        {
                            var clientsArray = new TcpClient[m_Clients.Count];
                            m_Clients.Keys.CopyTo(clientsArray, 0);

                            foreach (var client in clientsArray)
                            {
                                try
                                {
                                    if (client?.Connected == true)
                                    {
                                        var stream = client.GetStream();
                                        stream.Write(disconnectPacket, 0, disconnectPacket.Length);
                                        stream.Flush();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log($"⚠️ Failed to send disconnect notification to client: {ex.Message}");
                                }
                            }
                        }
                    }
                    else
                    {
                        // Client: Notify server
                        if (_tcpClient?.Connected == true)
                        {
                            Log("📢 Sending graceful disconnect notification to server...");
                            try
                            {
                                var stream = _tcpClient.GetStream();
                                stream.Write(disconnectPacket, 0, disconnectPacket.Length);
                                stream.Flush();
                            }
                            catch (Exception ex)
                            {
                                Log($"⚠️ Failed to send disconnect notification to server: {ex.Message}");
                            }
                        }
                    }

                    // Wait for a short period to allow notification delivery
                    System.Threading.Thread.Sleep(Math.Min(m_ShutdownTimeout, 500)); // Cap at 500ms for safety
                    Log("✅ Graceful disconnect notification sent");
                }
                catch (Exception ex)
                {
                    Log($"⚠️ Error during graceful shutdown: {ex.Message}");
                }
            }

            // --- Clear Priority Queues ---
            if (m_EnablePriorityQueue)
            {
                lock (_queueLock)
                {
                    foreach (var queue in _sendQueue.Values)
                    {
                        queue.Clear();
                    }
                    _isProcessingQueue = false;
                }
                Log("🧹 Priority queues cleared");
            }

            _cts?.Cancel();

            // Stop TCP listener (server only)
            _tcpListener?.Stop();

            // Close TCP client (client only)
            _tcpClient?.Close();

            // Close all client connections (server only)
            lock (m_Clients)
            {
                // Create array to avoid ToList() allocation
                var clientsArray = new TcpClient[m_Clients.Count];
                m_Clients.Keys.CopyTo(clientsArray, 0);

                foreach (var client in clientsArray)
                {
                    client?.Close();
                }
                m_Clients.Clear();
            }

            // Handle connection state
            if (m_IsConnected)
            {
                m_IsConnected = false;
                m_OnServerDisconnected?.Invoke(m_ServerInfo);
            }

            // Dispose cancellation token
            _cts?.Dispose();
            _cts = null;

            Log("Connection stopped.");
        }

        #endregion

        #region Server Methods

        private async UniTask StartServerAsync(CancellationToken token)
        {
            try
            {
                // Check if we should still be running before starting server
                if (!m_IsRunning || token.IsCancellationRequested)
                {
                    Log("Server start canceled before initialization.");
                    return;
                }

                // Validate port range
                if (m_Port <= 0 || m_Port > 65535)
                {
                    Log($"Invalid port number: {m_Port}. Must be between 1-65535.");
                    ReportError(ErrorInfo.ErrorType.Connection, $"Invalid port number: {m_Port}", null);
                    return;
                }

                // Start UDP broadcast for discovery if enabled
                if (m_EnableDiscovery)
                {
                    BroadcastPresenceAsync(token).Forget();
                }

                _tcpListener = new TcpListener(IPAddress.Any, m_Port);

                try
                {
                    _tcpListener.Start(m_MaxConcurrentConnections);
                    Log($"✅ Server started on port {m_Port}");
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    Log($"❌ Port {m_Port} is already in use by another application");
                    ReportError(ErrorInfo.ErrorType.Connection, $"Port {m_Port} is already in use", ex);
                    return;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AccessDenied)
                {
                    Log($"❌ Access denied for port {m_Port}. May require administrator privileges");
                    ReportError(ErrorInfo.ErrorType.Connection, $"Access denied for port {m_Port}", ex);
                    return;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressNotAvailable)
                {
                    Log($"❌ Address not available for port {m_Port}. Check network configuration");
                    ReportError(ErrorInfo.ErrorType.Connection, $"Address not available for port {m_Port}", ex);
                    return;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.NetworkUnreachable)
                {
                    Log($"❌ Network unreachable. Check Windows network adapter settings");
                    ReportError(ErrorInfo.ErrorType.Connection, $"Network unreachable for port {m_Port}", ex);
                    return;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostUnreachable)
                {
                    Log($"❌ Host unreachable. Check Windows Firewall or network configuration");
                    ReportError(ErrorInfo.ErrorType.Connection, $"Host unreachable for port {m_Port}", ex);
                    return;
                }

                // Create server info for consistent ID mapping
                m_ServerInfo = new ConnectorInfo
                {
                    id = _tcpListener.GetHashCode(),
                    ipAddress = "0.0.0.0", // Server listens on all interfaces
                    port = m_Port,
                    remoteEndPoint = new IPEndPoint(IPAddress.Any, m_Port)
                };

                Log($"Server listening on TCP port {m_Port} (max {m_MaxConcurrentConnections} connections)");

                while (!token.IsCancellationRequested && m_IsRunning)
                {
                    TcpClient connectedClient = await _tcpListener.AcceptTcpClientAsync().ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();

                    // Check again after accepting client
                    if (!m_IsRunning || token.IsCancellationRequested)
                    {
                        connectedClient?.Close();
                        break;
                    }

                    // Configure TCP settings for performance
                    ConfigureTcpClient(connectedClient);

                    HandleClientAsync(connectedClient, token).Forget();
                }
            }
            catch (OperationCanceledException)
            {
                // Silent cancellation
            }
            catch (ObjectDisposedException)
            {
                // Silent disposal
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
            {
                // Silent abort
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
                id = System.Threading.Interlocked.Increment(ref _nextClientId),
                ipAddress = clientEndPoint.Address.ToString(),
                port = clientEndPoint.Port,
                remoteEndPoint = clientEndPoint
            };

            // Add to pending connections (not m_Clients yet)
            lock (_handshakeLock)
            {
                _pendingConnections[client] = ConnectionState.Pending;
            }

            Log($"✅ Client TCP connected: {clientInfo.ipAddress}:{clientInfo.port} (ID: {clientInfo.id})");

            var stream = client.GetStream();
            bool handshakeComplete = false;

            try
            {
                // --- Handshake Step 1: Send HELLO ---
                lock (_handshakeLock)
                {
                    _pendingConnections[client] = ConnectionState.HelloSent;
                }

                byte[] helloPacket = CreatePacket(ACTION_CONNECTION_HELLO, Array.Empty<byte>());
                await SendDataToStreamAsync(stream, helloPacket, null, token);

                // --- Handshake Step 2: Wait for ACK ---
                string ackKey = $"ack_{clientInfo.id}";
                UniTaskCompletionSource<PacketResponse> ackWaiter;
                lock (_handshakeLock)
                {
                    ackWaiter = new UniTaskCompletionSource<PacketResponse>();
                    _handshakeWaiters[ackKey] = ackWaiter;
                }

                // Start receive loop BEFORE waiting for ACK (critical for handshake)
                var receiveLoopTask = ReceiveDataLoopAsync(stream, clientInfo, token);

                using (var timeoutCts = new CancellationTokenSource(HANDSHAKE_TIMEOUT_MS))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token))
                {
                    try
                    {
                        await ackWaiter.Task.AttachExternalCancellation(linkedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        lock (_handshakeLock)
                        {
                            _handshakeWaiters.Remove(ackKey);
                        }

                        if (timeoutCts.IsCancellationRequested)
                        {
                            Log($"Handshake timeout: Client {clientInfo.ipAddress} did not send ACK");
                            return;
                        }
                        throw; // Re-throw if it's the main cancellation token
                    }
                }

                lock (_handshakeLock)
                {
                    _handshakeWaiters.Remove(ackKey);
                }

                lock (_handshakeLock)
                {
                    _pendingConnections[client] = ConnectionState.Acknowledged;
                }

                // --- Protocol Version Check (if enabled): Request and validate version ---
                if (m_EnableVersionCheck)
                {
                    // Send VERSION_REQUEST
                    byte[] versionRequestPacket = CreatePacket(ACTION_VERSION_REQUEST, Array.Empty<byte>());
                    await SendDataToStreamAsync(stream, versionRequestPacket, null, token);

                    // Wait for VERSION_RESPONSE
                    string versionKey = $"version_{clientInfo.id}";
                    UniTaskCompletionSource<PacketResponse> versionWaiter;
                    lock (_handshakeLock)
                    {
                        versionWaiter = new UniTaskCompletionSource<PacketResponse>();
                        _handshakeWaiters[versionKey] = versionWaiter;
                    }

                    using (var versionTimeoutCts = new CancellationTokenSource(5000)) // 5s timeout for version check
                    using (var linkedVersionCts = CancellationTokenSource.CreateLinkedTokenSource(token, versionTimeoutCts.Token))
                    {
                        try
                        {
                            var versionResponse = await versionWaiter.Task.AttachExternalCancellation(linkedVersionCts.Token);
                            
                            // Parse client's protocol version
                            string clientVersion = System.Text.Encoding.UTF8.GetString(versionResponse.data).Trim();
                            
                            // Validate version compatibility (simple string comparison for now)
                            if (clientVersion != m_ProtocolVersion)
                            {
                                Log($"❌ Protocol version mismatch: Server={m_ProtocolVersion}, Client={clientVersion}");
                                lock (m_Clients) { m_Clients.Remove(client); }
                                await SwitchToMainThreadWithRetry();
                                m_OnVersionMismatch?.Invoke(clientInfo, m_ProtocolVersion, clientVersion);
                                return;
                            }

                            Log($"✅ Protocol version validated: {clientVersion}");
                        }
                        catch (OperationCanceledException)
                        {
                            lock (_handshakeLock)
                            {
                                _handshakeWaiters.Remove(versionKey);
                            }

                            if (versionTimeoutCts.IsCancellationRequested)
                            {
                                Log($"❌ Version check timeout: Client {clientInfo.ipAddress} did not respond");
                                lock (m_Clients) { m_Clients.Remove(client); }
                                return;
                            }
                            throw;
                        }
                    }

                    lock (_handshakeLock)
                    {
                        _handshakeWaiters.Remove(versionKey);
                    }
                }

                // --- Authentication Step (if enabled): Request and validate credentials ---
                if (m_EnableAuthentication)
                {
                    // Send AUTH_REQUEST
                    byte[] authRequestPacket = CreatePacket(ACTION_AUTH_REQUEST, Array.Empty<byte>());
                    await SendDataToStreamAsync(stream, authRequestPacket, null, token);

                    // Wait for AUTH_RESPONSE
                    string authKey = $"auth_{clientInfo.id}";
                    UniTaskCompletionSource<PacketResponse> authWaiter;
                    lock (_handshakeLock)
                    {
                        authWaiter = new UniTaskCompletionSource<PacketResponse>();
                        _handshakeWaiters[authKey] = authWaiter;
                    }

                    using (var authTimeoutCts = new CancellationTokenSource(m_AuthTimeout))
                    using (var linkedAuthCts = CancellationTokenSource.CreateLinkedTokenSource(token, authTimeoutCts.Token))
                    {
                        try
                        {
                            var authResponse = await authWaiter.Task.AttachExternalCancellation(linkedAuthCts.Token);
                            
                            // Validate password
                            string providedPassword = System.Text.Encoding.UTF8.GetString(authResponse.data);
                            bool isValid = ValidateAuthentication(providedPassword);

                            if (!isValid)
                            {
                                Log($"❌ Authentication failed for {clientInfo.ipAddress}:{clientInfo.port} (invalid credentials)");
                                lock (m_Clients) { m_Clients.Remove(client); }
                                await SwitchToMainThreadWithRetry();
                                m_OnAuthFailed?.Invoke(clientInfo, "Invalid credentials");
                                return;
                            }

                            // Mark as authenticated
                            _authenticatedClients[client] = true;
                            Log($"✅ Client authenticated: {clientInfo.ipAddress}:{clientInfo.port}");
                            await SwitchToMainThreadWithRetry();
                            m_OnAuthSuccess?.Invoke(clientInfo);
                        }
                        catch (OperationCanceledException)
                        {
                            lock (_handshakeLock)
                            {
                                _handshakeWaiters.Remove(authKey);
                            }

                            if (authTimeoutCts.IsCancellationRequested)
                            {
                                Log($"❌ Authentication timeout: Client {clientInfo.ipAddress} did not respond");
                                lock (m_Clients) { m_Clients.Remove(client); }
                                await SwitchToMainThreadWithRetry();
                                m_OnAuthFailed?.Invoke(clientInfo, "Timeout");
                                return;
                            }
                            throw;
                        }
                    }

                    lock (_handshakeLock)
                    {
                        _handshakeWaiters.Remove(authKey);
                    }
                }

                // --- Handshake Step 3: Send client list sync ---
                // Temporarily add to m_Clients for BroadcastClientListSync
                lock (m_Clients) { m_Clients[client] = clientInfo; }
                await BroadcastClientListSync(clientInfo.id, isFullSync: true);

                // --- Handshake Step 4: Wait for READY ---
                string readyKey = $"ready_{clientInfo.id}";
                UniTaskCompletionSource<PacketResponse> readyWaiter;
                lock (_handshakeLock)
                {
                    readyWaiter = new UniTaskCompletionSource<PacketResponse>();
                    _handshakeWaiters[readyKey] = readyWaiter;
                }

                using (var timeoutCts = new CancellationTokenSource(HANDSHAKE_TIMEOUT_MS))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token))
                {
                    try
                    {
                        await readyWaiter.Task.AttachExternalCancellation(linkedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        lock (_handshakeLock)
                        {
                            _handshakeWaiters.Remove(readyKey);
                        }

                        if (timeoutCts.IsCancellationRequested)
                        {
                            Log($"Handshake timeout: Client {clientInfo.ipAddress} did not send READY");
                            lock (m_Clients) { m_Clients.Remove(client); }
                            return;
                        }
                        throw; // Re-throw if it's the main cancellation token
                    }
                }

                lock (_handshakeLock)
                {
                    _handshakeWaiters.Remove(readyKey);
                }

                lock (_handshakeLock)
                {
                    _pendingConnections[client] = ConnectionState.Ready;
                    _pendingConnections.Remove(client);
                }

                handshakeComplete = true;
                Log($"✅ Handshake complete: {clientInfo.ipAddress}:{clientInfo.port}");

                // Initialize metrics tracking
                if (m_EnableMetrics)
                {
                    lock (_metricsLock)
                    {
                        _clientMetrics[client] = new ConnectionMetrics(clientInfo.id);
                    }
                }

                // Initialize rate limiter
                if (m_EnableRateLimit)
                {
                    lock (_rateLimitLock)
                    {
                        _rateLimiters[client] = new TokenBucket(m_BurstSize, m_RateLimit);
                    }
                }

                // Fire m_OnClientConnected only after handshake completes
                await SwitchToMainThreadWithRetry();
                m_OnClientConnected?.Invoke(clientInfo);

                // Start receive loop, heartbeat loop, and metrics loop concurrently
                var receiveTask = ReceiveDataLoopAsync(stream, clientInfo, token);
                var heartbeatTask = HeartbeatLoopAsync(client, stream, clientInfo, token);
                var metricsTask = m_EnableMetrics ? MetricsReportLoopAsync(client, clientInfo, token) : UniTask.CompletedTask;
                await UniTask.WhenAll(receiveTask, heartbeatTask, metricsTask);
            }
            catch (OperationCanceledException)
            {
                // Silent cancellation
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted ||
                                            ex.SocketErrorCode == SocketError.ConnectionAborted ||
                                            ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                // Connection closed
            }
            catch (IOException ex) when (ex.InnerException is SocketException socketEx &&
                                       (socketEx.SocketErrorCode == SocketError.OperationAborted ||
                                        socketEx.SocketErrorCode == SocketError.ConnectionAborted ||
                                        socketEx.SocketErrorCode == SocketError.ConnectionReset))
            {
                // Connection closed
            }
            catch (ObjectDisposedException)
            {
                // Stream disposed
            }
            catch (Exception ex)
            {
                Log($"Unexpected client handling error: {ex.GetType().Name} - {ex.Message}");
                ReportError(ErrorInfo.ErrorType.DataTransmission, "Unexpected error handling client", ex, clientInfo);
            }
            finally
            {
                // Clean up pending/active connections
                lock (_handshakeLock)
                {
                    _pendingConnections.Remove(client);
                }

                lock (m_Clients)
                {
                    m_Clients.Remove(client);
                }

                // Clean up metrics
                if (m_EnableMetrics)
                {
                    lock (_metricsLock)
                    {
                        _clientMetrics.Remove(client);
                    }
                }

                // Clean up heartbeat tracking
                lock (_heartbeatLock)
                {
                    _lastHeartbeatTicks.Remove(client);
                }

                // Clean up authentication tracking
                if (m_EnableAuthentication)
                {
                    _authenticatedClients.Remove(client);
                }

                // Clean up rate limiter
                if (m_EnableRateLimit)
                {
                    lock (_rateLimitLock)
                    {
                        _rateLimiters.Remove(client);
                    }
                }

                client.Close();

                // Only fire disconnect event if handshake was complete
                if (handshakeComplete)
                {
                    await SwitchToMainThreadWithRetry();
                    m_OnClientDisconnected?.Invoke(clientInfo);

                    // Sync: Broadcast client left to all remaining clients
                    await BroadcastClientListSync(clientInfo.id, isFullSync: false);
                }
            }
        }

        private async UniTask BroadcastPresenceAsync(CancellationToken token)
        {
            try
            {
                using var udpClient = new UdpClient();
                udpClient.EnableBroadcast = true;

                var broadcastAddress = new IPEndPoint(IPAddress.Broadcast, m_DiscoveryPort);
                var messageBytes = Encoding.UTF8.GetBytes(m_DiscoveryMessage);

                Log($"Broadcasting discovery on UDP port {m_DiscoveryPort}");
                bool useBroadcast = true;
                int broadcastCount = 0;
                bool wasBroadcastingPaused = false;

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // Check if server has reached max concurrent connections
                        int currentClientCount;
                        lock (m_Clients)
                        {
                            currentClientCount = m_Clients.Count;
                        }

                        bool shouldPauseBroadcast = currentClientCount >= m_MaxConcurrentConnections;

                        if (shouldPauseBroadcast)
                        {
                            // Server is full - pause broadcasting
                            if (!wasBroadcastingPaused)
                            {
                                Log($"Server reached max connections ({currentClientCount}/{m_MaxConcurrentConnections}). Pausing discovery broadcast...");
                                wasBroadcastingPaused = true;
                            }

                            // Wait longer when server is paused to reduce CPU usage
                            await UniTask.Delay(m_DiscoveryInterval * 3, cancellationToken: token);
                            continue;
                        }
                        else
                        {
                            // Server has available slots - resume/continue broadcasting
                            if (wasBroadcastingPaused)
                            {
                                Log($"Server has available slots ({currentClientCount}/{m_MaxConcurrentConnections}). Resuming discovery broadcast...");
                                wasBroadcastingPaused = false;
                            }
                        }

                        broadcastCount++;

                        if (useBroadcast)
                        {
                            await udpClient.SendAsync(messageBytes, messageBytes.Length, broadcastAddress);
                        }
                        else
                        {
                            // Send to common local network ranges
                            var localRanges = new[]
                            {
                                "192.168.1.255",
                                "192.168.0.255",
                                "10.0.0.255",
                                "172.16.255.255"
                            };

                            foreach (var range in localRanges)
                            {
                                try
                                {
                                    var endpoint = new IPEndPoint(IPAddress.Parse(range), m_DiscoveryPort);
                                    await udpClient.SendAsync(messageBytes, messageBytes.Length, endpoint);
                                }
                                catch
                                {
                                    // Ignore individual subnet failures
                                }
                            }
                        }

                        await UniTask.Delay(m_DiscoveryInterval, cancellationToken: token);
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostUnreachable ||
                                                    ex.SocketErrorCode == SocketError.NetworkUnreachable)
                    {
                        if (useBroadcast)
                        {
                            Log($"Broadcast failed, switching to local subnet");
                            useBroadcast = false;
                        }
                        await UniTask.Delay(m_DiscoveryInterval, cancellationToken: token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log($"Broadcast error: {ex.Message}");
                        await UniTask.Delay(m_DiscoveryInterval, cancellationToken: token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Silent cancellation
            }
            catch (Exception ex)
            {
                Log($"Broadcast setup error: {ex.Message}");
            }
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
            }
            catch (Exception ex)
            {
                Log($"Failed to configure TCP client: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends periodic heartbeat pings to detect dead connections (Server-side)
        /// </summary>
        private async UniTask HeartbeatLoopAsync(TcpClient client, NetworkStream stream, ConnectorInfo clientInfo, CancellationToken token)
        {
            if (!m_EnableHeartbeat) return;

            try
            {
                // Initialize heartbeat timestamp
                lock (_heartbeatLock)
                {
                    _lastHeartbeatTicks[client] = DateTime.UtcNow.Ticks;
                }

                while (!token.IsCancellationRequested && client.Connected && stream.CanWrite)
                {
                    await UniTask.Delay(m_HeartbeatInterval, cancellationToken: token);

                    // Check if connection is still alive
                    lock (_heartbeatLock)
                    {
                        if (!_lastHeartbeatTicks.ContainsKey(client)) break;

                        long lastHeartbeat = _lastHeartbeatTicks[client];
                        long elapsedTicks = DateTime.UtcNow.Ticks - lastHeartbeat;
                        long elapsedMs = elapsedTicks / TimeSpan.TicksPerMillisecond;

                        if (elapsedMs > m_HeartbeatTimeout)
                        {
                            Log($"⚠️ Heartbeat timeout: {clientInfo.ipAddress}:{clientInfo.port} (last: {elapsedMs}ms ago)");
                            break;
                        }
                    }

                    // Send heartbeat ping
                    byte[] pingPacket = CreatePacket(ACTION_HEARTBEAT_PING, Array.Empty<byte>());
                    await SendDataToStreamAsync(stream, pingPacket, null, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Silent cancellation
            }
            catch (Exception ex)
            {
                Log($"Heartbeat error for {clientInfo.ipAddress}:{clientInfo.port}: {ex.Message}");
            }
            finally
            {
                // Cleanup
                lock (_heartbeatLock)
                {
                    _lastHeartbeatTicks.Remove(client);
                }
            }
        }

        /// <summary>
        /// Sends periodic heartbeat pings to server (Client-side)
        /// </summary>
        private async UniTask ClientHeartbeatLoopAsync(NetworkStream stream, CancellationToken token)
        {
            if (!m_EnableHeartbeat) return;

            try
            {
                _lastServerHeartbeatTicks = DateTime.UtcNow.Ticks;

                while (!token.IsCancellationRequested && stream.CanWrite)
                {
                    await UniTask.Delay(m_HeartbeatInterval, cancellationToken: token);

                    // Check server timeout
                    long elapsedTicks = DateTime.UtcNow.Ticks - _lastServerHeartbeatTicks;
                    long elapsedMs = elapsedTicks / TimeSpan.TicksPerMillisecond;

                    if (elapsedMs > m_HeartbeatTimeout)
                    {
                        Log($"⚠️ Server heartbeat timeout (last: {elapsedMs}ms ago)");
                        break;
                    }

                    // Send heartbeat ping
                    byte[] pingPacket = CreatePacket(ACTION_HEARTBEAT_PING, Array.Empty<byte>());
                    await SendDataToStreamAsync(stream, pingPacket, null, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Silent cancellation
            }
            catch (Exception ex)
            {
                Log($"Client heartbeat error: {ex.Message}");
            }
        }

        #endregion

        #region Connection Metrics

        /// <summary>
        /// Periodically reports connection metrics (Server-side per-client)
        /// </summary>
        private async UniTask MetricsReportLoopAsync(TcpClient client, ConnectorInfo clientInfo, CancellationToken token)
        {
            if (!m_EnableMetrics) return;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Delay(m_MetricsReportInterval, cancellationToken: token);

                    ConnectionMetrics metrics;
                    lock (_metricsLock)
                    {
                        if (!_clientMetrics.TryGetValue(client, out metrics))
                        {
                            break; // Client disconnected
                        }
                        metrics.UpdateRates();
                    }

                    // Fire metrics event on main thread
                    await SwitchToMainThreadWithRetry();
                    m_OnMetricsUpdated?.Invoke(metrics);
                }
            }
            catch (OperationCanceledException)
            {
                // Silent cancellation
            }
            catch (Exception ex)
            {
                Log($"Metrics report error for {clientInfo.ipAddress}:{clientInfo.port}: {ex.Message}");
            }
        }

        /// <summary>
        /// Periodically reports server connection metrics (Client-side)
        /// </summary>
        private async UniTask ClientMetricsReportLoopAsync(CancellationToken token)
        {
            if (!m_EnableMetrics) return;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Delay(m_MetricsReportInterval, cancellationToken: token);

                    ConnectionMetrics metrics;
                    lock (_metricsLock)
                    {
                        if (_serverMetrics == null)
                        {
                            break; // Disconnected
                        }
                        _serverMetrics.UpdateRates();
                        metrics = _serverMetrics;
                    }

                    // Fire metrics event on main thread
                    await SwitchToMainThreadWithRetry();
                    m_OnMetricsUpdated?.Invoke(metrics);
                }
            }
            catch (OperationCanceledException)
            {
                // Silent cancellation
            }
            catch (Exception ex)
            {
                Log($"Client metrics report error: {ex.Message}");
            }
        }

        #endregion

        #region Encryption Methods

        /// <summary>
        /// Initializes AES encryption provider with key
        /// </summary>
        private void InitializeEncryption()
        {
            lock (_encryptionLock)
            {
                try
                {
                    // Dispose existing provider
                    _aesProvider?.Dispose();

                    // Create AES provider
                    _aesProvider = System.Security.Cryptography.Aes.Create();
                    _aesProvider.Mode = System.Security.Cryptography.CipherMode.CBC;
                    _aesProvider.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
                    _aesProvider.KeySize = 256; // AES-256

                    // Parse or generate key
                    if (!string.IsNullOrEmpty(m_EncryptionKey))
                    {
                        // Try to parse as hex or base64
                        try
                        {
                            // Try hex first
                            _encryptionKeyBytes = HexStringToBytes(m_EncryptionKey);
                        }
                        catch
                        {
                            // Try base64
                            _encryptionKeyBytes = System.Convert.FromBase64String(m_EncryptionKey);
                        }

                        if (_encryptionKeyBytes.Length != 32)
                        {
                            Log($"❌ Encryption key must be 32 bytes (256 bits). Got {_encryptionKeyBytes.Length} bytes. Generating random key.");
                            _encryptionKeyBytes = null;
                        }
                    }

                    // Generate random key if not provided or invalid
                    if (_encryptionKeyBytes == null)
                    {
                        _aesProvider.GenerateKey();
                        _encryptionKeyBytes = _aesProvider.Key;
                        Log($"⚠️ Generated random encryption key: {BytesToHexString(_encryptionKeyBytes)}");
                        Log($"⚠️ WARNING: Save this key! Clients must use the same key to connect.");
                    }

                    _aesProvider.Key = _encryptionKeyBytes;

                    Log("✅ Encryption initialized (AES-256-CBC)");
                }
                catch (Exception ex)
                {
                    Log($"❌ Failed to initialize encryption: {ex.Message}");
                    m_EnableEncryption = false;
                }
            }
        }

        /// <summary>
        /// Encrypts data using AES
        /// </summary>
        private byte[] EncryptData(byte[] data)
        {
            if (!m_EnableEncryption || _aesProvider == null) return data;

            lock (_encryptionLock)
            {
                try
                {
                    // Generate random IV for each message
                    _aesProvider.GenerateIV();
                    var iv = _aesProvider.IV;

                    // Encrypt data
                    using (var encryptor = _aesProvider.CreateEncryptor())
                    using (var ms = new System.IO.MemoryStream())
                    {
                        // Write IV at the beginning (16 bytes for AES)
                        ms.Write(iv, 0, iv.Length);

                        // Write encrypted data
                        using (var cs = new System.Security.Cryptography.CryptoStream(ms, encryptor, System.Security.Cryptography.CryptoStreamMode.Write))
                        {
                            cs.Write(data, 0, data.Length);
                            cs.FlushFinalBlock();
                        }

                        return ms.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    Log($"❌ Encryption failed: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Decrypts data using AES
        /// </summary>
        private byte[] DecryptData(byte[] encryptedData)
        {
            if (!m_EnableEncryption || _aesProvider == null) return encryptedData;

            lock (_encryptionLock)
            {
                try
                {
                    // Extract IV from the beginning (16 bytes)
                    var iv = new byte[16];
                    System.Buffer.BlockCopy(encryptedData, 0, iv, 0, 16);

                    // Decrypt data
                    using (var decryptor = _aesProvider.CreateDecryptor(_encryptionKeyBytes, iv))
                    using (var ms = new System.IO.MemoryStream(encryptedData, 16, encryptedData.Length - 16))
                    using (var cs = new System.Security.Cryptography.CryptoStream(ms, decryptor, System.Security.Cryptography.CryptoStreamMode.Read))
                    using (var result = new System.IO.MemoryStream())
                    {
                        cs.CopyTo(result);
                        return result.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    Log($"❌ Decryption failed: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Converts hex string to byte array
        /// </summary>
        private byte[] HexStringToBytes(string hex)
        {
            hex = hex.Replace(" ", "").Replace("-", "");
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = System.Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        /// <summary>
        /// Converts byte array to hex string
        /// </summary>
        private string BytesToHexString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        #endregion

        #region Authentication Methods

        /// <summary>
        /// Initializes authentication system with password hashing
        /// </summary>
        private void InitializeAuthentication()
        {
            if (string.IsNullOrEmpty(m_AuthPassword))
            {
                Log("⚠️ Authentication enabled but no password set. Access will be denied to all clients.");
                _authPasswordHash = "";
                return;
            }

            // Hash password using SHA256
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var passwordBytes = System.Text.Encoding.UTF8.GetBytes(m_AuthPassword);
                var hashBytes = sha256.ComputeHash(passwordBytes);
                _authPasswordHash = BytesToHexString(hashBytes);
            }

            Log("✅ Authentication initialized (SHA256)");
        }

        /// <summary>
        /// Validates authentication credentials
        /// </summary>
        private bool ValidateAuthentication(string providedPassword)
        {
            if (string.IsNullOrEmpty(_authPasswordHash))
            {
                return false; // No password set
            }

            // Hash provided password
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var passwordBytes = System.Text.Encoding.UTF8.GetBytes(providedPassword);
                var hashBytes = sha256.ComputeHash(passwordBytes);
                var providedHash = BytesToHexString(hashBytes);
                
                return providedHash == _authPasswordHash;
            }
        }

        #endregion

        #region Priority Queue Methods

        /// <summary>
        /// Initializes priority queues for all priority levels
        /// </summary>
        private void InitializePriorityQueues()
        {
            lock (_queueLock)
            {
                _sendQueue.Clear();
                foreach (MessagePriority priority in System.Enum.GetValues(typeof(MessagePriority)))
                {
                    _sendQueue[priority] = new Queue<QueuedMessage>();
                }
            }
            Log("✅ Priority queues initialized");
        }

        /// <summary>
        /// Enqueues a message with specified priority
        /// </summary>
        private async UniTask<bool> EnqueueMessage(ushort action, byte[] data, MessagePriority priority, int[] targetClientIds, IProgress<float> progress, CancellationToken token)
        {
            if (!m_EnablePriorityQueue)
            {
                // Direct send if priority queue disabled
                return await SendDataAsync(action, data, progress, token, isInternalCall: false);
            }

            UniTaskCompletionSource<bool> completionSource;

            lock (_queueLock)
            {
                if (_sendQueue[priority].Count >= m_QueueMaxSize)
                {
                    Log($"⚠️ Message queue full for priority {priority} (max: {m_QueueMaxSize})");
                    return false;
                }

                var queuedMessage = new QueuedMessage
                {
                    Action = action,
                    Data = data,
                    TargetClientIds = targetClientIds,
                    Progress = progress,
                    Token = token,
                    CompletionSource = new UniTaskCompletionSource<bool>()
                };

                completionSource = queuedMessage.CompletionSource;
                _sendQueue[priority].Enqueue(queuedMessage);

                // Start queue processor if not already running
                if (!_isProcessingQueue)
                {
                    _isProcessingQueue = true;
                    ProcessMessageQueue(_cts.Token).Forget();
                }
            }

            return await completionSource.Task;
        }

        /// <summary>
        /// Processes messages from priority queues (highest priority first)
        /// </summary>
        private async UniTask ProcessMessageQueue(CancellationToken token)
        {
            while (!token.IsCancellationRequested && m_IsRunning)
            {
                QueuedMessage message = null;

                // Dequeue highest priority message
                lock (_queueLock)
                {
                    foreach (MessagePriority priority in System.Enum.GetValues(typeof(MessagePriority)))
                    {
                        if (_sendQueue[priority].Count > 0)
                        {
                            message = _sendQueue[priority].Dequeue();
                            break;
                        }
                    }

                    if (message == null)
                    {
                        _isProcessingQueue = false;
                        return; // No messages to process
                    }
                }

                // Send the message
                try
                {
                    bool success = await SendDataAsync(message.Action, message.Data, message.Progress, message.Token, isInternalCall: false);
                    message.CompletionSource.TrySetResult(success);
                }
                catch (Exception ex)
                {
                    Log($"Queue processing error: {ex.Message}");
                    message.CompletionSource.TrySetException(ex);
                }

                // Small delay to prevent tight loop
                await UniTask.Delay(1, cancellationToken: token);
            }

            _isProcessingQueue = false;
        }

        #endregion

        #region Packet Utilities

        /// <summary>
        /// Checks if an action number is reserved for internal protocol use.
        /// Reserved range: 65529-65535 or 0xFFFD-0xFFFF.
        /// </summary>
        private bool IsReservedAction(ushort action)
        {
            return action >= RESERVED_ACTION_MIN && action <= RESERVED_ACTION_MAX;
        }

        /// <summary>
        /// Creates the core packet structure
        /// </summary>
        private byte[] CreateCorePacket(ushort action, byte[] data)
        {
            data ??= Array.Empty<byte>();
            byte[] packet = new byte[PACKET_HEADER_SIZE + data.Length];
            int offset = 0;

            // Generate random message ID
            Buffer.BlockCopy(BitConverter.GetBytes(new System.Random().Next()), 0, packet, offset, 4); offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(action), 0, packet, offset, 2);

            Buffer.BlockCopy(data, 0, packet, PACKET_HEADER_SIZE, data.Length);

            return packet;
        }

        /// <summary>
        /// Reads the core packet structure
        /// </summary>
        private PacketResponse ReadCorePacket(byte[] data, int dataLength, ConnectorInfo connectorInfo)
        {
            if (data == null || dataLength < PACKET_HEADER_SIZE) return null;

            int offset = 0;
            int messageId = BitConverter.ToInt32(data, offset); offset += 4;
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
                senderId = connectorInfo.id,
                action = action,
                data = payload,
                remoteEndPoint = connectorInfo.remoteEndPoint,
                status = PacketResponse.ReceiveStatus.None,
                totalBytes = dataLength,
                processedBytes = dataLength
            };
        }

        #endregion

        #region Client Methods



        /// <summary>
        /// Discover server IP address using UDP broadcast - keeps listening until server found
        /// </summary>
        private async UniTask DiscoverServerAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    string serverIp = await ListenForServerAsync(token);

                    if (!string.IsNullOrEmpty(serverIp))
                    {
                        m_Host = serverIp;
                        return;
                    }
                    // If no server found, wait before retrying
                    await UniTask.Delay(1000, cancellationToken: token);

                }
                catch (OperationCanceledException)
                {
                    // Silent cancellation
                    return;
                }
                catch (Exception ex)
                {
                    Log($"Discovery Error: {ex.Message}");
                    ReportError(ErrorInfo.ErrorType.Discovery, "Server discovery failed", ex);
                }

            }
        }


        private async UniTask StartClientAsync(CancellationToken token)
        {
            _reconnectAttempts = 0;
            _isReconnecting = false;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (m_EnableDiscovery)
                        await DiscoverServerAsync(token).AttachExternalCancellation(token);

                    // Start TCP client connection
                    await ConnectToServerAsync(m_Host, m_Port, token).AttachExternalCancellation(token);

                    // If we reach here, connection was successful but then disconnected
                    // Reset reconnection tracking on successful connection
                    if (_isReconnecting)
                    {
                        _reconnectAttempts = 0;
                        _isReconnecting = false;
                    }

                    // Check if auto-reconnect is enabled
                    if (!m_EnableAutoReconnect || !m_IsRunning)
                    {
                        Log("Connection lost. Auto-reconnect disabled.");
                        break; // Exit loop if auto-reconnect is disabled
                    }

                    // Prepare for reconnection
                    _reconnectAttempts++;
                    _isReconnecting = true;

                    // Check max attempts (0 = infinite)
                    if (m_ReconnectMaxAttempts > 0 && _reconnectAttempts > m_ReconnectMaxAttempts)
                    {
                        Log($"❌ Max reconnection attempts ({m_ReconnectMaxAttempts}) reached. Giving up.");
                        await SwitchToMainThreadWithRetry();
                        m_OnReconnectFailed?.Invoke();
                        break;
                    }

                    // Calculate delay with exponential backoff
                    int delayIndex = Math.Min(_reconnectAttempts - 1, m_ReconnectDelays.Length - 1);
                    int delay = m_ReconnectDelays[delayIndex];

                    Log($"🔄 Reconnecting... (Attempt {_reconnectAttempts}/{(m_ReconnectMaxAttempts > 0 ? m_ReconnectMaxAttempts.ToString() : "∞")}, Delay: {delay}ms)");
                    
                    // Fire reconnecting event
                    await SwitchToMainThreadWithRetry();
                    m_OnReconnecting?.Invoke(_reconnectAttempts);

                    // Wait before next attempt
                    await UniTask.Delay(delay, cancellationToken: token);
                }
                catch (OperationCanceledException)
                {
                    // Exit gracefully on cancellation
                    break;
                }
                catch (Exception ex)
                {
                    Log($"Client Error: {ex.GetType().Name} - {ex.Message}");
                    ReportError(ErrorInfo.ErrorType.Connection, "Client connection failed", ex);

                    // If not auto-reconnect, break the loop
                    if (!m_EnableAutoReconnect || !m_IsRunning)
                    {
                        break;
                    }
                }
            }

            // Cleanup on exit
            _isReconnecting = false;
            _reconnectAttempts = 0;
        }

        private async UniTask ConnectToServerAsync(string _host, int _port, CancellationToken token)
        {
            bool handshakeComplete = false;

            try
            {
                Log($"Attempting to connect to {_host}:{_port}");

                _tcpClient = new TcpClient();
                _tcpClient.ReceiveBufferSize = m_BufferSize;
                _tcpClient.SendBufferSize = m_BufferSize;

                // Add connection timeout for builds - wrap to avoid SynchronizationContext issues
                var connectTask = UniTask.Create(async () => {
                    await _tcpClient.ConnectAsync(_host, _port).ConfigureAwait(false);
                });
                var timeoutTask = UniTask.Delay(10000, cancellationToken: token); // 10 second timeout

                var result = await UniTask.WhenAny(connectTask, timeoutTask);

                if (result == 1) // Timeout
                {
                    Log($"❌ Connection timeout to {_host}:{_port}");
                    throw new TimeoutException($"Connection timeout to {_host}:{_port}");
                }

                if (!_tcpClient.Connected)
                {
                    Log($"❌ Failed to connect to {_host}:{_port}");
                    throw new InvalidOperationException($"Failed to connect to {_host}:{_port}");
                }

                ConfigureTcpClient(_tcpClient);

                var serverEndPoint = (IPEndPoint)_tcpClient.Client.RemoteEndPoint;

                m_ServerInfo = new ConnectorInfo
                {
                    id = System.Threading.Interlocked.Increment(ref _nextClientId),
                    ipAddress = serverEndPoint.Address.ToString(),
                    port = serverEndPoint.Port,
                    remoteEndPoint = serverEndPoint
                };

                Log($"✅ TCP connected to server: {m_ServerInfo.ipAddress}:{m_ServerInfo.port} (ID: {m_ServerInfo.id})");

                var stream = _tcpClient.GetStream();

                // --- Client Handshake Flow ---

                // Add server to pending connections
                lock (_handshakeLock)
                {
                    _pendingConnections[_tcpClient] = ConnectionState.Pending;
                }

                // --- Prepare ALL handshake waiters BEFORE starting receive loop (prevent race condition) ---
                string helloKey = $"hello_{m_ServerInfo.id}";
                string authReqKey = $"authreq_{m_ServerInfo.id}"; // Authentication request key
                string clientListKey = $"clientlist_{m_ServerInfo.id}";
                UniTaskCompletionSource<PacketResponse> helloWaiter;
                UniTaskCompletionSource<PacketResponse> authReqWaiter = null; // Only if auth enabled
                UniTaskCompletionSource<PacketResponse> clientListWaiter;
                
                lock (_handshakeLock)
                {
                    helloWaiter = new UniTaskCompletionSource<PacketResponse>();
                    _handshakeWaiters[helloKey] = helloWaiter;
                    
                    if (m_EnableAuthentication)
                    {
                        authReqWaiter = new UniTaskCompletionSource<PacketResponse>();
                        _handshakeWaiters[authReqKey] = authReqWaiter;
                    }
                    
                    clientListWaiter = new UniTaskCompletionSource<PacketResponse>();
                    _handshakeWaiters[clientListKey] = clientListWaiter;
                }

                // Start receive loop in background to process handshake messages
                var receiveLoopTask = ReceiveDataLoopAsync(stream, m_ServerInfo, token);

                // --- Handshake Step 1: Wait for HELLO from server ---
                using (var timeoutCts = new CancellationTokenSource(HANDSHAKE_TIMEOUT_MS))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token))
                {
                    try
                    {
                        await helloWaiter.Task.AttachExternalCancellation(linkedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        lock (_handshakeLock)
                        {
                            _handshakeWaiters.Remove(helloKey);
                            _handshakeWaiters.Remove(clientListKey);
                        }

                        if (timeoutCts.IsCancellationRequested)
                        {
                            Log($"Handshake timeout: Server did not send HELLO");
                            throw new TimeoutException("Server handshake HELLO timeout");
                        }
                        throw;
                    }
                }

                lock (_handshakeLock)
                {
                    _handshakeWaiters.Remove(helloKey);
                    _pendingConnections[_tcpClient] = ConnectionState.HelloSent;
                }

                // --- Handshake Step 2: Send ACK to server ---
                byte[] ackPacket = CreatePacket(ACTION_CONNECTION_ACK, Array.Empty<byte>());
                await SendDataToStreamAsync(stream, ackPacket, null, token);

                lock (_handshakeLock)
                {
                    _pendingConnections[_tcpClient] = ConnectionState.Acknowledged;
                }

                // --- Authentication Step (if enabled): Wait for request and send credentials ---
                if (m_EnableAuthentication)
                {
                    // Wait for AUTH_REQUEST from server
                    using (var authTimeoutCts = new CancellationTokenSource(m_AuthTimeout))
                    using (var linkedAuthCts = CancellationTokenSource.CreateLinkedTokenSource(token, authTimeoutCts.Token))
                    {
                        try
                        {
                            await authReqWaiter.Task.AttachExternalCancellation(linkedAuthCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            lock (_handshakeLock)
                            {
                                _handshakeWaiters.Remove(authReqKey);
                            }

                            if (authTimeoutCts.IsCancellationRequested)
                            {
                                Log($"❌ Authentication timeout: Server did not request authentication");
                                throw new TimeoutException("Server authentication request timeout");
                            }
                            throw;
                        }
                    }

                    lock (_handshakeLock)
                    {
                        _handshakeWaiters.Remove(authReqKey);
                    }

                    // Send AUTH_RESPONSE with password
                    byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(m_AuthPassword);
                    byte[] authResponsePacket = CreatePacket(ACTION_AUTH_RESPONSE, passwordBytes);
                    await SendDataToStreamAsync(stream, authResponsePacket, null, token);

                    Log($"📤 Sent authentication credentials to server");
                }

                // --- Handshake Step 3: Wait for CLIENT_LIST_FULL (waiter already created above) ---

                using (var timeoutCts = new CancellationTokenSource(HANDSHAKE_TIMEOUT_MS))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token))
                {
                    try
                    {
                        await clientListWaiter.Task.AttachExternalCancellation(linkedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        lock (_handshakeLock)
                        {
                            _handshakeWaiters.Remove(clientListKey);
                        }

                        if (timeoutCts.IsCancellationRequested)
                        {
                            Log($"Handshake timeout: Server did not send CLIENT_LIST");
                            throw new TimeoutException("Server handshake CLIENT_LIST timeout");
                        }
                        throw;
                    }
                }

                lock (_handshakeLock)
                {
                    _handshakeWaiters.Remove(clientListKey);
                }

                // --- Handshake Step 4: Send READY to server ---
                byte[] readyPacket = CreatePacket(ACTION_CONNECTION_READY, Array.Empty<byte>());
                await SendDataToStreamAsync(stream, readyPacket, null, token);

                lock (_handshakeLock)
                {
                    _pendingConnections[_tcpClient] = ConnectionState.Ready;
                    _pendingConnections.Remove(_tcpClient);
                }

                m_IsConnected = true;
                handshakeComplete = true;
                Log($"✅ Handshake complete with server: {m_ServerInfo.ipAddress}:{m_ServerInfo.port}");

                // Initialize metrics tracking
                if (m_EnableMetrics)
                {
                    lock (_metricsLock)
                    {
                        _serverMetrics = new ConnectionMetrics(m_ServerInfo.id);
                    }
                }

                // Fire m_OnServerConnected only after handshake completes
                await SwitchToMainThreadWithRetry();
                m_OnServerConnected?.Invoke(m_ServerInfo);

                // Fire reconnected event if this was a reconnection
                if (_isReconnecting && _reconnectAttempts > 0)
                {
                    Log($"✅ Reconnected successfully after {_reconnectAttempts} attempt(s)");
                    m_OnReconnected?.Invoke(m_ServerInfo);
                }

                // Start heartbeat loop, metrics loop, and wait for all (receive already running)
                var heartbeatTask = ClientHeartbeatLoopAsync(stream, token);
                var metricsTask = m_EnableMetrics ? ClientMetricsReportLoopAsync(token) : UniTask.CompletedTask;
                await UniTask.WhenAll(receiveLoopTask, heartbeatTask, metricsTask);
            }
            catch (OperationCanceledException)
            {
                // Silent cancellation - don't log when stopping
                throw;
            }
            catch (Exception ex)
            {
                Log($"TCP connection error: {ex.GetType().Name} - {ex.Message}");
                throw;
            }
            finally
            {
                // Clean up pending connections
                lock (_handshakeLock)
                {
                    if (_tcpClient != null)
                    {
                        _pendingConnections.Remove(_tcpClient);
                    }
                }

                // Clean up metrics
                if (m_EnableMetrics)
                {
                    lock (_metricsLock)
                    {
                        _serverMetrics = null;
                    }
                }

                // Only fire disconnect event if handshake was complete
                if (handshakeComplete && m_IsConnected)
                {
                    m_IsConnected = false;
                    await SwitchToMainThreadWithRetry();
                    m_OnServerDisconnected?.Invoke(m_ServerInfo);
                }

                _tcpClient?.Close();
                _tcpClient?.Dispose();
                _tcpClient = null;
            }
        }

        private async UniTask<string> ListenForServerAsync(CancellationToken token)
        {
            try
            {
                using var udpClient = new UdpClient(m_DiscoveryPort);

                Log($"Listening for server on UDP port {m_DiscoveryPort}");

                // Use ConfigureAwait(false) to avoid SynchronizationContext issues
                var receiveResult = await udpClient.ReceiveAsync().ConfigureAwait(false);
                
                // Check cancellation after await
                token.ThrowIfCancellationRequested();
                
                var receivedMessage = Encoding.UTF8.GetString(receiveResult.Buffer);

                if (receivedMessage == m_DiscoveryMessage)
                {
                    var serverIP = receiveResult.RemoteEndPoint.Address.ToString();
                    Log($"✅ Server found: {serverIP}");
                    return serverIP;
                }
            }
            catch (OperationCanceledException)
            {
                // Silent cancellation
            }
            catch (Exception ex)
            {
                Log($"Listen error: {ex.Message}");
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
                    if (bytesRead < 4)
                    {
                        break; // Connection closed
                    }

                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                    if (messageLength <= 0 || messageLength > 1024 * 1024 * 10) // Max 10MB
                    {
                        Log($"Invalid message length: {messageLength}. Disconnecting.");
                        break;
                    }

                    // Log receive start
                    bool isLargeReceive = messageLength > m_BufferSize * 2;
                    if (m_LogReceive)
                    {
                        if (isLargeReceive)
                        {
                            Log($"📥 Receiving large data: {messageLength:N0} bytes from {connectorInfo.ipAddress}:{connectorInfo.port}");
                        }
                        else
                        {
                            Log($"📥 Receiving data: Size={messageLength} bytes from {connectorInfo.ipAddress}:{connectorInfo.port}");
                        }
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
                        int lastLoggedProgress = 0;

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
                            
                            // Log progress for large transfers (every 25%)
                            if (isLargeReceive && m_LogReceive)
                            {
                                float progressPercentage = (float)totalBytesRead / messageLength;
                                int currentProgress = (int)(progressPercentage * 100);
                                
                                if (currentProgress >= lastLoggedProgress + 25 || totalBytesRead >= messageLength)
                                {
                                    lastLoggedProgress = currentProgress;
                                    Log($"📥 Receive progress: {currentProgress}% ({totalBytesRead:N0}/{messageLength:N0} bytes)");
                                }
                            }

                            // Throttle progress reports to avoid UI spam (Optimized)
                            var nowTicks = DateTime.UtcNow.Ticks;
                            if ((nowTicks - _lastProgressTicks) / TimeSpan.TicksPerMillisecond >= progressReportIntervalMs ||
                                totalBytesRead >= messageLength)
                            {
                                _lastProgressTicks = nowTicks;
                                try
                                {
                                    await UniTask.SwitchToMainThread();
                                    m_OnDataReceived?.Invoke(packetResponse);
                                }
                                catch (Exception ex)
                                {
                                    Log($"Warning: Could not switch to main thread for progress update: {ex.Message}");
                                }
                            }

                            // Only yield for large messages to reduce overhead
                            if (messageLength > m_BufferSize)
                            {
                                await UniTask.Yield();
                            }
                        }

                        // Parse the complete packet and update status
                        var finalPacketResponse = ReadPacket(dataBuffer, messageLength, connectorInfo);
                        if (finalPacketResponse != null)
                        {
                            // Copy progress information to final response
                            finalPacketResponse.status = PacketResponse.ReceiveStatus.Received;
                            finalPacketResponse.totalBytes = messageLength;
                            finalPacketResponse.processedBytes = totalBytesRead;
                            finalPacketResponse.operationId = operationId;

                            // --- Rate Limiting Check (Server-side only) ---
                            if (m_IsServer && m_EnableRateLimit)
                            {
                                TcpClient clientKey = null;
                                lock (m_Clients)
                                {
                                    foreach (var kvp in m_Clients)
                                    {
                                        if (kvp.Value.id == connectorInfo.id)
                                        {
                                            clientKey = kvp.Key;
                                            break;
                                        }
                                    }
                                }

                                if (clientKey != null)
                                {
                                    TokenBucket rateLimiter;
                                    lock (_rateLimitLock)
                                    {
                                        _rateLimiters.TryGetValue(clientKey, out rateLimiter);
                                    }

                                    if (rateLimiter != null && !rateLimiter.TryConsume(1))
                                    {
                                        // Rate limit exceeded - disconnect client
                                        Log($"⚠️ Rate limit exceeded for {connectorInfo.ipAddress}:{connectorInfo.port} - disconnecting");
                                        await SwitchToMainThreadWithRetry();
                                        m_OnRateLimitExceeded?.Invoke(connectorInfo, (int)rateLimiter.GetCurrentTokens());
                                        break; // Exit receive loop to disconnect
                                    }
                                }
                            }

                            // --- Handle Handshake Messages (Internal Protocol) ---
                            bool isHandshakeMessage = false;

                            // Server-side: Handle ACK and READY from client
                            if (m_IsServer && (finalPacketResponse.action == ACTION_CONNECTION_ACK || 
                                              finalPacketResponse.action == ACTION_CONNECTION_READY))
                            {
                                isHandshakeMessage = true;
                                string waitKey = finalPacketResponse.action == ACTION_CONNECTION_ACK 
                                    ? $"ack_{connectorInfo.id}" 
                                    : $"ready_{connectorInfo.id}";

                                lock (_handshakeLock)
                                {
                                    if (_handshakeWaiters.TryGetValue(waitKey, out var waiter))
                                    {
                                        waiter.TrySetResult(finalPacketResponse);
                                    }
                                }
                            }

                            // Client-side: Handle HELLO and CLIENT_LIST from server
                            if (!m_IsServer && (finalPacketResponse.action == ACTION_CONNECTION_HELLO || 
                                               finalPacketResponse.action == ACTION_CLIENT_LIST_FULL ||
                                               finalPacketResponse.action == ACTION_AUTH_REQUEST))
                            {
                                isHandshakeMessage = true;
                                string waitKey = finalPacketResponse.action == ACTION_CONNECTION_HELLO 
                                    ? $"hello_{connectorInfo.id}" 
                                    : finalPacketResponse.action == ACTION_AUTH_REQUEST
                                        ? $"authreq_{connectorInfo.id}"
                                        : $"clientlist_{connectorInfo.id}";

                                lock (_handshakeLock)
                                {
                                    if (_handshakeWaiters.TryGetValue(waitKey, out var waiter))
                                    {
                                        waiter.TrySetResult(finalPacketResponse);
                                    }
                                }

                                // For CLIENT_LIST, also process it normally for client list sync
                                if (finalPacketResponse.action == ACTION_CLIENT_LIST_FULL)
                                {
                                    await ProcessClientSyncMessage(finalPacketResponse);
                                }
                            }

                            // Server-side: Handle AUTH_RESPONSE from client
                            if (m_IsServer && finalPacketResponse.action == ACTION_AUTH_RESPONSE)
                            {
                                isHandshakeMessage = true;
                                string waitKey = $"auth_{connectorInfo.id}";

                                lock (_handshakeLock)
                                {
                                    if (_handshakeWaiters.TryGetValue(waitKey, out var waiter))
                                    {
                                        waiter.TrySetResult(finalPacketResponse);
                                    }
                                }
                            }

                            // Server-side: Handle VERSION_RESPONSE from client
                            if (m_IsServer && finalPacketResponse.action == ACTION_VERSION_RESPONSE)
                            {
                                isHandshakeMessage = true;
                                string waitKey = $"version_{connectorInfo.id}";

                                lock (_handshakeLock)
                                {
                                    if (_handshakeWaiters.TryGetValue(waitKey, out var waiter))
                                    {
                                        waiter.TrySetResult(finalPacketResponse);
                                    }
                                }
                            }

                            // Client-side: Handle VERSION_REQUEST from server
                            if (!m_IsServer && finalPacketResponse.action == ACTION_VERSION_REQUEST)
                            {
                                isHandshakeMessage = true;
                                
                                // Send VERSION_RESPONSE with our protocol version
                                byte[] versionData = System.Text.Encoding.UTF8.GetBytes(m_ProtocolVersion);
                                byte[] versionResponsePacket = CreatePacket(ACTION_VERSION_RESPONSE, versionData);
                                await SendDataToStreamAsync(stream, versionResponsePacket, null, token);
                                
                                Log($"📤 Sent protocol version to server: {m_ProtocolVersion}");
                                continue; // Skip normal processing
                            }

                            // --- Handle Heartbeat Messages (Internal Protocol) ---
                            if (finalPacketResponse.action == ACTION_HEARTBEAT_PING)
                            {
                                // Respond with PONG
                                byte[] pongPacket = CreatePacket(ACTION_HEARTBEAT_PONG, Array.Empty<byte>());
                                await SendDataToStreamAsync(stream, pongPacket, null, token);
                                continue; // Skip normal processing
                            }

                            if (finalPacketResponse.action == ACTION_HEARTBEAT_PONG)
                            {
                                // Update heartbeat timestamp
                                if (m_IsServer)
                                {
                                    // Server-side: Update client heartbeat
                                    var clientKey = m_Clients.FirstOrDefault(x => x.Value.id == connectorInfo.id).Key;
                                    if (clientKey != null)
                                    {
                                        lock (_heartbeatLock)
                                        {
                                            _lastHeartbeatTicks[clientKey] = DateTime.UtcNow.Ticks;
                                        }
                                    }
                                }
                                else
                                {
                                    // Client-side: Update server heartbeat
                                    _lastServerHeartbeatTicks = DateTime.UtcNow.Ticks;
                                }
                                continue; // Skip normal processing
                            }

                            // --- Handle Graceful Disconnect Notification (Internal Protocol) ---
                            if (finalPacketResponse.action == ACTION_DISCONNECT_NOTIFICATION)
                            {
                                Log($"📢 Received graceful disconnect notification from {connectorInfo.ipAddress}:{connectorInfo.port}");
                                
                                if (m_IsServer)
                                {
                                    // Server: Client is disconnecting gracefully
                                    Log($"Client {connectorInfo.id} is disconnecting gracefully");
                                }
                                else
                                {
                                    // Client: Server is shutting down gracefully
                                    Log("Server is shutting down gracefully");
                                }
                                
                                // Break the receive loop to allow graceful disconnection
                                break;
                            }

                            // Skip normal processing for handshake messages
                            if (isHandshakeMessage && finalPacketResponse.action != ACTION_CLIENT_LIST_FULL)
                            {
                                // Don't invoke m_OnDataReceived for handshake messages (except CLIENT_LIST which is dual-purpose)
                                continue; // Skip to next message
                            }

                            // --- Normal Message Processing ---

                            // Log successful receive
                            if (m_LogReceive)
                            {
                                Log($"✅ Data received successfully: Action={finalPacketResponse.action}, Size={totalBytesRead} bytes from {connectorInfo.ipAddress}:{connectorInfo.port}");
                            }

                            // Track metrics
                            if (m_EnableMetrics)
                            {
                                lock (_metricsLock)
                                {
                                    if (m_IsServer)
                                    {
                                        // Server mode: find metrics by client
                                        TcpClient clientKey = null;
                                        lock (m_Clients)
                                        {
                                            foreach (var kvp in m_Clients)
                                            {
                                                if (kvp.Value.id == connectorInfo.id)
                                                {
                                                    clientKey = kvp.Key;
                                                    break;
                                                }
                                            }
                                        }

                                        if (clientKey != null && _clientMetrics.TryGetValue(clientKey, out var metrics))
                                        {
                                            metrics.RecordReceived(totalBytesRead);
                                        }
                                    }
                                    else
                                    {
                                        // Client mode: track server metrics
                                        _serverMetrics?.RecordReceived(totalBytesRead);
                                    }
                                }
                            }

                            try
                            {
                                await UniTask.SwitchToMainThread();
                                m_OnDataReceived?.Invoke(finalPacketResponse);
                            }
                            catch (Exception ex)
                            {
                                Log($"Warning: Could not switch to main thread for final data received: {ex.Message}");
                            }

                            // Process client sync messages (client mode only)
                            if (!m_IsServer)
                            {
                                await ProcessClientSyncMessage(finalPacketResponse);
                            }

                            // Process relay messages (server mode only)
                            if (m_IsServer)
                            {
                                await ProcessRelayMessage(finalPacketResponse, connectorInfo);
                            }
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
                    // Connection closed
                    break;
                }
                catch (IOException ex) when (ex.InnerException is SocketException socketEx &&
                                           (socketEx.SocketErrorCode == SocketError.OperationAborted ||
                                            socketEx.SocketErrorCode == SocketError.ConnectionAborted ||
                                            socketEx.SocketErrorCode == SocketError.ConnectionReset))
                {
                    // Connection closed
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // Stream disposed
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
        public async UniTask<bool> SendDataAsync(ushort action, byte[] data, CancellationToken token = default)
        {
            return await SendDataAsync(action, data, (IProgress<float>)null, token);
        }

        /// <summary>
        /// Sends data with specified priority (priority queue enabled only when m_EnablePriorityQueue is true)
        /// </summary>
        public async UniTask<bool> SendDataAsync(ushort action, byte[] data, MessagePriority priority, CancellationToken token = default)
        {
            return await SendDataAsync(action, data, null, priority, token);
        }

        /// <summary>
        /// Sends data with progress reporting and priority
        /// </summary>
        public async UniTask<bool> SendDataAsync(ushort action, byte[] data, IProgress<float> progress, MessagePriority priority, CancellationToken token = default)
        {
            data ??= Array.Empty<byte>();
            
            if (m_EnablePriorityQueue)
            {
                return await EnqueueMessage(action, data, priority, null, progress, token);
            }
            else
            {
                return await SendDataAsync(action, data, progress, token, isInternalCall: false);
            }
        }

        /// <summary>
        /// Sends data with automatic retry logic. Will attempt to send up to MaxRetryCount times
        /// with RetryDelaySeconds delay between attempts if sending fails.
        /// 
        /// For servers: Sends to all connected clients.
        /// For clients: Sends to server only.
        /// 
        /// ⚠️ WARNING: Action numbers 65529-65535 are RESERVED for internal protocol.
        /// Use IsValidUserAction() to check if an action number is valid, or use actions 0-65528.
        /// </summary>
        /// <param name="action">Action identifier (0-65528). Reserved actions (65529-65535) will be rejected.</param>
        /// <param name="data">Data to send</param>
        /// <param name="progress">Optional progress reporter (0.0 to 1.0)</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>True if data was sent successfully, false otherwise</returns>
        public async UniTask<bool> SendDataAsync(ushort action, byte[] data, IProgress<float> progress, CancellationToken token = default)
        {
            return await SendDataAsync(action, data, progress, token, isInternalCall: false);
        }

        /// <summary>
        /// Internal version with validation bypass for internal protocol use
        /// </summary>
        private async UniTask<bool> SendDataAsync(ushort action, byte[] data, IProgress<float> progress, CancellationToken token, bool isInternalCall)
        {
            if (!m_IsRunning) return false;

            // Validate action number - reject reserved actions (only for user calls)
            if (!isInternalCall && IsReservedAction(action))
            {
                var errorMsg = $"Action {action} is reserved for internal protocol. Valid range: 0-{RESERVED_ACTION_MIN - 1}";
                Log($"❌ {errorMsg}");
                ReportError(ErrorInfo.ErrorType.Protocol, errorMsg, null);
                return false;
            }

            // Handle null data by converting to empty byte array
            data ??= Array.Empty<byte>();

            byte[] message = CreatePacket(action, data);
            int retryCount = 0;
            bool success = false;

            // Log send attempt
            if (m_LogSend)
            {
                Log($"📤 Sending data: Action={action}, Size={data?.Length ?? 0} bytes");
            }

            while (retryCount <= m_MaxRetryCount && !success && !token.IsCancellationRequested)
            {
                try
                {
                    // Simple connection check - let TCP error handling manage the rest
                    if (!m_IsRunning)
                    {
                        throw new InvalidOperationException("Connection is not running");
                    }

                    if (m_IsServer)
                    {
                        if (m_Clients.Count == 0)
                        {
                            throw new InvalidOperationException("No clients connected");
                        }
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

                    // Log successful send
                    if (m_LogSend)
                    {
                        if (retryCount > 0)
                        {
                            Log($"✅ Data sent successfully after {retryCount} retries: Action={action}");
                        }
                        else
                        {
                            Log($"✅ Data sent successfully: Action={action}");
                        }
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Connection closed") ||
                                                           ex.Message.Contains("Stream disposed") ||
                                                           ex.Message.Contains("Connection is not healthy") ||
                                                           ex.Message.Contains("Client is not connected"))
                {
                    // Connection issues - don't retry, just exit gracefully
                    Log($"Send operation aborted: {ex.Message}");
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
                        if (m_LogSend)
                        {
                            Log($"❌ Send Data Failed after {m_MaxRetryCount} retries: Action={action}, Error={ex.Message}");
                        }
                        ReportError(ErrorInfo.ErrorType.DataTransmission, $"Data send failed after {m_MaxRetryCount} retries", ex);
                        break;
                    }
                    else
                    {
                        if (m_LogSend)
                        {
                            Log($"⚠️ Send Data Failed (attempt {retryCount}/{m_MaxRetryCount}), retrying in {m_RetryDelay}ms: Action={action}, Error={ex.Message}");
                        }
                        await UniTask.Delay(m_RetryDelay, cancellationToken: token);
                    }
                }
            }

            // Log final result if failed
            if (!success && m_LogSend)
            {
                Log($"❌ Send operation failed completely: Action={action}");
            }

            return success;
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
            List<TcpClient> clientsToSend = new List<TcpClient>();

            lock (m_Clients)
            {
                if (targetClientIds == null || targetClientIds.Length == 0)
                {
                    // Send to all connected clients - avoid LINQ for performance
                    foreach (var client in m_Clients.Keys)
                    {
                        if (client.Connected)
                        {
                            clientsToSend.Add(client);
                        }
                    }
                }
                else
                {
                    // Send to specific clients - avoid LINQ for performance
                    foreach (var kvp in m_Clients)
                    {
                        if (kvp.Key.Connected)
                        {
                            foreach (var targetId in targetClientIds)
                            {
                                if (kvp.Value.id == targetId)
                                {
                                    clientsToSend.Add(kvp.Key);
                                    break;
                                }
                            }
                        }
                    }
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

                    // Track sent bytes for this client
                    if (m_EnableMetrics)
                    {
                        lock (_metricsLock)
                        {
                            if (_clientMetrics.TryGetValue(client, out var metrics))
                            {
                                metrics.RecordSent(message.Length);
                            }
                        }
                    }

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
            
            // Log for large transfers
            bool isLargeTransfer = message.Length > m_BufferSize * 2;
            if (isLargeTransfer && m_LogSend)
            {
                Log($"📤 Sending large data: {message.Length:N0} bytes (will send in {Math.Ceiling((double)message.Length / chunkSize)} chunks)");
            }

            // Create timeout token source
            using var timeoutCts = new CancellationTokenSource(m_SendTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
            
            int lastLoggedProgress = 0;

            try
            {
                while (offset < message.Length && !linkedCts.Token.IsCancellationRequested)
                {
                    int bytesToSend = Math.Min(chunkSize, message.Length - offset);
                    await stream.WriteAsync(message, offset, bytesToSend, linkedCts.Token);
                    offset += bytesToSend;

                    // Report progress as percentage (0.0 to 1.0)
                    float progressPercentage = (float)offset / message.Length;
                    progress?.Report(progressPercentage);
                    
                    // Log progress for large transfers (every 25%)
                    if (isLargeTransfer && m_LogSend)
                    {
                        int currentProgress = (int)(progressPercentage * 100);
                        if (currentProgress >= lastLoggedProgress + 25 || offset >= message.Length)
                        {
                            lastLoggedProgress = currentProgress;
                            Log($"📤 Send progress: {currentProgress}% ({offset:N0}/{message.Length:N0} bytes)");
                        }
                    }

                    // Only yield on larger messages to reduce overhead
                    if (message.Length > m_BufferSize)
                    {
                        await UniTask.Yield();
                    }
                }

                // Ensure all data is sent with timeout
                await stream.FlushAsync(linkedCts.Token);

                // Track sent bytes in metrics (if tracking is enabled)
                if (m_EnableMetrics && message.Length > 0)
                {
                    lock (_metricsLock)
                    {
                        if (m_IsServer)
                        {
                            //  Note: For server→client sends, we track in per-client basis
                            // We need to determine which client this stream belongs to
                            // This is done by looking up stream's TcpClient in m_Clients
                            // However, we don't have direct access here. We'll track in SendAsServerAsync instead.
                        }
                        else
                        {
                            // Client mode: track sent to server
                            _serverMetrics?.RecordSent(message.Length);
                        }
                    }
                }

                // Log removed for performance - was logging every successful send operation
            }
            catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
            {
                // Timeout occurred
                throw new TimeoutException($"Send operation timed out after {m_SendTimeout}ms at offset {offset}/{message.Length}", ex);
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

        public async UniTask<bool> SendDataAsync<T>(ushort action, T data, CancellationToken token = default)
        {
            return await SendDataAsync(action, data, (IProgress<float>)null, token);
        }

        public async UniTask<bool> SendDataAsync<T>(ushort action, T data, MessagePriority priority, CancellationToken token = default)
        {
            return await SendDataAsync(action, data, null, priority, token);
        }

        public async UniTask<bool> SendDataAsync<T>(ushort action, T data, IProgress<float> progress, CancellationToken token = default)
        {
            return await SendDataAsync(action, data, progress, token, isInternalCall: false);
        }

        public async UniTask<bool> SendDataAsync<T>(ushort action, T data, IProgress<float> progress, MessagePriority priority, CancellationToken token = default)
        {
            byte[] serializedData = SerializeData(data);
            
            if (m_EnablePriorityQueue)
            {
                return await EnqueueMessage(action, serializedData, priority, null, progress, token);
            }
            else
            {
                return await SendDataAsync(action, serializedData, progress, token, isInternalCall: false);
            }
        }

        /// <summary>
        /// Internal version with validation bypass for internal protocol use
        /// </summary>
        private async UniTask<bool> SendDataAsync<T>(ushort action, T data, IProgress<float> progress, CancellationToken token, bool isInternalCall)
        {
            if (!m_IsRunning) return false;

            // Validate action number - reject reserved actions (only for user calls)
            if (!isInternalCall && IsReservedAction(action))
            {
                var errorMsg = $"Action {action} is reserved for internal protocol. Valid range: 0-{RESERVED_ACTION_MIN - 1}";
                Log($"❌ {errorMsg}");
                ReportError(ErrorInfo.ErrorType.Protocol, errorMsg, null);
                return false;
            }

            byte[] serializedData;
            try
            {
                if (data == null)
                {
                    // Send empty data for null input
                    serializedData = Array.Empty<byte>();
                }
                else
                {
                    // Handle primitive types and basic types directly
                    serializedData = SerializeData(data);
                }
            }
            catch (Exception ex)
            {
                Log($"Serialization Error: {ex.Message}");
                ReportError(ErrorInfo.ErrorType.Serialization, "Data serialization failed", ex);
                return false;
            }

            return await SendDataAsync(action, serializedData, progress, token, isInternalCall);
        }

        /// <summary>
        /// Sends a relay message to other clients via server.
        /// Client-only method - the server will forward the message to target clients.
        /// </summary>
        /// <param name="targetClientIds">Target client IDs to send to. Null or empty for broadcast to all other clients.</param>
        /// <param name="action">The action ID for the message</param>
        /// <param name="data">Message data (will be serialized to JSON)</param>
        public async UniTask<bool> SendRelayMessageAsync<T>(int[] targetClientIds, ushort action, T data, CancellationToken token = default)
        {
            if (m_IsServer)
            {
                Log("SendRelayMessageAsync is only available for clients. Use SendDataToClientsAsync for server.");
                return false;
            }

            if (!m_IsConnected)
            {
                Log("Cannot send relay message - not connected to server");
                return false;
            }

            // Validate action number - reject reserved actions
            if (IsReservedAction(action))
            {
                var errorMsg = $"Action {action} is reserved for internal protocol. Valid range: 0-{RESERVED_ACTION_MIN - 1}";
                Log($"❌ {errorMsg}");
                ReportError(ErrorInfo.ErrorType.Protocol, errorMsg, null);
                return false;
            }

            try
            {
                string jsonData = data == null ? "" : JsonConvert.SerializeObject(data);
                var relayMessage = new RelayMessage(m_MyClientId, targetClientIds, action, jsonData);
                return await SendDataAsync(ACTION_RELAY_MESSAGE, relayMessage, null, token, isInternalCall: true);
            }
            catch (Exception ex)
            {
                Log($"Failed to send relay message: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sends a relay message to a specific client via server.
        /// </summary>
        public async UniTask<bool> SendRelayMessageToClientAsync<T>(int targetClientId, ushort action, T data, CancellationToken token = default)
        {
            return await SendRelayMessageAsync(new[] { targetClientId }, action, data, token);
        }

        /// <summary>
        /// Broadcasts a relay message to all other clients via server.
        /// </summary>
        public async UniTask<bool> BroadcastRelayMessageAsync<T>(ushort action, T data, CancellationToken token = default)
        {
            return await SendRelayMessageAsync(null, action, data, token);
        }

        /// <summary>
        /// Serializes data based on type - primitives directly, complex objects via JSON
        /// </summary>
        private byte[] SerializeData<T>(T data)
        {
            try
            {
                byte[] bytes = UnityConverter.ToBytes(data);
                // Log removed for performance - was logging every serialization
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

            jsonString = JsonConvert.SerializeObject(data);

            var bytes = Encoding.UTF8.GetBytes(jsonString);
            // Log removed for performance - was logging every JSON serialization
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
        /// <returns>True if data was sent successfully to at least one target client, false otherwise</returns>
        public async UniTask<bool> SendDataToClientsAsync(ushort action, byte[] data, int[] targetClientIds, CancellationToken token = default)
        {
            return await SendDataToClientsAsync(action, data, targetClientIds, null, token);
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
        /// <returns>True if data was sent successfully to at least one target client, false otherwise</returns>
        public async UniTask<bool> SendDataToClientsAsync(ushort action, byte[] data, int[] targetClientIds, IProgress<float> progress, CancellationToken token = default)
        {
            return await SendDataToClientsAsync(action, data, targetClientIds, progress, token, isInternalCall: false);
        }

        /// <summary>
        /// Internal version with validation bypass for internal protocol use
        /// </summary>
        private async UniTask<bool> SendDataToClientsAsync(ushort action, byte[] data, int[] targetClientIds, IProgress<float> progress, CancellationToken token, bool isInternalCall)
        {
            if (!m_IsRunning) return false;


            // Only servers can target specific clients
            if (!m_IsServer)
            {
                Log("SendDataToClientsAsync is only available for servers");
                return false;
            }

            if (targetClientIds == null || targetClientIds.Length == 0)
            {
                Log("No target client IDs specified - use SendDataAsync for broadcasting to all clients");
                return false;
            }

            // Validate action number - reject reserved actions (only for user calls)
            if (!isInternalCall && IsReservedAction(action))
            {
                var errorMsg = $"Action {action} is reserved for internal protocol. Valid range: 0-{RESERVED_ACTION_MIN - 1}";
                Log($"❌ {errorMsg}");
                ReportError(ErrorInfo.ErrorType.Protocol, errorMsg, null);
                return false;
            }

            // Server-side targeting logic
            byte[] message = CreatePacket(action, data);
            int retryCount = 0;
            bool success = false;

            while (retryCount <= m_MaxRetryCount && !success && !token.IsCancellationRequested)
            {
                try
                {
                    // Simple connection check - let TCP error handling manage the rest
                    if (!m_IsRunning)
                    {
                        throw new InvalidOperationException("Connection is not running");
                    }

                    if (m_Clients.Count == 0)
                    {
                        throw new InvalidOperationException("No clients connected");
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

            return success;
        }

        /// <summary>
        /// Server-only: Sends serialized object to specific clients by their IDs
        /// This method is only effective when called from a server instance
        /// </summary>
        /// <returns>True if data was sent successfully to at least one target client, false otherwise</returns>
        public async UniTask<bool> SendDataToClientsAsync<T>(ushort action, T data, int[] targetClientIds, CancellationToken token = default)
        {
            return await SendDataToClientsAsync(action, data, targetClientIds, null, token);
        }

        /// <summary>
        /// Server-only: Sends serialized object to specific clients by their IDs with progress reporting
        /// This method is only effective when called from a server instance
        /// </summary>
        /// <returns>True if data was sent successfully to at least one target client, false otherwise</returns>
        public async UniTask<bool> SendDataToClientsAsync<T>(ushort action, T data, int[] targetClientIds, IProgress<float> progress, CancellationToken token = default)
        {
            return await SendDataToClientsAsync(action, data, targetClientIds, progress, token, isInternalCall: false);
        }

        /// <summary>
        /// Internal version with validation bypass for internal protocol use
        /// </summary>
        private async UniTask<bool> SendDataToClientsAsync<T>(ushort action, T data, int[] targetClientIds, IProgress<float> progress, CancellationToken token, bool isInternalCall)
        {
            if (!m_IsRunning) return false;

            // Only servers can target specific clients
            if (!m_IsServer)
            {
                Log("SendDataToClientsAsync is only available for servers");
                return false;
            }

            if (targetClientIds == null || targetClientIds.Length == 0)
            {
                Log("No target client IDs specified - use SendDataAsync for broadcasting to all clients");
                return false;
            }

            // Validate action number - reject reserved actions (only for user calls)
            if (!isInternalCall && IsReservedAction(action))
            {
                var errorMsg = $"Action {action} is reserved for internal protocol. Valid range: 0-{RESERVED_ACTION_MIN - 1}";
                Log($"❌ {errorMsg}");
                ReportError(ErrorInfo.ErrorType.Protocol, errorMsg, null);
                return false;
            }

            byte[] serializedData;
            try
            {
                if (data == null)
                {
                    // Send empty data for null input
                    serializedData = Array.Empty<byte>();
                }
                else
                {
                    serializedData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
                }
            }
            catch (Exception ex)
            {
                Log($"Serialization Error: {ex.Message}");
                ReportError(ErrorInfo.ErrorType.Serialization, "Data serialization failed", ex);
                return false;
            }

            return await SendDataToClientsAsync(action, serializedData, targetClientIds, progress, token, isInternalCall);
        }

        /// <summary>
        /// Creates a TCP packet with length prefix for stream protocol
        /// </summary>
        private byte[] CreatePacket(ushort action, byte[] data)
        {
            // Get the core packet structure
            byte[] packet = CreateCorePacket(action, data);

            // Encrypt packet if encryption is enabled
            if (m_EnableEncryption)
            {
                packet = EncryptData(packet);
            }

            // TCP needs length prefix for stream protocol
            byte[] lengthPrefix = BitConverter.GetBytes(packet.Length);
            byte[] finalMessage = new byte[4 + packet.Length];

            Buffer.BlockCopy(lengthPrefix, 0, finalMessage, 0, 4);
            Buffer.BlockCopy(packet, 0, finalMessage, 4, packet.Length);

            // Log removed for performance - was logging every packet creation
            return finalMessage;
        }

        /// <summary>
        /// Reads TCP packet (assumes length prefix has already been processed)
        /// </summary>
        private PacketResponse ReadPacket(byte[] data, int dataLength, ConnectorInfo connectorInfo)
        {
            // Decrypt packet if encryption is enabled
            if (m_EnableEncryption)
            {
                data = DecryptData(data);
                dataLength = data.Length;
            }

            // TCP packets don't include the length prefix in the data buffer passed here
            // The length prefix is processed separately in the receive loop
            return ReadCorePacket(data, dataLength, connectorInfo);
        }

        /// <summary>
        /// Switch to main thread with retry and exponential backoff
        /// </summary>
        private async UniTask SwitchToMainThreadWithRetry(int maxRetries = 3)
        {
            int retryCount = 0;
            while (retryCount < maxRetries)
            {
                try
                {
                    await UniTask.SwitchToMainThread();
                    return; // Success
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        Log($"Failed to switch to main thread after {maxRetries} attempts: {ex.Message}");
                        throw;
                    }
                    
                    // Exponential backoff: 10ms, 20ms, 40ms
                    int delayMs = 10 * (1 << retryCount);
                    await UniTask.Delay(delayMs);
                }
            }
        }

        #endregion

        #region Nested Classes & Enums

        /// <summary>
        /// Connection state for handshake protocol
        /// </summary>
        public enum ConnectionState
        {
            Pending,      // Initial state after TCP connection
            HelloSent,    // HELLO message sent, waiting for ACK
            Acknowledged, // ACK received, waiting for READY
            Ready         // Handshake complete, connection ready for user data
        }

        /// <summary>
        /// Message priority levels for priority queue system
        /// </summary>
        public enum MessagePriority
        {
            Critical = 0,  // Highest priority (e.g., connection control, critical game state)
            High = 1,      // High priority (e.g., player actions, important events)
            Normal = 2,    // Normal priority (e.g., regular game updates)
            Low = 3        // Low priority (e.g., chat messages, non-critical data)
        }

        /// <summary>
        /// Queued message with priority information
        /// </summary>
        private class QueuedMessage
        {
            public ushort Action;
            public byte[] Data;
            public int[] TargetClientIds;
            public IProgress<float> Progress;
            public CancellationToken Token;
            public UniTaskCompletionSource<bool> CompletionSource;
        }

        /// <summary>
        /// Contains connection information for a client or server.
        /// The 'id' field is used as PacketResponse.senderId:
        /// - For TCP connections: id is set when connection is established
        /// - For Real-time UDP: id = IPEndPoint.GetHashCode()
        /// </summary>
        [Serializable]
        public class TokenBucket
        {
            private double _tokens;
            private readonly double _maxTokens;
            private readonly double _refillRate; // tokens per second
            private DateTime _lastRefill;
            private readonly object _lock = new object();

            public TokenBucket(double maxTokens, double refillRate)
            {
                _maxTokens = maxTokens;
                _refillRate = refillRate;
                _tokens = maxTokens; // Start with full bucket
                _lastRefill = DateTime.UtcNow;
            }

            /// <summary>
            /// Try to consume tokens. Returns true if successful, false if rate limit exceeded.
            /// </summary>
            public bool TryConsume(int count = 1)
            {
                lock (_lock)
                {
                    Refill();

                    if (_tokens >= count)
                    {
                        _tokens -= count;
                        return true;
                    }

                    return false; // Rate limit exceeded
                }
            }

            /// <summary>
            /// Refill tokens based on elapsed time
            /// </summary>
            private void Refill()
            {
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastRefill).TotalSeconds;
                _lastRefill = now;

                _tokens = Math.Min(_maxTokens, _tokens + (_refillRate * elapsed));
            }

            /// <summary>
            /// Get current token count (for diagnostics)
            /// </summary>
            public double GetCurrentTokens()
            {
                lock (_lock)
                {
                    Refill();
                    return _tokens;
                }
            }
        }

        [Serializable]
        public class ConnectionMetrics
        {
            public int ClientId;
            public long BytesSent;
            public long BytesReceived;
            public long MessagesSent;
            public long MessagesReceived;
            public double ConnectionUptimeSeconds;
            public double AverageSendRate; // bytes per second
            public double AverageReceiveRate; // bytes per second
            public DateTime ConnectionStartTime;
            public DateTime LastActivityTime;

            public ConnectionMetrics(int clientId)
            {
                ClientId = clientId;
                ConnectionStartTime = DateTime.UtcNow;
                LastActivityTime = DateTime.UtcNow;
            }

            public void RecordSent(int bytes)
            {
                BytesSent += bytes;
                MessagesSent++;
                LastActivityTime = DateTime.UtcNow;
            }

            public void RecordReceived(int bytes)
            {
                BytesReceived += bytes;
                MessagesReceived++;
                LastActivityTime = DateTime.UtcNow;
            }

            public void UpdateRates()
            {
                ConnectionUptimeSeconds = (DateTime.UtcNow - ConnectionStartTime).TotalSeconds;
                if (ConnectionUptimeSeconds > 0)
                {
                    AverageSendRate = BytesSent / ConnectionUptimeSeconds;
                    AverageReceiveRate = BytesReceived / ConnectionUptimeSeconds;
                }
            }
        }

        [Serializable] public struct ConnectorInfo { public int id; public string ipAddress; public int port; [JsonIgnore] public IPEndPoint remoteEndPoint; }

        [Serializable]
        public class PacketResponse
        {
            public enum ReceiveStatus { None, Receiving, Received }

            public int messageId;
            /// <summary>
            /// ID of the sender. This value comes from ConnectorInfo.id:
            /// - For TCP: ConnectorInfo.id is set when connection is established
            /// - For UDP: ConnectorInfo.id = IPEndPoint.GetHashCode()
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
                        return JsonConvert.DeserializeObject<T>(UnityConverter.GetString(data));

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
        public class SyncedClientList
        {
            public int yourId; // The recipient client's own ID (for self-filtering)
            public List<ConnectorInfo> clients;

            public SyncedClientList()
            {
                yourId = -1;
                clients = new List<ConnectorInfo>();
            }

            public SyncedClientList(int recipientId, List<ConnectorInfo> clientList)
            {
                yourId = recipientId;
                clients = clientList ?? new List<ConnectorInfo>();
            }
        }

        /// <summary>
        /// Message structure for client-to-client relay via server
        /// </summary>
        [Serializable]
        public class RelayMessage
        {
            public int senderId;      // Original sender's client ID
            public int[] targetIds;   // Target client IDs (null or empty = broadcast to all)
            public ushort action;     // Original action
            public string data;       // Message data (JSON string)

            public RelayMessage()
            {
                senderId = -1;
                targetIds = null;
                action = 0;
                data = "";
            }

            public RelayMessage(int sender, int[] targets, ushort actionId, string messageData)
            {
                senderId = sender;
                targetIds = targets;
                action = actionId;
                data = messageData;
            }
        }

        /// <summary>
        /// Broadcasts client list synchronization to all connected clients.
        /// Server-only method.
        /// </summary>
        /// <param name="excludeClientId">Client ID to exclude from broadcast (typically newly joined client for initial sync)</param>
        /// <param name="isFullSync">If true, sends full client list. If false, assumes this is a disconnect notification.</param>
        private async UniTask BroadcastClientListSync(int excludeClientId = -1, bool isFullSync = true)
        {
            if (!m_IsServer) return;

            List<ConnectorInfo> currentClients;
            lock (m_Clients)
            {
                currentClients = m_Clients.Values.ToList();
            }

            // Update server's own synced list
            lock (_syncedClientListLock)
            {
                m_SyncedClientList = new List<ConnectorInfo>(currentClients);
            }

            ushort action = ACTION_CLIENT_LIST_FULL;

            // Send personalized sync data to each client (with their own ID)
            foreach (var client in currentClients)
            {
                // Create personalized sync payload with client's own ID
                var syncData = new SyncedClientList(client.id, currentClients);

                try
                {
                    await SendDataToClientsAsync(action, syncData, new[] { client.id }, null, default, isInternalCall: true);
                    if (client.id == excludeClientId)
                    {
                        Log($"Sent initial full client list to new client {client.id}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"Failed to send sync to client {client.id}: {ex.Message}");
                }
            }

            Log($"Broadcasted client list sync to {currentClients.Count} clients");

            // Fire local event on main thread
            try
            {
                await UniTask.SwitchToMainThread();
                m_OnClientListUpdated?.Invoke(new List<ConnectorInfo>(currentClients));
            }
            catch (Exception ex)
            {
                Log($"Warning: Could not invoke OnClientListUpdated event: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes client sync messages received from server.
        /// Client-only method.
        /// </summary>
        private async UniTask ProcessClientSyncMessage(PacketResponse packet)
        {
            if (m_IsServer) return;

            // Check if this is a sync action
            if (packet.action != ACTION_CLIENT_LIST_FULL &&
                packet.action != ACTION_CLIENT_JOINED &&
                packet.action != ACTION_CLIENT_LEFT)
            {
                return; // Not a sync message
            }

            try
            {
                // Deserialize the client list
                var syncData = packet.GetData<SyncedClientList>();
                if (syncData == null || syncData.clients == null)
                {
                    Log("Received invalid client sync data");
                    return;
                }

                // Store client's own server-assigned ID
                m_MyClientId = syncData.yourId;

                // Update local synced list
                lock (_syncedClientListLock)
                {
                    m_SyncedClientList = new List<ConnectorInfo>(syncData.clients);
                }

                Log($"Client list synced: {syncData.clients.Count} clients connected (my ID: {m_MyClientId})");

                // Fire event on main thread
                try
                {
                    await UniTask.SwitchToMainThread();
                    m_OnClientListUpdated?.Invoke(new List<ConnectorInfo>(syncData.clients));
                }
                catch (Exception ex)
                {
                    Log($"Warning: Could not invoke OnClientListUpdated event: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to process client sync message: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes relay messages on server side - forwards messages to target clients
        /// Server-only method.
        /// </summary>
        private async UniTask ProcessRelayMessage(PacketResponse packet, ConnectorInfo senderInfo)
        {
            if (!m_IsServer) return;
            if (packet.action != ACTION_RELAY_MESSAGE) return;

            try
            {
                var relayData = packet.GetData<RelayMessage>();
                if (relayData == null)
                {
                    Log("Received invalid relay message data");
                    return;
                }

                // Set sender ID from connection info
                relayData.senderId = senderInfo.id;

                int[] targetIds;
                if (relayData.targetIds == null || relayData.targetIds.Length == 0)
                {
                    // Broadcast to all clients except sender
                    lock (m_Clients)
                    {
                        targetIds = m_Clients.Values
                            .Where(c => c.id != senderInfo.id)
                            .Select(c => c.id)
                            .ToArray();
                    }
                }
                else
                {
                    // Send to specific targets (excluding sender)
                    targetIds = relayData.targetIds.Where(id => id != senderInfo.id).ToArray();
                }

                if (targetIds.Length > 0)
                {
                    await SendDataToClientsAsync(ACTION_RELAY_MESSAGE, relayData, targetIds);
                    Log($"Relayed message from client {senderInfo.id} to {targetIds.Length} clients, action={relayData.action}");
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to process relay message: {ex.Message}");
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



        /// <summary>
        /// Simple method to check if server/client is ready
        /// </summary>
        public bool IsReady()
        {
            if (m_IsServer)
                return m_IsRunning && m_Clients.Count > 0;
            else
                return m_IsRunning && m_IsConnected;
        }




        #region Event Handlers
        public IUniTaskAsyncEnumerable<PacketResponse> OnDataReceived(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<PacketResponse>(m_OnDataReceived, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnClientConnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnClientConnected, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnClientDisconnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnClientDisconnected, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnServerConnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnServerConnected, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnServerDisconnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnServerDisconnected, token);
        public IUniTaskAsyncEnumerable<List<ConnectorInfo>> OnClientListUpdated(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<List<ConnectorInfo>>(m_OnClientListUpdated, token);

        public IUniTaskAsyncEnumerable<ErrorInfo> OnError(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ErrorInfo>(m_OnError, token);

        #endregion

        #region Error Reporting

        private void ReportError(ErrorInfo.ErrorType errorType, string message, Exception exception = null, ConnectorInfo? connectorInfo = null, string operationId = null)
        {
            var errorInfo = new ErrorInfo(errorType, message, exception, connectorInfo, operationId);

            // Just report via event - let the user decide how to handle errors
            // This allows for better separation of concerns and flexibility
            try
            {
                m_OnError?.Invoke(errorInfo);
            }
            catch (Exception ex)
            {
                // Only fallback to Debug.LogError if event invocation fails
                // This prevents infinite recursion and ensures errors are not lost
                if (m_IsDebug)
                {
                    var prefix = m_IsServer ? "Server" : "Client";
                    Debug.LogError($"[TCP-{prefix}] Failed to invoke error event: {ex.Message}");
                    Debug.LogError($"[TCP-{prefix}] Original error - {errorType}: {message}");
                }
            }
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
        private bool performanceExpanded = false;
        private bool securityExpanded = false;
        private bool healthExpanded = false;
        private bool advancedExpanded = false;

        // Force inspector to repaint during play mode to show live status updates
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

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
                var isDebug = serializedObject.FindProperty("m_IsDebug");
                EditorGUILayout.PropertyField(isDebug);

                if (isDebug.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_LogSend"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_LogReceive"));
                }

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

            // --- Security Box ---
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;

            securityExpanded = EditorGUILayout.Foldout(securityExpanded, "Security Settings", true);

            if (securityExpanded)
            {
                EditorGUILayout.LabelField("Encryption", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_EnableEncryption"));
                var enableEncryption = serializedObject.FindProperty("m_EnableEncryption");
                if (enableEncryption.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_EncryptionKey"));
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Authentication", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_EnableAuthentication"));
                var enableAuth = serializedObject.FindProperty("m_EnableAuthentication");
                if (enableAuth.boolValue && isServer.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_AuthPassword"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_AuthTimeout"));
                }
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();

            // --- Connection Health Box ---
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;

            healthExpanded = EditorGUILayout.Foldout(healthExpanded, "Connection Health", true);

            if (healthExpanded)
            {
                EditorGUILayout.LabelField("Heartbeat", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_EnableHeartbeat"));
                var enableHeartbeat = serializedObject.FindProperty("m_EnableHeartbeat");
                if (enableHeartbeat.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_HeartbeatInterval"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_HeartbeatTimeout"));
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Auto Reconnection", EditorStyles.boldLabel);
                if (!isServer.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_EnableAutoReconnect"));
                    var enableReconnect = serializedObject.FindProperty("m_EnableAutoReconnect");
                    if (enableReconnect.boolValue)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ReconnectMaxAttempts"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ReconnectDelays"));
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Auto-reconnection is only available in Client mode", MessageType.Info);
                }
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();

            // --- Advanced Features Box ---
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;

            advancedExpanded = EditorGUILayout.Foldout(advancedExpanded, "Advanced Features", true);

            if (advancedExpanded)
            {
                EditorGUILayout.LabelField("Connection Metrics", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_EnableMetrics"));
                var enableMetrics = serializedObject.FindProperty("m_EnableMetrics");
                if (enableMetrics.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_MetricsReportInterval"));
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Rate Limiting", EditorStyles.boldLabel);
                if (isServer.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_EnableRateLimit"));
                    var enableRateLimit = serializedObject.FindProperty("m_EnableRateLimit");
                    if (enableRateLimit.boolValue)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_RateLimit"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_BurstSize"));
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Rate limiting is only available in Server mode", MessageType.Info);
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Message Priority Queues", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_EnablePriorityQueue"));
                var enablePriorityQueue = serializedObject.FindProperty("m_EnablePriorityQueue");
                if (enablePriorityQueue.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_QueueMaxSize"));
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Graceful Shutdown", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_EnableGracefulShutdown"));
                var enableGracefulShutdown = serializedObject.FindProperty("m_EnableGracefulShutdown");
                if (enableGracefulShutdown.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ShutdownTimeout"));
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Protocol Version Checking", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_EnableVersionCheck"));
                var enableVersionCheck = serializedObject.FindProperty("m_EnableVersionCheck");
                if (enableVersionCheck.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ProtocolVersion"));
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
                if (!isServer.boolValue)
                {
                    EditorGUILayout.Toggle("Is Connected", isConnectedProp.boolValue);

                    // Show connected server info and synced client list for client mode
                    if (tcpConnector.IsConnected)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Connected Server:", EditorStyles.miniLabel);
                        var serverInfo = tcpConnector.ServerInfo;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"• Server: {serverInfo.ipAddress}:{serverInfo.port}", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                        GUI.backgroundColor = Color.cyan;
                        if (GUILayout.Button("Send", GUILayout.Width(50)))
                        {
                            // Send message directly to server
                            tcpConnector.SendDataAsync(testAction, testMessage).Forget();
                            Debug.Log($"[TCPConnector] Sent message to server: Action={testAction}, Message='{testMessage}'");
                        }
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();

                        // Show client's own ID
                        if (tcpConnector.MyClientId != -1)
                        {
                            EditorGUILayout.LabelField($"My Client ID: {tcpConnector.MyClientId}", EditorStyles.miniLabel);
                        }

                        // Show synced client list (other clients connected to same server, excluding self)
                        var syncedClients = tcpConnector.GetAllConnectedClientsInfo();
                        var otherClients = syncedClients.Where(c => c.id != tcpConnector.MyClientId).ToList();
                        if (otherClients.Count > 0)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField($"Other Connected Clients ({otherClients.Count}):", EditorStyles.miniLabel);
                            foreach (var client in otherClients)
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField($"• ID: {client.id}: {client.ipAddress}:{client.port}", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                                GUI.backgroundColor = Color.yellow;
                                if (GUILayout.Button("Send", GUILayout.Width(50)))
                                {
                                    // Send relay message to specific client via server
                                    tcpConnector.SendRelayMessageToClientAsync(client.id, testAction, testMessage).Forget();
                                    Debug.Log($"[TCPConnector] Sent relay message to client {client.id}: Action={testAction}, Message='{testMessage}'");
                                }
                                GUI.backgroundColor = Color.white;
                                EditorGUILayout.EndHorizontal();
                            }

                            // Broadcast to All other clients button
                            EditorGUILayout.Space();
                            GUI.backgroundColor = Color.green;
                            if (GUILayout.Button("📢 Broadcast to All Other Clients"))
                            {
                                // Broadcast relay message to all other clients via server
                                tcpConnector.BroadcastRelayMessageAsync(testAction, testMessage).Forget();
                                Debug.Log($"[TCPConnector] Broadcast relay message to {otherClients.Count} clients: Action={testAction}, Message='{testMessage}'");
                            }
                            GUI.backgroundColor = Color.white;
                        }
                    }
                }

                if (isServer.boolValue && tcpConnector.ConnectedClientCount > 0)
                {
                    EditorGUILayout.LabelField($"TCP Clients: {tcpConnector.ConnectedClientCount}");

                    // Show client details with individual Send buttons
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Clients:", EditorStyles.miniLabel);
                    var clientList = tcpConnector.GetClientInfoList();
                    foreach (var client in clientList)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"• ID: {client.id}: {client.ipAddress}:{client.port}", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                        GUI.backgroundColor = Color.yellow;
                        if (GUILayout.Button("Send", GUILayout.Width(50)))
                        {
                            tcpConnector.SendDataToClientsAsync(testAction, testMessage, new[] { client.id }).Forget();
                            Debug.Log($"[TCPConnector] Sent message to client {client.id}: Action={testAction}, Message='{testMessage}'");
                        }
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();
                    }

                    // Broadcast to All button
                    EditorGUILayout.Space();
                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button("📢 Broadcast to All Clients"))
                    {
                        int[] allClientIds = clientList.Select(c => c.id).ToArray();
                        tcpConnector.SendDataToClientsAsync(testAction, testMessage, allClientIds).Forget();
                        Debug.Log($"[TCPConnector] Broadcast message to {allClientIds.Length} clients: Action={testAction}, Message='{testMessage}'");
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndVertical();

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
                EditorGUILayout.LabelField("Connection Events", EditorStyles.boldLabel);
                if (isServer.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnClientConnected"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnClientDisconnected"));
                }
                else
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnServerConnected"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnServerDisconnected"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnReconnecting"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnReconnected"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnReconnectFailed"));
                }
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Data Events", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnDataReceived"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnClientListUpdated"));
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Security Events", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnAuthSuccess"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnAuthFailed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnVersionMismatch"));
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("System Events", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnError"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnMetricsUpdated"));
                if (isServer.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnRateLimitExceeded"));
                }
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif