using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

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
        [SerializeField] private string m_Host = "127.0.0.1";
        [SerializeField] private int m_Port = 7777;
        [SerializeField] private bool m_IsServer = false;
        
        [Header("Behavior")]
        [SerializeField] private bool m_StartOnEnable = true;
        [SerializeField] private bool m_IsDebug = true;

        [Header("Auto Discovery (UDP Broadcast)")]
        [SerializeField] private bool m_EnableDiscovery = true;
        [SerializeField] private int m_DiscoveryPort = 7778;
        [SerializeField] private string m_DiscoveryMessage = "DiscoverServer";

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

        private CancellationTokenSource _cts;

        // --- Packet Structure ---
        private const int PACKET_HEADER_SIZE = 14; // 4-id, 4-sender, 2-type, 2-dataType, 2-action

        #endregion

        #region Public Variables
        public bool IsConnected => m_IsConnected;
        public bool IsRunning => m_IsRunning;
        public List<ConnectorInfo> ClientInfoList => m_Clients.Values.ToList();
        public ConnectorInfo ServerInfo => m_ServerInfo;
        #endregion

        #region Unity Methods

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
                if(m_EnableDiscovery)
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
                lock(m_Clients)
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
                if(m_IsConnected)
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
                Log($"Server listening on TCP port {m_Port}.");

                while (!token.IsCancellationRequested)
                {
                    TcpClient connectedClient = await _tcpListener.AcceptTcpClientAsync().AsUniTask().AttachExternalCancellation(token);
                    HandleClientAsync(connectedClient, token).Forget();
                }
            }
            catch (OperationCanceledException) { Log("Server start operation was canceled."); }
            catch (Exception ex) { Log($"Server Error: {ex.GetType().Name} - {ex.Message}"); }
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
            
            lock(m_Clients) { m_Clients.Add(client, clientInfo); }
            Log($"Client connected: {clientInfo.ipAddress}:{clientInfo.port}");
            await UniTask.SwitchToMainThread();
            m_OnClientConnected?.Invoke(clientInfo);

            var stream = client.GetStream();
            try { await ReceiveDataLoopAsync(stream, clientInfo, token); }
            catch (Exception) { /* Handled in finally */ }
            finally
            {
                Log($"Client disconnected: {clientInfo.ipAddress}:{clientInfo.port}");
                lock(m_Clients) { m_Clients.Remove(client); }
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
                    await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log($"UDP Broadcast Error: {ex.Message}"); }
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
                    Log("Searching for server...");
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
            catch (OperationCanceledException) { Log("Connection attempt canceled by token."); }
            catch (Exception ex) { Log($"Failed to connect or connection lost: {ex.Message}"); }
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
            catch (Exception ex) { Log($"UDP Listen Error: {ex.Message}"); }
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
                int bytesRead = await stream.ReadAsync(lengthBuffer, 0, lengthBuffer.Length, token);
                if (bytesRead < 4) break; // Connection closed
                
                int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                if (messageLength <= 0 || messageLength > 1024 * 1024 * 4) // <<< Sanity check for message size (e.g., max 4MB)
                {
                    Log($"Invalid message length received: {messageLength}. Disconnecting.");
                    break;
                }
                var dataBuffer = new byte[messageLength];

                int totalBytesRead = 0;
                while(totalBytesRead < messageLength)
                {
                    bytesRead = await stream.ReadAsync(dataBuffer, totalBytesRead, messageLength - totalBytesRead, token);
                    if (bytesRead == 0) throw new InvalidOperationException("Connection closed prematurely.");
                    totalBytesRead += bytesRead;
                }
                
                PacketResponse response = ReadPacket(dataBuffer, connectorInfo.remoteEndPoint);
                if (response != null)
                {
                    await UniTask.SwitchToMainThread();
                    m_OnDataReceived?.Invoke(response);
                }
            }
        }
        
        public async UniTask SendDataAsync(ushort action, DataType dataType, byte[] data, CancellationToken token = default)
        {
            if (!m_IsRunning) return;

            byte[] message = CreatePacket(PacketType.SEND_DATA, dataType, action, data);
            try
            {
                if (m_IsServer)
                {
                    // Create a copy of the list to avoid issues if a client disconnects during the send
                    List<TcpClient> clientsToSend;
                    lock(m_Clients) { clientsToSend = m_Clients.Keys.ToList(); }

                    var sendTasks = clientsToSend.Select(client => 
                        client.GetStream().WriteAsync(message, 0, message.Length, token).AsUniTask().AttachExternalCancellation(token)
                    ).ToList();
                    await UniTask.WhenAll(sendTasks);
                }
                else if (m_IsConnected && _tcpClient != null)
                {
                    await _tcpClient.GetStream().WriteAsync(message, 0, message.Length, token);
                }
            }
            catch (Exception ex)
            {
                Log($"Send Data Error: {ex.Message}");
                // A failed write usually means connection is dead. The receive loop will handle the disconnect.
            }
        }

        private byte[] CreatePacket(PacketType packetType, DataType dataType, ushort action, byte[] data)
        {
            data ??= Array.Empty<byte>();
            byte[] packet = new byte[PACKET_HEADER_SIZE + data.Length];
            int offset = 0;
            
            int myId = m_IsServer ? -1 : _tcpClient?.GetHashCode() ?? 0;

            Buffer.BlockCopy(BitConverter.GetBytes(new System.Random().Next()), 0, packet, offset, 4); offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(myId), 0, packet, offset, 4); offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)packetType), 0, packet, offset, 2); offset += 2;
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)dataType), 0, packet, offset, 2); offset += 2;
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
            var packetType = (PacketType)BitConverter.ToUInt16(data, offset); offset += 2;
            var dataType = (DataType)BitConverter.ToUInt16(data, offset); offset += 2;
            var action = BitConverter.ToUInt16(data, offset);
            
            var payload = new byte[data.Length - PACKET_HEADER_SIZE];
            Buffer.BlockCopy(data, PACKET_HEADER_SIZE, payload, 0, payload.Length);
            
            return new PacketResponse
            {
                messageId = messageId, senderId = senderId, packetType = packetType,
                dataType = dataType, action = action, data = payload, remoteEndPoint = remoteEndPoint
            };
        }
        #endregion

        #region Nested Classes & Enums
        
        [Serializable] public struct ConnectorInfo { public int id; public string ipAddress; public int port; public IPEndPoint remoteEndPoint; }
        public class PacketResponse { public int messageId; public int senderId; public PacketType packetType; public DataType dataType; public ushort action; public byte[] data; public IPEndPoint remoteEndPoint; }
        public enum PacketType { SEND_DATA = 1 }
        public enum DataType { NONE, STRING, BYTES, FLOAT, INT, LONG, VECTOR2, VECTOR3, VECTOR4, QUATERNION, COLOR, BOOL, DOUBLE, TRANSFORM }

        #endregion

        #region Event Handlers
        public IUniTaskAsyncEnumerable<PacketResponse> OnDataReceived(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<PacketResponse>(m_OnDataReceived, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnClientConnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnClientConnected, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnClientDisconnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnClientDisconnected, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnServerConnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnServerConnected, token);
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnServerDisconnected(CancellationToken token) => new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnServerDisconnected, token);
        #endregion
    }
}


#if UNITY_EDITOR
namespace Modules.Utilities
{
    [CustomEditor(typeof(TCPConnector))]
    public class TCPConnectorEditor : UnityEditor.Editor
    {
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
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_IsDebug"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_StartOnEnable"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Port"));

            var enableDiscovery = serializedObject.FindProperty("m_EnableDiscovery");
            if (!isServer.boolValue)
            {
                EditorGUILayout.PropertyField(enableDiscovery);
                // Only show Host field if discovery is disabled
                if (!enableDiscovery.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Host"));
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Auto Discovery (UDP)", EditorStyles.boldLabel);
            // <<< Discovery can be enabled/disabled for server too
            if (isServer.boolValue)
            {
                 EditorGUILayout.PropertyField(enableDiscovery);
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DiscoveryPort"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DiscoveryMessage"));
            
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
            EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
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
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif