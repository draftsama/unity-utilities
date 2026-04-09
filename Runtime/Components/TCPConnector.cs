using System;
using System.Collections.Concurrent;
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
        [SerializeField, Tooltip("Maximum allowed size of a single received packet in megabytes (default 256 MB)")]
        public int m_MaxPacketSizeMB = 256; // MB

        [Header("Retry Settings")]
        [SerializeField, Range(0, 10), Tooltip("Maximum retry attempts")]
        public int m_MaxRetryCount = 3;
        [SerializeField, Range(0, 3000), Tooltip("Delay between retries (ms)")]
        public int m_RetryDelay = 1000;

        [Header("Auto Reconnection (Client Mode)")]
        [SerializeField, Tooltip("Enable automatic reconnection when connection is lost (client mode only)")]
        public bool m_EnableAutoReconnect = true;
        [SerializeField, Range(1, 20), Tooltip("Maximum reconnection attempts (0 = infinite)")]
        public int m_ReconnectMaxAttempts = 5;
        [SerializeField, Tooltip("Reconnection delay pattern in milliseconds (exponential backoff)")]
        public int[] m_ReconnectDelays = new int[] { 1000, 2000, 5000, 10000, 30000 }; // 1s, 2s, 5s, 10s, 30s

        [Header("Auto Discovery (UDP Broadcast)")]
        public bool m_EnableDiscovery = true;

        [SerializeField, Range(0, 5000), Tooltip("Interval between discovery broadcasts (milliseconds)")]
        public int m_DiscoveryInterval = 1000; // milliseconds
        [SerializeField, Tooltip("UDP port for server discovery broadcasts")]
        public int m_DiscoveryPort = 54322;
        [SerializeField, Tooltip("Message string sent during server discovery")]
        public string m_DiscoveryMessage = "DiscoverServer";

        // --- Status ---
        [Header("Status (Read Only)")]
        [SerializeField] private volatile bool m_IsRunning = false;
        [SerializeField] private volatile bool m_IsConnected = false;

        // --- TCP Components ---
        private TcpListener _tcpListener; // Server
        private TcpClient _tcpClient;     // Client
        private readonly ConcurrentDictionary<TcpClient, ConnectorInfo> m_Clients = new ConcurrentDictionary<TcpClient, ConnectorInfo>();

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

        private const ushort ACTION_DISCONNECT_NOTIFICATION = 65524;

        // --- Client List Tracking ---
        private List<ConnectorInfo> m_SyncedClientList = new List<ConnectorInfo>();
        private readonly object _syncedClientListLock = new object();
        private int m_MyClientId = -1; // Client's own server-assigned ID (received from server sync)

        // --- Client ID Generation ---
        private static int _nextClientId = 0; // Thread-safe sequential ID counter

        // --- Reconnection Tracking ---
        private int _reconnectAttempts = 0;
        private bool _isReconnecting = false;

        // --- Stream Write Synchronization (v2.7.3: Fix concurrent write race condition) ---
        private readonly ConcurrentDictionary<TcpClient, SemaphoreSlim> _streamSendLocks = new ConcurrentDictionary<TcpClient, SemaphoreSlim>();

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
            // ConcurrentDictionary iteration is thread-safe
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
        /// Gets or creates a SemaphoreSlim for thread-safe stream writes (v2.7.3)
        /// Prevents race conditions when multiple threads write to the same NetworkStream
        /// Uses ConcurrentDictionary.GetOrAdd for atomic thread-safe creation
        /// </summary>
        private SemaphoreSlim GetOrCreateSendLock(TcpClient client)
        {
            if (client == null) return null;

            // GetOrAdd is atomic - prevents race condition where multiple threads
            // could create semaphores for the same client
            return _streamSendLocks.GetOrAdd(client, _ => new SemaphoreSlim(1, 1));
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
            _cachedName = name;
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
            // Ensure connection is stopped
            StopConnection();

            // Dispose all semaphores in the send locks dictionary
            foreach (var semaphore in _streamSendLocks.Values)
            {
                try
                {
                    semaphore?.Dispose();
                }
                catch (Exception ex)
                {
                    Log($"Error disposing semaphore: {ex.Message}");
                }
            }
            _streamSendLocks.Clear();

            // Clear buffer pool
            lock (_bufferPoolLock)
            {
                _bufferPool.Clear();
            }

            // Clean up cancellation token
            _cts?.Dispose();
            _cts = null;
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

        /// <summary>
        /// Checks if a TCP port is already in use by another process.
        /// </summary>
        private bool IsPortInUse(int port)
        {
            try
            {
                using var testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                testSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
                testSocket.Bind(new IPEndPoint(IPAddress.Any, port));
                return false; // Port is available
            }
            catch (SocketException)
            {
                return true; // Port is in use
            }
        }

        public async UniTask StartConnection()
        {
            if (m_IsRunning) return;

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
                try
                {
                    await SwitchToMainThreadWithRetry();
                    m_IsRunning = false;
                }
                catch { /* Ignore main thread switch errors during cleanup */ }
                ReportError(ErrorInfo.ErrorType.Connection, $"Failed to start connection: {ex.Message}", ex);
            }
            finally
            {
                try
                {
                    await SwitchToMainThreadWithRetry();
                    m_IsRunning = false;
                }
                catch { /* Ignore main thread switch errors during cleanup */ }
            }
        }

        public void StopConnection()
        {
            // Call async implementation and forget (events will be fired on main thread)
            StopConnectionAsync().Forget();
        }

        private async UniTask StopConnectionAsync()
        {
            m_IsRunning = false;

            _cts?.Cancel();

            // Stop TCP listener (server only)
            _tcpListener?.Stop();

            // Close TCP client (client only)
            _tcpClient?.Close();

            // Close all client connections (server only)
            // ConcurrentDictionary: get snapshot of keys, then iterate safely
            var clientsArray = m_Clients.Keys.ToArray();
            foreach (var client in clientsArray)
            {
                client?.Close();
            }
            m_Clients.Clear();

            // Handle connection state - MUST be on main thread for Unity serialization
            if (m_IsConnected)
            {
                await SwitchToMainThreadWithRetry();
                m_IsConnected = false;
                m_OnServerDisconnected?.Invoke(m_ServerInfo);
            }

            _cts?.Dispose();
            _cts = null;
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

                // Check if port is already in use by another process
                if (IsPortInUse(m_Port))
                {
                    Log($"⚠️ Port {m_Port} is currently in use by another process. Attempting to bind with ReuseAddress...");
                }

                // Start UDP broadcast for discovery if enabled
                if (m_EnableDiscovery)
                {
                    BroadcastPresenceAsync(token).Forget();
                }

                _tcpListener = new TcpListener(IPAddress.Any, m_Port);
                _tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _tcpListener.ExclusiveAddressUse = false;

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
                    Log($"❌ Access denied for port {m_Port}. Check: 1) Windows Firewall allows this app, 2) No other process is using this port (netstat -ano | findstr {m_Port}), 3) Port is not in Windows excluded range (netsh interface ipv4 show excludedportrange protocol=tcp), 4) Try running as Administrator");
                    ReportError(ErrorInfo.ErrorType.Connection, $"Access denied for port {m_Port}. Check Windows Firewall, other processes using this port, or try running as Administrator.", ex);
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
                try
                {
                    await SwitchToMainThreadWithRetry();
                    m_IsRunning = false;
                }
                catch { /* Ignore main thread switch errors during cleanup */ }
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

            var stream = client.GetStream();
            bool handshakeComplete = false;

            try
            {
                // --- Handshake Step 1: Send HELLO ---
                lock (_handshakeLock)
                {
                    _pendingConnections[client] = ConnectionState.HelloSent;
                }

                var (helloPacket, helloLength) = CreatePacket(ACTION_CONNECTION_HELLO, Array.Empty<byte>());
                await SendDataToStreamAsync(stream, helloPacket, helloLength, null, token, client);

                // --- Handshake Step 2: Wait for ACK ---
                string ackKey = $"ack_{clientInfo.id}";
                UniTaskCompletionSource<PacketResponse> ackWaiter;
                lock (_handshakeLock)
                {
                    ackWaiter = new UniTaskCompletionSource<PacketResponse>();
                    _handshakeWaiters[ackKey] = ackWaiter;
                }

                // Start receive loop BEFORE waiting for ACK (critical for handshake)
                var receiveLoopTask = ReceiveDataLoopAsync(stream, clientInfo, token, client);

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

                // --- Handshake Step 3: Send client list sync ---
                // Add to m_Clients (ConcurrentDictionary handles thread-safety)
                m_Clients[client] = clientInfo;
                
                // Update server's synced list after addition
                // Create snapshot first to avoid nested locks
                var clientSnapshot = m_Clients.Values.ToList();
                lock (_syncedClientListLock)
                {
                    m_SyncedClientList = clientSnapshot;
                }
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
                            
                            // Remove client (ConcurrentDictionary is thread-safe)
                            m_Clients.TryRemove(client, out _);
                            
                            // Update server's synced list after removal
                            var timeoutSnapshot = m_Clients.Values.ToList();
                            lock (_syncedClientListLock)
                            {
                                m_SyncedClientList = timeoutSnapshot;
                            }
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

                // Fire m_OnClientConnected only after handshake completes
                await SwitchToMainThreadWithRetry();
                m_OnClientConnected?.Invoke(clientInfo);

                await receiveLoopTask;
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

                // Remove client (ConcurrentDictionary is thread-safe)
                m_Clients.TryRemove(client, out _);

                // Update server's synced list after removal
                var clientSnapshot = m_Clients.Values.ToList();
                lock (_syncedClientListLock)
                {
                    m_SyncedClientList = clientSnapshot;
                }

                // Dispose and remove send lock for this client
                if (_streamSendLocks.TryRemove(client, out var semaphore))
                {
                    semaphore.Dispose();
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

                bool useBroadcast = true;
                int broadcastCount = 0;
                bool wasBroadcastingPaused = false;

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // Check if server has reached max concurrent connections
                        // ConcurrentDictionary.Count is thread-safe
                        int currentClientCount = m_Clients.Count;

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
                        useBroadcast = false;
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

                // Enable TCP Keep-Alive to detect dead connections (always on)
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    
                // TCP Keep-Alive parameters optimized for fast disconnect detection
                var keepAliveValues = new byte[12];
                BitConverter.GetBytes(1).CopyTo(keepAliveValues, 0);       // Enable = 1
                BitConverter.GetBytes(30000).CopyTo(keepAliveValues, 4);   // Idle time = 30s (before first probe)
                BitConverter.GetBytes(5000).CopyTo(keepAliveValues, 8);    // Interval = 5s (between probes)
                socket.IOControl(IOControlCode.KeepAliveValues, keepAliveValues, null);
                    
                // Total timeout: 30s idle + (5s × 9 probes) = ~75s max

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

                var stream = _tcpClient.GetStream();

                // --- Client Handshake Flow ---

                // Add server to pending connections
                lock (_handshakeLock)
                {
                    _pendingConnections[_tcpClient] = ConnectionState.Pending;
                }

                // --- Prepare ALL handshake waiters BEFORE starting receive loop (prevent race condition) ---
                string helloKey = $"hello_{m_ServerInfo.id}";
                string clientListKey = $"clientlist_{m_ServerInfo.id}";
                UniTaskCompletionSource<PacketResponse> helloWaiter;
                UniTaskCompletionSource<PacketResponse> clientListWaiter;
                
                lock (_handshakeLock)
                {
                    helloWaiter = new UniTaskCompletionSource<PacketResponse>();
                    _handshakeWaiters[helloKey] = helloWaiter;
                    
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
                var (ackPacket, ackLength) = CreatePacket(ACTION_CONNECTION_ACK, Array.Empty<byte>());
                await SendDataToStreamAsync(stream, ackPacket, ackLength, null, token, _tcpClient);

                lock (_handshakeLock)
                {
                    _pendingConnections[_tcpClient] = ConnectionState.Acknowledged;
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
                var (readyPacket, readyLength) = CreatePacket(ACTION_CONNECTION_READY, Array.Empty<byte>());
                await SendDataToStreamAsync(stream, readyPacket, readyLength, null, token, _tcpClient);

                lock (_handshakeLock)
                {
                    _pendingConnections[_tcpClient] = ConnectionState.Ready;
                    _pendingConnections.Remove(_tcpClient);
                }

                // Switch to main thread BEFORE modifying serialized fields
                await SwitchToMainThreadWithRetry();

                m_IsConnected = true;
                handshakeComplete = true;

                // Fire m_OnServerConnected only after handshake completes
                m_OnServerConnected?.Invoke(m_ServerInfo);

                // Fire reconnected event if this was a reconnection
                if (_isReconnecting && _reconnectAttempts > 0)
                {
                    Log($"✅ Reconnected successfully after {_reconnectAttempts} attempt(s)");
                    m_OnReconnected?.Invoke(m_ServerInfo);
                }

                await receiveLoopTask;
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

                if (_tcpClient != null)
                {
                    // ConcurrentDictionary: dispose and remove send lock atomically
                    if (_streamSendLocks.TryRemove(_tcpClient, out var semaphore))
                    {
                        semaphore.Dispose();
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
                using var udpClient = new UdpClient();
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, m_DiscoveryPort));

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

        private async UniTask ReceiveDataLoopAsync(NetworkStream stream, ConnectorInfo connectorInfo, CancellationToken token, TcpClient client = null)
        {
            var lengthBuffer = new byte[4];
            while (!token.IsCancellationRequested && stream.CanRead)
            {
                try
                {
                    // Read exactly 4 bytes for message length header (handle partial reads)
                    int headerBytesRead = 0;
                    while (headerBytesRead < 4)
                    {
                        int bytesRead = await stream.ReadAsync(lengthBuffer, headerBytesRead, 4 - headerBytesRead, token);
                        if (bytesRead == 0)
                        {
                            // Connection closed gracefully
                            if (headerBytesRead > 0)
                            {
                                Log($"⚠️ Connection closed while reading length header ({headerBytesRead}/4 bytes read)");
                            }
                            return; // Exit receive loop
                        }
                        headerBytesRead += bytesRead;
                        
                        // Debug: Log partial reads (only when needed for troubleshooting)
                        if (headerBytesRead < 4 && m_IsDebug && m_LogReceive)
                        {
                            Log($"📦 Partial header read: {bytesRead} bytes ({headerBytesRead}/4 total)");
                        }
                    }

                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                    if (messageLength <= 0)
                    {
                        // Likely stream desync — received bytes don't match protocol framing.
                        // Raw bytes help diagnose whether this is garbage data or an endian mismatch.
                        string rawHex = BitConverter.ToString(lengthBuffer);
                        string msg = $"Invalid message length: {messageLength} (raw header bytes: {rawHex}). " +
                                     $"Stream is desynced — closing connection to {connectorInfo.ipAddress}:{connectorInfo.port}.";
                        Log($"❌ {msg}");
                        ReportError(ErrorInfo.ErrorType.DataTransmission, msg, null, connectorInfo);
                        break;
                    }
                    if (messageLength > m_MaxPacketSizeMB * 1024 * 1024)
                    {
                        int maxBytes = m_MaxPacketSizeMB * 1024 * 1024;
                        string msg = $"Packet too large: {messageLength:N0} bytes ({messageLength / (1024f * 1024f):F1} MB) exceeds Max Packet Size ({m_MaxPacketSizeMB} MB). " +
                                     $"Increase \"Max Packet Size\" in the TCPConnector Inspector, or reduce the payload on the sender side. " +
                                     $"Closing connection to {connectorInfo.ipAddress}:{connectorInfo.port}.";
                        Log($"\u274c {msg}");
                        ReportError(ErrorInfo.ErrorType.DataTransmission, msg, null, connectorInfo);
                        break;
                    }

                    // Log receive start
                    bool isLargeReceive = messageLength > m_BufferSize * 2;
                    // Small packet log is deferred until after parsing (to skip heartbeat actions)
                    if (m_LogReceive && isLargeReceive)
                    {
                        Log($"📥 Receiving large data: {messageLength:N0} bytes from {connectorInfo.ipAddress}:{connectorInfo.port}");
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
                        const int progressReportIntervalMs = 100; // Report progress every 100ms max
                        int lastLoggedProgress = 0;

                        while (totalBytesRead < messageLength)
                        {
                            int remainingBytes = messageLength - totalBytesRead;
                            int bufferSize = Math.Min(m_BufferSize, remainingBytes);

                            int bytesRead = await stream.ReadAsync(dataBuffer, totalBytesRead, bufferSize, token);
                            if (bytesRead == 0)
                            {
                                string msg = $"Connection closed prematurely by {connectorInfo.ipAddress}:{connectorInfo.port} " +
                                             $"after {totalBytesRead:N0}/{messageLength:N0} bytes received.";
                                Log($"⚠️ {msg}");
                                ReportError(ErrorInfo.ErrorType.DataTransmission, msg, null, connectorInfo, operationId);
                                return; // Stream is gone — exit cleanly (finally will return buffer)
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
                        
                        // Critical null check - packet may be corrupted
                        if (finalPacketResponse == null)
                        {
                            string msg = $"Failed to parse packet from {connectorInfo.ipAddress}:{connectorInfo.port} " +
                                         $"(declared size={messageLength:N0} bytes). Packet header may be corrupted or truncated.";
                            Log($"⚠️ {msg}");
                            ReportError(ErrorInfo.ErrorType.DataTransmission, msg, null, connectorInfo, operationId);
                            break;
                        }
                        
                        // Copy progress information to final response
                        finalPacketResponse.status = PacketResponse.ReceiveStatus.Received;
                        finalPacketResponse.totalBytes = messageLength;
                        finalPacketResponse.processedBytes = totalBytesRead;
                        finalPacketResponse.operationId = operationId;

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
                                               finalPacketResponse.action == ACTION_CLIENT_LIST_FULL))
                            {
                                isHandshakeMessage = true;
                                string waitKey = finalPacketResponse.action == ACTION_CONNECTION_HELLO 
                                    ? $"hello_{connectorInfo.id}" 
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
                                if (!isLargeReceive)
                                {
                                    Log($"📥 Receiving data: Size={messageLength} bytes from {connectorInfo.ipAddress}:{connectorInfo.port}");
                                }
                                Log($"✅ Data received successfully: Action={finalPacketResponse.action}, Size={totalBytesRead} bytes from {connectorInfo.ipAddress}:{connectorInfo.port}");
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
                catch (IOException ex)
                {
                    Log($"❌ Network I/O error from {connectorInfo.ipAddress}:{connectorInfo.port}: {ex.Message}");
                    ReportError(ErrorInfo.ErrorType.DataTransmission, $"Network I/O error: {ex.Message}", ex, connectorInfo);
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

            // Use buffer pool for normal messages (not in critical handshake paths)
            bool usePool = true;
            var (message, messageLength) = CreatePacket(action, data, usePool);
            int retryCount = 0;
            bool success = false;

            // Log send attempt
            if (m_LogSend)
            {
                Log($"📤 Sending data: Action={action}, Size={data?.Length ?? 0} bytes");
            }

            try
            {
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
                            // Pass shouldReturnBuffer flag - will be returned after all clients receive
                            await SendAsServerAsync(message, messageLength, progress, token, shouldReturnBuffer: usePool);
                        }
                        else
                        {
                            // Client always sends to server
                            if (m_IsConnected && _tcpClient != null && _tcpClient.Connected)
                            {
                                await SendDataToStreamAsync(_tcpClient.GetStream(), message, messageLength, progress, token, _tcpClient, shouldReturnBuffer: usePool);
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
            }
            finally
            {
                // If buffer was pooled but send failed (never made it to SendDataToStreamAsync/SendAsServerAsync),
                // return it here. If send succeeded, buffer already returned by those methods.
                // Check if buffer needs cleanup on retry failures.
                if (usePool && !success && message != null)
                {
                    ReturnBuffer(message);
                }
            }

            return success;
        }

        /// <summary>
        /// Handles server-side message sending to all connected clients
        /// </summary>
        private async UniTask SendAsServerAsync(byte[] message, int messageLength, IProgress<float> progress, CancellationToken token, bool shouldReturnBuffer = false)
        {
            await SendAsServerAsync(message, messageLength, null, progress, token, shouldReturnBuffer);
        }

        /// <summary>
        /// Handles server-side message sending to specific clients or all clients
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="messageLength">Actual message length (may be less than message.Length for pooled buffers)</param>
        /// <param name="targetClientIds">Target client IDs. If null, sends to all clients</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="token">Cancellation token</param>
        /// <param name="shouldReturnBuffer">If true, returns buffer to pool after send completes</param>
        private async UniTask SendAsServerAsync(byte[] message, int messageLength, int[] targetClientIds, IProgress<float> progress, CancellationToken token, bool shouldReturnBuffer = false)
        {
            List<TcpClient> clientsToSend = new List<TcpClient>();

            // ConcurrentDictionary: iteration is thread-safe, get snapshot
            if (targetClientIds == null || targetClientIds.Length == 0)
            {
                // Send to all connected clients - avoid LINQ for performance
                foreach (var kvp in m_Clients)
                {
                    if (kvp.Key.Connected)
                    {
                        clientsToSend.Add(kvp.Key);
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

            if (clientsToSend.Count == 0)
            {
                string targetInfo = targetClientIds == null ? "all clients" : $"targeted clients [{string.Join(", ", targetClientIds)}]";
                Log($"No connected clients to send data to ({targetInfo})");
                
                // Return buffer if pooled, even if no clients to send to
                if (shouldReturnBuffer && message != null)
                {
                    ReturnBuffer(message);
                }
                return;
            }

            // Send to selected clients
            var sendTasks = clientsToSend.Select(async client =>
            {
                try
                {
                    // Note: Don't return buffer here - will be returned after all sends complete
                    await SendDataToStreamAsync(client.GetStream(), message, messageLength, progress, token, client, shouldReturnBuffer: false);

                    return true; // Success
                }
                catch (Exception ex)
                {
                    Log($"Failed to send data to client {client.Client.RemoteEndPoint}: {ex.Message}");
                    return false; // Failed for this client
                }
            }).ToList();

            try
            {
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
            finally
            {
                // Return buffer after all sends complete (success or failure)
                if (shouldReturnBuffer && message != null)
                {
                    ReturnBuffer(message);
                }
            }
        }

        /// <summary>
        /// Sends data to a specific NetworkStream with progress reporting.
        /// </summary>
        /// <param name="actualLength">Actual data length to send (may be less than message.Length for pooled buffers)</param>
        /// <param name="shouldReturnBuffer">If true, returns message buffer to pool after send completes (for pooled buffers)</param>
        private async UniTask SendDataToStreamAsync(NetworkStream stream, byte[] message, int actualLength, IProgress<float> progress = null, CancellationToken token = default, TcpClient client = null, bool shouldReturnBuffer = false)
        {
            if (stream == null || !stream.CanWrite)
            {
                throw new InvalidOperationException("Stream is not available for writing");
            }

            SemaphoreSlim sendLock = client != null ? GetOrCreateSendLock(client) : null;
            bool lockAcquired = false;

            // Use configured buffer size for optimal performance
            // CRITICAL: Use actualLength, NOT message.Length (buffer may be larger when pooled)
            int chunkSize = Math.Min(m_BufferSize, actualLength);
            int offset = 0;
            
            // Log for large transfers
            bool isLargeTransfer = actualLength > m_BufferSize * 2;
            if (isLargeTransfer && m_LogSend)
            {
                Log($"📤 Sending large data: {actualLength:N0} bytes (will send in {Math.Ceiling((double)actualLength / chunkSize)} chunks)");
            }

            // Create timeout token source
            using var timeoutCts = new CancellationTokenSource(m_SendTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
            
            int lastLoggedProgress = 0;

            try
            {
                // Acquire lock inside try block to ensure proper cleanup on cancellation
                if (sendLock != null)
                {
                    await sendLock.WaitAsync(linkedCts.Token);
                    lockAcquired = true;
                }

                while (offset < actualLength && !linkedCts.Token.IsCancellationRequested)
                {
                    int bytesToSend = Math.Min(chunkSize, actualLength - offset);
                    await stream.WriteAsync(message, offset, bytesToSend, linkedCts.Token);
                    offset += bytesToSend;

                    // Report progress as percentage (0.0 to 1.0)
                    float progressPercentage = (float)offset / actualLength;
                    progress?.Report(progressPercentage);
                    
                    // Log progress for large transfers (every 25%)
                    if (isLargeTransfer && m_LogSend)
                    {
                        int currentProgress = (int)(progressPercentage * 100);
                        if (currentProgress >= lastLoggedProgress + 25 || offset >= actualLength)
                        {
                            lastLoggedProgress = currentProgress;
                            Log($"📤 Send progress: {currentProgress}% ({offset:N0}/{actualLength:N0} bytes)");
                        }
                    }
                }

                // Ensure all data is sent with timeout
                await stream.FlushAsync(linkedCts.Token);
            }
            catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
            {
                // Timeout occurred
                throw new TimeoutException($"Send operation timed out after {m_SendTimeout}ms at offset {offset}/{actualLength}", ex);
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
                Log($"Stream write error at offset {offset}/{actualLength}: {ex.GetType().Name} - {ex.Message}");
                throw; // Re-throw to be handled by retry logic
            }
            finally
            {
                // Only release lock if it was successfully acquired
                if (lockAcquired)
                {
                    sendLock?.Release();
                }
                
                // Return buffer to pool if requested (after lock is released)
                if (shouldReturnBuffer && message != null)
                {
                    ReturnBuffer(message);
                }
            }
        }

        public async UniTask<bool> SendDataAsync<T>(ushort action, T data, CancellationToken token = default)
        {
            return await SendDataAsync(action, data, (IProgress<float>)null, token);
        }

        public async UniTask<bool> SendDataAsync<T>(ushort action, T data, IProgress<float> progress, CancellationToken token = default)
        {
            return await SendDataAsync(action, data, progress, token, isInternalCall: false);
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
                return UnityConverter.ToBytes(data);
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

            return Encoding.UTF8.GetBytes(jsonString);
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

            // Server-side targeting logic - use buffer pool
            bool usePool = true;
            var (message, messageLength) = CreatePacket(action, data, usePool);
            int retryCount = 0;
            bool success = false;

            try
            {
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

                        // Pass shouldReturnBuffer flag - will be returned after send completes
                        await SendAsServerAsync(message, messageLength, targetClientIds, progress, token, shouldReturnBuffer: usePool);
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
            finally
            {
                // If buffer was pooled but send failed, return it here
                if (usePool && !success && message != null)
                {
                    ReturnBuffer(message);
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
        /// <param name="action">Action identifier</param>
        /// <param name="data">Payload data</param>
        /// <param name="usePool">If true, uses buffer pool (caller must return buffer). If false, creates new buffer (fire-and-forget).</param>
        /// <returns>Tuple of (buffer, actualLength). Buffer may be larger than actualLength when pooled. Caller MUST use actualLength, not buffer.Length.</returns>
        private (byte[] buffer, int length) CreatePacket(ushort action, byte[] data, bool usePool = false)
        {
            data ??= Array.Empty<byte>();
            
            // Calculate total size: 4 bytes (length prefix) + PACKET_HEADER_SIZE + data length
            int packetSize = PACKET_HEADER_SIZE + data.Length;
            int totalSize = 4 + packetSize;
            
            // Use buffer pool or allocate new buffer
            // WARNING: Pooled buffer may be LARGER than totalSize!
            byte[] finalMessage = usePool ? GetBuffer(totalSize) : new byte[totalSize];
            
            // Write length prefix (first 4 bytes)
            Buffer.BlockCopy(BitConverter.GetBytes(packetSize), 0, finalMessage, 0, 4);
            
            int offset = 4; // Start after length prefix
            
            // Write message ID (4 bytes)
            Buffer.BlockCopy(BitConverter.GetBytes(new System.Random().Next()), 0, finalMessage, offset, 4);
            offset += 4;
            
            // Write action (2 bytes)
            Buffer.BlockCopy(BitConverter.GetBytes(action), 0, finalMessage, offset, 2);
            offset += 2;
            
            // Write payload data
            if (data.Length > 0)
            {
                Buffer.BlockCopy(data, 0, finalMessage, offset, data.Length);
            }

            // Log removed for performance - was logging every packet creation
            // Return both buffer and ACTUAL length (critical for pooled buffers!)
            return (finalMessage, totalSize);
        }

        /// <summary>
        /// Reads TCP packet (assumes length prefix has already been processed)
        /// </summary>
        private PacketResponse ReadPacket(byte[] data, int dataLength, ConnectorInfo connectorInfo)
        {
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
            
            /// <summary>
            /// Payload data. This buffer is owned by the event handler.
            /// ⚠️ WARNING: Do not retain references to this buffer beyond the event handler scope.
            /// For future optimization compatibility, always deserialize or copy data immediately within the handler.
            /// </summary>
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

            // Get snapshot of current clients (ConcurrentDictionary iteration is thread-safe)
            List<ConnectorInfo> currentClients = m_Clients.Values.ToList();

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
                }
                catch (Exception ex)
                {
                    Log($"Failed to send sync to client {client.id}: {ex.Message}");
                }
            }

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
                    // ConcurrentDictionary iteration is thread-safe
                    targetIds = m_Clients.Values
                        .Where(c => c.id != senderInfo.id)
                        .Select(c => c.id)
                        .ToArray();
                }
                else
                {
                    // Send to specific targets (excluding sender)
                    targetIds = relayData.targetIds.Where(id => id != senderInfo.id).ToArray();
                }

                if (targetIds.Length > 0)
                {
                    await SendDataToClientsAsync(ACTION_RELAY_MESSAGE, relayData, targetIds);
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