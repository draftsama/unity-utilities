using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Modules.Utilities;
using NUnit.Framework;
using UnityEngine.Events;
using UnityEditor.VersionControl;
using Unity.Android.Gradle.Manifest;
using Cysharp.Threading.Tasks.Linq;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modules.Utilities
{
    public class UDPConnector : MonoBehaviour
    {

        [System.Serializable]
        public struct ConnectorInfo
        {
            public string ip;
            public long lastPing;
            public IPEndPoint remoteEndPoint;

            public void Clear()
            {
                ip = string.Empty;
                lastPing = -1;
                remoteEndPoint = null;
            }
            public bool IsEmpty()
            {
                return string.IsNullOrEmpty(ip) && lastPing == -1 && remoteEndPoint == null;
            }
        }

        [System.Serializable]
        public class PacketResponse
        {
            public int packetId;
            public PacketType packetType;
            public ushort action;
            public bool isCompleted;
            public byte[] data;
            public IPEndPoint remoteEndPoint;
        }


        public enum PacketType
        {
            PING = 0,
            STRING_DATA = 1,
            BYTE_DATA = 2,
            FLOAT_DATA = 3,
            INT_DATA = 4,
            LONG_DATA = 5,
            VECTOR2_DATA = 6,
            VECTOR3_DATA = 7,
            VECTOR4_DATA = 8,
            QUATERNION_DATA = 9,
            COLOR_DATA = 10,
            BOOL_DATA = 11,

        }



        [SerializeField] private string m_Host = "";
        [SerializeField] private int m_Port = 5555;
        [SerializeField] private bool m_StartOnEnable = true;
        [SerializeField] private bool m_IsServer = false;
        [SerializeField] private bool m_IsRunning = false;
        [SerializeField] private bool m_IsConnected = false;
        [SerializeField] private bool m_IsWaitingReconnect = false;

        [SerializeField] private bool m_IsDebug = true;

        private UdpClient udpClient;

        [SerializeField] private List<ConnectorInfo> m_ClientInfoList = new List<ConnectorInfo>();
        [SerializeField] private ConnectorInfo m_ServerInfo;




        [SerializeField] private UnityEvent<ConnectorInfo> m_OnClientConnected = new UnityEvent<ConnectorInfo>();
        [SerializeField] private UnityEvent<ConnectorInfo> m_OnClientDisconnected = new UnityEvent<ConnectorInfo>();

        [SerializeField] private UnityEvent<ConnectorInfo> m_OnServerConnected = new UnityEvent<ConnectorInfo>();
        [SerializeField] private UnityEvent<ConnectorInfo> m_OnServerDisconnected = new UnityEvent<ConnectorInfo>();

        [SerializeField] private UnityEvent<PacketResponse> m_OnDataReceived = new UnityEvent<PacketResponse>();

        private CancellationTokenSource _Cts;

        private const int MAX_CHUNK_SIZE = 1472;

        //4 bytes for id(int)
        //2 bytes for total chunks(ushort)
        //2 bytes for chunk index(ushort)
        //2 byte for type(ushort)
        //2 byte for action(ushort)
        private const int PACKET_HEADER_SIZE = 12;
        private Dictionary<int, List<byte[]>> receivedChunks = new Dictionary<int, List<byte[]>>();


        private void OnEnable()
        {
            if (m_StartOnEnable)
            {
                Connect(m_IsServer).Forget();
            }

        }

        private void OnDisable()
        {
            Disconnect();
        }
        private void OnDestroy()
        {
            Disconnect();
        }
        public void Disconnect()
        {
            _Cts?.Cancel();
            _Cts?.Dispose();
            _Cts = null;

            if (udpClient != null)
            {

                udpClient.Close();
                udpClient = null;

            }

            m_IsRunning = false;
            m_IsConnected = false;
            m_IsWaitingReconnect = false;
            m_ClientInfoList.Clear();
            m_ServerInfo.Clear();
            Log("UDP client closed");
        }
        private void Log(object message)
        {
            if (!m_IsDebug)
                return;
            var prefix = m_IsServer ? "Server" : "Client";
            Debug.Log($"[{prefix}] {message}");
        }

        public async UniTask Connect(bool _isServer)
        {
            if (m_IsRunning || m_IsWaitingReconnect)
                return;

            gameObject.SetActive(true);
            _Cts?.Cancel();
            _Cts = new CancellationTokenSource();
            var token = _Cts.Token;
            await UniTask.SwitchToThreadPool();
            try
            {
                m_IsServer = _isServer;

                m_IsRunning = false;
                m_IsConnected = false;
                m_IsWaitingReconnect = false;
                m_ClientInfoList.Clear();
                m_ServerInfo.Clear();


                if (m_IsServer)
                {


                    udpClient = new UdpClient();
                    udpClient.ExclusiveAddressUse = true;
                    udpClient.DontFragment = true;

                    udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                    udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                    var ipV4 = NetworkUtility.GetLocalIPv4();

                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(ipV4), m_Port);
                    udpClient.Client.Bind(remoteEP);

                    //get local ip
                    Log($"UDP server started on {ipV4} : {m_Port}");
                    m_IsRunning = true;



                }
                else
                {
                    //connect to server
                    var serverEndPoint = new IPEndPoint(IPAddress.Parse(m_Host), m_Port);
                    udpClient = new UdpClient();
                    udpClient.DontFragment = true;

                    udpClient.Connect(serverEndPoint);

                    Log($"UDP client connecting to {m_Host}:{m_Port}");

                    m_IsRunning = true;



                }


                await UniTask.WhenAll(
                    PingInterval(1000, token),
                    ReceiveDataProcess(token),
                    ConnectionMonitor(1000, 1100, token)
                );



            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException ex)
            {
                Log($"Socket Error: {ex.Message}");
                if (!_isServer)
                    Reconnect(false, 3000).Forget();
            }
            catch (ObjectDisposedException)
            {

            }
            catch (Exception ex)
            {

                Log($"Error: {ex.Message}");
                if (!_isServer)
                    Reconnect(false, 3000).Forget();
            }





        }





        private async UniTask Reconnect(bool _isServer, int _delay)
        {
            await UniTask.SwitchToMainThread();
            m_IsWaitingReconnect = true;
            Log($"Waiting for {_delay}ms to reconnect...");
            _Cts?.Cancel();
            _Cts = new CancellationTokenSource();
            var token = _Cts.Token;

            if (udpClient != null)
            {
                udpClient.Close();
                udpClient = null;
            }
            m_IsRunning = false;
            m_IsConnected = false;
            m_ClientInfoList.Clear();
            m_ServerInfo.Clear();
            try
            {

                await UniTask.Delay(_delay, cancellationToken: token);
                m_IsWaitingReconnect = false;

                Connect(_isServer).Forget();

            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log($"Reconnect Error: {ex.Message}");
            }
            finally
            {
                m_IsWaitingReconnect = false;
            }


        }
        private int GetRandomPacketId()
        {
            return new System.Random().Next();
        }


        public async UniTask PingInterval(int _interval, CancellationToken _token)
        {


            while (!_token.IsCancellationRequested)
            {

                try
                {


                    var messageId = GetRandomPacketId();
                    var action = 0;

                    await SendPacket(messageId, PacketType.PING, (ushort)action, null, _token);

                    await UniTask.Delay(_interval, cancellationToken: _token);


                }


                catch (Exception ex)
                {
                    // Log($"PingInterval Error: {ex.Message}");
                    throw ex;
                }
            }
        }


        public async UniTask ConnectionMonitor(int _interval, int disconnectTime, CancellationToken _token)
        {


            while (!_token.IsCancellationRequested)
            {
                try
                {

                    await UniTask.Delay(_interval, cancellationToken: _token);

                    var timeNow = DateTime.UtcNow.Ticks;

                    var disconnectTick = disconnectTime * TimeSpan.TicksPerMillisecond;
                    if (m_IsServer)
                    {

                        //check if client is disconnected
                        for (int i = 0; i < m_ClientInfoList.Count; i++)
                        {
                            var client = m_ClientInfoList[i];
                            if ((timeNow - client.lastPing) > disconnectTick)
                            {
                                // Client disconnected
                                m_OnClientDisconnected?.Invoke(client);
                                Log($"Client {client.ip} disconnected");
                                m_ClientInfoList.RemoveAt(i);
                                i--;
                            }
                        }

                    }
                    else
                    {

                        if ((timeNow - m_ServerInfo.lastPing) > disconnectTick)
                        {
                            // Server disconnected
                            m_ServerInfo.Clear();
                            m_IsConnected = false;
                            m_OnServerDisconnected?.Invoke(m_ServerInfo);

                        }
                    }





                }


                catch (Exception ex)
                {
                    throw ex;
                }

            }

        }

        private async UniTask ReceiveDataProcess(CancellationToken _token)
        {

            while (!_token.IsCancellationRequested)
            {
                try
                {


                    var result = await udpClient.ReceiveAsync();


                    var packetResponse = ReadPacket(result);

                    if (packetResponse == null)
                        continue;
                    if (!packetResponse.isCompleted)
                        continue;

                    switch (packetResponse.packetType)
                    {
                        case PacketType.PING:
                            if (m_IsServer)
                                UpdateClientConnected(result.RemoteEndPoint);
                            else
                                UpdateServerConnected(result.RemoteEndPoint);
                            break;
                        case PacketType.STRING_DATA:
                        case PacketType.BYTE_DATA:
                        case PacketType.FLOAT_DATA:
                        case PacketType.INT_DATA:
                        case PacketType.LONG_DATA:
                        case PacketType.VECTOR2_DATA:
                        case PacketType.VECTOR3_DATA:
                        case PacketType.VECTOR4_DATA:
                        case PacketType.QUATERNION_DATA:
                        case PacketType.COLOR_DATA:
                        case PacketType.BOOL_DATA:
                            m_OnDataReceived?.Invoke(packetResponse);
                            break;

                    }




                }


                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }



        public void UpdateClientConnected(IPEndPoint _remoteEndPoint)
        {
            if (!m_IsServer)
                return;
            // Log($"m_ClientInfoList count : {m_ClientInfoList.Count}");
            var timeNow = DateTime.UtcNow.Ticks;
            try
            {
                var _ip = _remoteEndPoint.Address.ToString();
                var index = m_ClientInfoList.FindIndex(x => x.ip == _ip);


                if (index == -1)
                {
                    Log($"Client connected: {_ip}");
                    var clientInfo = new ConnectorInfo()
                    {
                        ip = _ip,
                        lastPing = timeNow,
                        remoteEndPoint = _remoteEndPoint
                    };
                    m_OnClientConnected?.Invoke(clientInfo);

                    m_ClientInfoList.Add(clientInfo);
                }
                else
                {
                    var clientInfo = m_ClientInfoList[index];
                    clientInfo.lastPing = timeNow;
                    m_ClientInfoList[index] = clientInfo;
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }


        }

        public void UpdateServerConnected(IPEndPoint _remoteEndPoint)
        {
            if (m_IsServer)
                return;

            var timeNow = DateTime.UtcNow.Ticks;


            try
            {
                m_ServerInfo.ip = _remoteEndPoint.Address.ToString();
                m_ServerInfo.lastPing = timeNow;
                m_ServerInfo.remoteEndPoint = _remoteEndPoint;

                if (!m_IsConnected)
                {
                    m_OnServerConnected?.Invoke(m_ServerInfo);
                }

                m_IsConnected = true;



            }
            catch (Exception ex)
            {
                throw ex;
            }

        }
        private async UniTask SendPacket(int messageId, PacketType _packetType, ushort _action, byte[] _data, CancellationToken _token)
        {
            if (udpClient == null)
                return;

            if (_data == null)
                _data = new byte[1]{
                0x00
            };

            try
            {

                var payloadSize = MAX_CHUNK_SIZE - PACKET_HEADER_SIZE;
                int totalChunks = (int)Math.Ceiling((double)_data.Length / payloadSize);

                for (int i = 0; i < totalChunks; i++)
                {

                    int offset = i * payloadSize;
                    int size = Math.Min(payloadSize, _data.Length - offset);

                    byte[] packet = new byte[PACKET_HEADER_SIZE + size];
                    //header
                    Buffer.BlockCopy(BitConverter.GetBytes(messageId), 0, packet, 0, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes((ushort)totalChunks), 0, packet, 4, 2);
                    Buffer.BlockCopy(BitConverter.GetBytes((ushort)i), 0, packet, 6, 2);
                    Buffer.BlockCopy(BitConverter.GetBytes((ushort)_packetType), 0, packet, 8, 2);
                    Buffer.BlockCopy(BitConverter.GetBytes(_action), 0, packet, 10, 2);


                    //payload
                    Buffer.BlockCopy(_data, offset, packet, 12, size);
                    //send packet
                    if (m_IsServer)
                    {
                        //send to all clients
                        for (int j = 0; j < m_ClientInfoList.Count; j++)
                        {
                            var client = m_ClientInfoList[j];
                            if (client.remoteEndPoint == null)
                                continue;
                            var endPoint = client.remoteEndPoint;
                            await udpClient.SendAsync(packet, packet.Length, endPoint).AsUniTask(false).AttachExternalCancellation(_token);
                        }

                    }
                    else
                    {
                        //send to server
                        await udpClient.SendAsync(packet, packet.Length).AsUniTask(false).AttachExternalCancellation(_token);
                    }


                }
            }


            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw ex;
            }


        }

        private PacketResponse ReadPacket(UdpReceiveResult _result)
        {
            var data = _result.Buffer;

            if (data == null || data.Length < PACKET_HEADER_SIZE)
                return null;

            //header
            int packetId = BitConverter.ToInt32(data, 0);
            ushort totalChunks = BitConverter.ToUInt16(data, 4);
            ushort chunkIndex = BitConverter.ToUInt16(data, 6);
            var type = BitConverter.ToUInt16(data, 8);
            var action = BitConverter.ToUInt16(data, 10);

            //payload
            var payload = new byte[data.Length - PACKET_HEADER_SIZE];
            Buffer.BlockCopy(data, PACKET_HEADER_SIZE, payload, 0, payload.Length);

            if (!receivedChunks.ContainsKey(packetId))
            {
                receivedChunks.Add(packetId, new List<byte[]>());
            }

            // Add the chunk to the list
            receivedChunks[packetId].Add(payload);

            // Check if we have received all chunks
            if (receivedChunks[packetId].Count == totalChunks)
            {
                // Combine all chunks into a single byte array
                byte[] completePacket = new byte[totalChunks * (data.Length - PACKET_HEADER_SIZE)];
                for (int i = 0; i < totalChunks; i++)
                {
                    Buffer.BlockCopy(receivedChunks[packetId][i], 0, completePacket, i * (data.Length - PACKET_HEADER_SIZE), data.Length - PACKET_HEADER_SIZE);
                }

                // Remove the message from the dictionary
                receivedChunks.Remove(packetId);

                return new PacketResponse()
                {
                    packetId = packetId,
                    packetType = (PacketType)type,
                    action = action,
                    isCompleted = true,
                    data = completePacket,
                    remoteEndPoint = _result.RemoteEndPoint
                };
            }

            return new PacketResponse()
            {
                packetId = packetId,
                packetType = (PacketType)type,
                action = action,
                isCompleted = false,
                data = null,
                remoteEndPoint = _result.RemoteEndPoint
            };



        }







        public async UniTask SendDataAsync(ushort _action, PacketType _packetType, byte[] _data, CancellationToken _token)
        {
            if (udpClient == null || !m_IsRunning)
                return;


            //incase when client have not server info 
            if (!m_IsServer && m_ServerInfo.IsEmpty())
                return;

            //incase when server have not client info 
            if (m_IsServer && m_ClientInfoList.Count == 0)
                return;

            try
            {

                var messageId = GetRandomPacketId();
                //send data
                await SendPacket(messageId, _packetType, _action, _data, _token);

            }

            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        public IUniTaskAsyncEnumerable<PacketResponse> OnDataReceived(CancellationToken _token)
        {
            return new UnityEventHandlerAsyncEnumerable<PacketResponse>(m_OnDataReceived, _token);
        }
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnClientConnected(CancellationToken _token)
        {
            return new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnClientConnected, _token);
        }
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnClientDisconnected(CancellationToken _token)
        {
            return new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnClientDisconnected, _token);
        }
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnServerConnected(CancellationToken _token)
        {
            return new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnServerConnected, _token);
        }
        public IUniTaskAsyncEnumerable<ConnectorInfo> OnServerDisconnected(CancellationToken _token)
        {
            return new UnityEventHandlerAsyncEnumerable<ConnectorInfo>(m_OnServerDisconnected, _token);
        }


















    }
}

#if UNITY_EDITOR

namespace Modules.Utilities
{

    [CustomEditor(typeof(UDPConnector))]
    public class UDPConnectorEditor : UnityEditor.Editor
    {

        private string m_IpAddress = "";
        public override void OnInspectorGUI()
        {
            // base.OnInspectorGUI();
            var udpConnector = (UDPConnector)target;
            serializedObject.Update();
            var isRunning = serializedObject.FindProperty("m_IsRunning");
            var isConnected = serializedObject.FindProperty("m_IsConnected");
            var isServer = serializedObject.FindProperty("m_IsServer");
            var startOnEnable = serializedObject.FindProperty("m_StartOnEnable");
            var host = serializedObject.FindProperty("m_Host");
            var port = serializedObject.FindProperty("m_Port");
            var isDebug = serializedObject.FindProperty("m_IsDebug");
            var isWaitingReconnect = serializedObject.FindProperty("m_IsWaitingReconnect");

            var clientInfoList = serializedObject.FindProperty("m_ClientInfoList");
            var serverInfo = serializedObject.FindProperty("m_ServerInfo");

            var onClientConnected = serializedObject.FindProperty("m_OnClientConnected");
            var onClientDisconnected = serializedObject.FindProperty("m_OnClientDisconnected");
            var onServerConnected = serializedObject.FindProperty("m_OnServerConnected");
            var onServerDisconnected = serializedObject.FindProperty("m_OnServerDisconnected");
            var onDataReceived = serializedObject.FindProperty("m_OnDataReceived");


            //begin horizontal group
            EditorGUILayout.BeginHorizontal();
            GUI.color = isServer.boolValue ? Color.green : Color.gray;
            if (GUILayout.Button("Server"))
            {
                isServer.boolValue = true;
            }
            GUI.color = isServer.boolValue ? Color.gray : Color.green;

            if (GUILayout.Button("Client"))
            {
                isServer.boolValue = false;
            }

            EditorGUILayout.EndHorizontal();
            GUI.color = Color.white;


            EditorGUILayout.BeginHorizontal();
            //indent
            EditorGUI.indentLevel++;
            //draw window with title

            GUI.backgroundColor = Color.gray;
            EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));

            GUI.backgroundColor = Color.black;
            // Draw a rounded box for the "Settings" section


            //rounded corners
            // boxStyle.normal.background = GUI.skin.window.normal.background;

            EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            GUI.backgroundColor = Color.white;

            DrawProperty(host, !isServer.boolValue);
            DrawProperty(port);
            DrawProperty(isRunning, isReadOnly: true);
            DrawProperty(isConnected, !isServer.boolValue, isReadOnly: true);

            DrawProperty(startOnEnable);
            DrawProperty(isDebug);
            DrawProperty(clientInfoList, isServer.boolValue, isReadOnly: true);
            DrawProperty(serverInfo, !isServer.boolValue, isReadOnly: true);

            DrawProperty(onClientConnected, isServer.boolValue);
            DrawProperty(onClientDisconnected, isServer.boolValue);
            DrawProperty(onServerConnected, !isServer.boolValue);
            DrawProperty(onServerDisconnected, !isServer.boolValue);
            DrawProperty(onDataReceived);

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }

            if (UnityEngine.Application.isPlaying)
            {
                GUI.backgroundColor = Color.gray;

                EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));

                GUI.backgroundColor = Color.black;

                EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));

                EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

                EditorGUILayout.EndVertical();

                GUI.backgroundColor = Color.white;

                if (!isRunning.boolValue)
                {

                    if (isWaitingReconnect.boolValue)
                    {
                        GUI.color = Color.yellow;
                        EditorGUILayout.LabelField("Waiting for reconnect...", EditorStyles.boldLabel);

                        GUI.color = Color.red;

                        if (GUILayout.Button("Disconnect"))
                        {
                            udpConnector.Disconnect();
                        }
                    }
                    else
                    {
                        GUI.color = Color.gray;

                        if (GUILayout.Button("Connect"))
                        {
                            m_IpAddress = isServer.boolValue ? NetworkUtility.GetLocalIPv4() : host.stringValue;
                            udpConnector.Connect(isServer.boolValue).Forget();

                        }
                    }



                }
                else
                {

                    GUI.color = Color.green;

                    EditorGUILayout.LabelField($"Running on Ip:{m_IpAddress} - Port:{port.intValue}", EditorStyles.boldLabel);


                    GUI.color = Color.red;

                    if (GUILayout.Button("Disconnect"))
                    {
                        udpConnector.Disconnect();
                    }



                }

                EditorGUILayout.EndVertical();

            }




        }

        private void DrawProperty(SerializedProperty property, bool isShow = true, bool isReadOnly = false)
        {
            GUI.enabled = !isReadOnly;
            if (isShow)
                EditorGUILayout.PropertyField(property, true);
            GUI.enabled = true;
        }



    }
}
#endif
