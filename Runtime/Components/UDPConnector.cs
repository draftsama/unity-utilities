using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Modules.Utilities;
using UnityEngine.Events;
using Cysharp.Threading.Tasks.Linq;




#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modules.Utilities
{
    public class UDPConnector : MonoBehaviour
    {
        #region private variables

        [SerializeField] private int m_Id = -1;
        [SerializeField] private string m_IpAddress = "";

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
        //2 byte for packet type(ushort)
        //2 byte for data type(ushort)
        //2 byte for action(ushort)
        //1 byte for request complete(bool)
        //4 bytes for senderId(int)
        private const int PACKET_HEADER_SIZE = 19;
        private Dictionary<int, List<byte[]>> receivedChunks = new Dictionary<int, List<byte[]>>();
        private List<PacketRequest> packetSendQueue = new List<PacketRequest>();
        private List<PacketConfirm> packetWaitConfirmList = new List<PacketConfirm>();

        #endregion

        #region public variables
        public bool IsConnected => m_IsConnected;
        public bool IsRunning => m_IsRunning;

        public List<ConnectorInfo> ClientInfoList => m_ClientInfoList;
        public ConnectorInfo ServerInfo => m_ServerInfo;


        #endregion


        #region Unity Methods

        private void OnEnable()
        {
            if (m_StartOnEnable)
                Connect(m_Host, m_Port, m_IsServer).Forget();
        }

        private void OnDisable()
        {
            Disconnect();
        }


        private void OnDestroy()
        {
            Disconnect();
        }
        #endregion





        #region Private Methods

        private void Log(object message)
        {
            if (!m_IsDebug)
                return;
            var prefix = m_IsServer ? "Server" : "Client";
            Debug.Log($"[{prefix}] {message}");
        }

        private async UniTask Reconnect(int _delay)
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
            packetSendQueue.Clear();
            receivedChunks.Clear();

            try
            {

                await UniTask.Delay(_delay, cancellationToken: token);
                m_IsWaitingReconnect = false;

                Connect(m_Host, m_Port, m_IsServer).Forget();

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
        private int GetRandomId()
        {
            return new System.Random().Next();
        }


        private async UniTask PingProcess(int _interval, CancellationToken _token)
        {


            while (!_token.IsCancellationRequested)
            {

                try
                {
                    await SendPing(_token);
                    await UniTask.Delay(_interval, cancellationToken: _token);
                }


                catch (Exception ex)
                {
                    // Log($"PingInterval Error: {ex.Message}");
                    throw ex;
                }
            }
        }


        private async UniTask ConnectionMonitorProcess(int _interval, int disconnectTime, CancellationToken _token)
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
                                InvokeOnClientDisconnected(client).Forget();
                                Log($"Client {client.ipAddress} disconnected");
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
                            InvokeOnServerDisconnect(m_ServerInfo).Forget();

                        }
                    }





                }


                catch (Exception ex)
                {
                    throw ex;
                }

            }

        }
        private async UniTask SendDataProcess(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {

                    if (packetSendQueue.Count == 0)
                    {
                        await UniTask.Yield(token);
                        continue;
                    }
                    //find request in queue
                    var list = packetSendQueue.FindAll(x => x.sendPacketStatus != SendPacketStatus.SEND_COMPLETE);

                    if (list.Count == 0)
                    {
                        await UniTask.Yield(token);
                        continue;
                    }

                    var request = list[0];

                    if (!m_IsServer)
                    {
                        //send to server
                        await udpClient.SendAsync(request.packetData, request.packetData.Length).AsUniTask(false).AttachExternalCancellation(token);
                        request.sendPacketStatus = SendPacketStatus.SEND;

                    }
                    else
                    {
                        //send to client
                        await udpClient.SendAsync(request.packetData, request.packetData.Length, request.remoteEndPoint).AsUniTask(false).AttachExternalCancellation(token);
                        request.sendPacketStatus = SendPacketStatus.SEND;

                    }

                    packetSendQueue.RemoveAt(0);


                    if (request.requestConfirm)
                    {

                        if (request.sendPacketStatus == SendPacketStatus.SEND_COMPLETE)
                        {
                            packetSendQueue.RemoveAt(0);
                        }
                        else
                        {
                            //add request back to queue
                            packetSendQueue.Add(request);
                        }

                    }


                    // await UniTask.Delay(300, cancellationToken: token);

                    await UniTask.Yield(token);





                }

            }
            catch (Exception ex)
            {
                throw ex;
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


                    switch (packetResponse.packetType)
                    {
                        case PacketType.PING:
                            UpdateConnectors(packetResponse);
                            break;
                        case PacketType.SEND_DATA:
                            //receiver side



                            var isExist = packetWaitConfirmList
                                    .FindIndex(x => x.messageId == packetResponse.messageId &&
                                    x.index == packetResponse.index &&
                                    x.id == packetResponse.senderId) != -1;

                            if (packetResponse.requestConfirm)
                            {

                                //send back to confirm
                                var confirmDataSend = new PacketConfirm()
                                {
                                    messageId = packetResponse.messageId,
                                    index = packetResponse.index,
                                    id = packetResponse.senderId
                                };

                                if (!isExist)
                                {
                                    packetWaitConfirmList.Add(confirmDataSend);

                                }

                                SendDataConfirm(confirmDataSend);

                            }




                            if (packetResponse.isCompleted)
                            {
                                //add response to list
                                //check id if not exist then Invoke
                                if (!isExist)
                                    m_OnDataReceived?.Invoke(packetResponse);

                            }

                            break;
                        case PacketType.SEND_DATA_CONFIRM:
                            //sender side
                            //remove request from queue
                            var confirmData = UnityConverter.FromBytes<PacketConfirm>(packetResponse.data);

                            var index = packetSendQueue.FindIndex(x => x.messageId == confirmData.messageId && x.index == confirmData.index && x.senderId == confirmData.id);

                            if (index != -1)
                            {
                                packetSendQueue.RemoveAt(index);
                            }
                            break;

                    }




                }


                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }



        private void UpdateConnectors(PacketResponse _packetResponse)
        {
            var senderId = _packetResponse.senderId;
            var remoteEndPoint = _packetResponse.remoteEndPoint;
            var ipAddress = remoteEndPoint.Address.ToString();

            // Log($"m_ClientInfoList count : {m_ClientInfoList.Count}");
            var timeNow = DateTime.UtcNow.Ticks;
            try
            {
                if (m_IsServer)
                {
                    var index = m_ClientInfoList.FindIndex(x => x.ipAddress == ipAddress);


                    if (index == -1)
                    {
                        Log($"Client connected: {ipAddress}");
                        var clientInfo = new ConnectorInfo()
                        {
                            id = senderId,
                            ipAddress = ipAddress,
                            lastPing = timeNow,
                            remoteEndPoint = remoteEndPoint
                        };

                        InvokeOnClientConnected(clientInfo).Forget();
                        m_ClientInfoList.Add(clientInfo);
                    }
                    else
                    {
                        var clientInfo = m_ClientInfoList[index];
                        clientInfo.lastPing = timeNow;
                        m_ClientInfoList[index] = clientInfo;
                    }
                }
                else
                {
                    m_ServerInfo.id = senderId;
                    m_ServerInfo.ipAddress = ipAddress;
                    m_ServerInfo.lastPing = timeNow;
                    m_ServerInfo.remoteEndPoint = remoteEndPoint;

                    if (!m_IsConnected)
                    {
                        InvokeOnSeverConnected(m_ServerInfo).Forget();
                    }

                    m_IsConnected = true;
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }


        }




        private List<byte[]> CreatePackets(int messageId, PacketType _packetType, DataType _dataType, ushort _action, byte[] _data, bool _requestComplete)
        {
            if (_data == null)
                _data = new byte[1]{
                0x00
            };

            List<byte[]> result;

            try
            {

                result = new List<byte[]>();

                var payloadSize = MAX_CHUNK_SIZE - PACKET_HEADER_SIZE;
                int totalChunks = (int)Math.Ceiling((double)_data.Length / payloadSize);

                for (int i = 0; i < totalChunks; i++)
                {

                    int offset = i * payloadSize;
                    int size = Math.Min(payloadSize, _data.Length - offset);
                    var packet = new byte[PACKET_HEADER_SIZE + _data.Length];
                    //header
                    Buffer.BlockCopy(BitConverter.GetBytes(messageId), 0, packet, 0, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes((ushort)totalChunks), 0, packet, 4, 2);
                    Buffer.BlockCopy(BitConverter.GetBytes((ushort)i), 0, packet, 6, 2);
                    Buffer.BlockCopy(BitConverter.GetBytes((ushort)_packetType), 0, packet, 8, 2);
                    Buffer.BlockCopy(BitConverter.GetBytes((ushort)_dataType), 0, packet, 10, 2);
                    Buffer.BlockCopy(BitConverter.GetBytes(_action), 0, packet, 12, 2);
                    Buffer.BlockCopy(BitConverter.GetBytes(_requestComplete), 0, packet, 14, 1);
                    Buffer.BlockCopy(BitConverter.GetBytes(m_Id), 0, packet, 15, 4);



                    //payload
                    Buffer.BlockCopy(_data, offset, packet, PACKET_HEADER_SIZE, size);

                    result.Add(packet);


                }
            }

            catch (Exception ex)
            {
                throw ex;
            }
            return result;
        }
        private async UniTask SendPing(CancellationToken _token)
        {
            var messageId = GetRandomId();
            var action = 0;
            var packetType = PacketType.PING;
            var dataType = DataType.NONE;

            var data = new byte[1]{
                0x00
            };

            var requests = CreatePackets(messageId, packetType, dataType, (ushort)action, data, false);
            if (requests == null)
                return;
            try
            {
                var packet = requests[0];
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
            catch (Exception ex)
            {
                throw ex;


            }
        }

        private void SendDataConfirm(PacketConfirm _confirmData, CancellationToken _token = default)
        {
            var messageId = GetRandomId();
            var action = 0;
            var packetType = PacketType.SEND_DATA_CONFIRM;
            var dataType = DataType.NONE;
            try
            {
                var confirmData = UnityConverter.ToBytes(_confirmData);
                AddPacketToQueue(messageId, packetType, dataType, (ushort)action, confirmData, false);

            }
            catch (Exception ex)
            {
                Debug.LogError($"Error converting confirm data: {ex.Message}");
            }




        }
        private void AddPacketToQueue(int messageId, PacketType _packetType, DataType _dataType, ushort _action, byte[] _data, bool _requestConfirm)
        {

            var packets = CreatePackets(messageId, _packetType, _dataType, _action, _data, _requestConfirm);
            if (packets == null)
                return;

            for (int i = 0; i < packets.Count; i++)
            {
                var packet = packets[i];
                if (m_IsServer)
                {
                    //send to all clients
                    for (int j = 0; j < m_ClientInfoList.Count; j++)
                    {
                        var client = m_ClientInfoList[j];
                        if (client.remoteEndPoint == null)
                            continue;
                        var endPoint = client.remoteEndPoint;
                        var request = new PacketRequest()
                        {
                            messageId = messageId,
                            senderId = m_Id,
                            packetType = _packetType,
                            dataType = _dataType,
                            action = _action,
                            index = i,
                            sendPacketStatus = SendPacketStatus.NONE,
                            packetData = packet,
                            remoteEndPoint = endPoint,
                            requestConfirm = _requestConfirm,
                            createdAt = DateTime.UtcNow.Ticks
                        };
                        //add request each client
                        packetSendQueue.Add(request);
                    }

                }
                else
                {

                    if (!m_IsConnected) break;

                    var request = new PacketRequest()
                    {
                        messageId = messageId,
                        senderId = m_Id,
                        packetType = _packetType,
                        dataType = _dataType,
                        action = _action,
                        index = i,
                        sendPacketStatus = SendPacketStatus.NONE,
                        packetData = packet,
                        remoteEndPoint = m_ServerInfo.remoteEndPoint,
                        requestConfirm = _requestConfirm,
                        createdAt = DateTime.UtcNow.Ticks

                    };
                    //add request to server
                    packetSendQueue.Add(request);
                }
            }



        }

        private PacketResponse ReadPacket(UdpReceiveResult _result)
        {
            var data = _result.Buffer;

            if (data == null || data.Length < PACKET_HEADER_SIZE)
                return null;

            //header
            int messageId = BitConverter.ToInt32(data, 0);
            ushort totalChunks = BitConverter.ToUInt16(data, 4);
            ushort chunkIndex = BitConverter.ToUInt16(data, 6);
            var packetType = BitConverter.ToUInt16(data, 8);
            var dataType = BitConverter.ToUInt16(data, 10);
            var action = BitConverter.ToUInt16(data, 12);
            var requestConfirm = BitConverter.ToBoolean(data, 14);
            //senderId
            var senderId = BitConverter.ToInt32(data, 15);

            //payload
            var payload = new byte[data.Length - PACKET_HEADER_SIZE];
            Buffer.BlockCopy(data, PACKET_HEADER_SIZE, payload, 0, payload.Length);

            if (!receivedChunks.ContainsKey(messageId))
            {
                receivedChunks.Add(messageId, new List<byte[]>());
            }

            // Add the chunk to the list
            receivedChunks[messageId].Add(payload);

            // Check if we have received all chunks
            if (receivedChunks[messageId].Count == totalChunks)
            {
                // Combine all chunks into a single byte array
                byte[] completePacket = new byte[totalChunks * (data.Length - PACKET_HEADER_SIZE)];
                for (int i = 0; i < totalChunks; i++)
                {
                    Buffer.BlockCopy(receivedChunks[messageId][i], 0, completePacket, i * (data.Length - PACKET_HEADER_SIZE), data.Length - PACKET_HEADER_SIZE);
                }

                // Remove the message from the dictionary
                receivedChunks.Remove(messageId);

                return new PacketResponse()
                {
                    messageId = messageId,
                    senderId = senderId,
                    packetType = (PacketType)packetType,
                    dataType = (DataType)dataType,
                    action = action,
                    index = chunkIndex,
                    isCompleted = true,
                    data = completePacket,
                    remoteEndPoint = _result.RemoteEndPoint,
                    requestConfirm = requestConfirm,
                    createdAt = DateTime.UtcNow.Ticks

                };
            }

            return new PacketResponse()
            {
                messageId = messageId,
                senderId = senderId,
                packetType = (PacketType)packetType,
                dataType = (DataType)dataType,
                action = action,
                isCompleted = false,
                index = chunkIndex,
                data = null,
                remoteEndPoint = _result.RemoteEndPoint,
                requestConfirm = requestConfirm,
                createdAt = DateTime.UtcNow.Ticks

            };



        }
        private async UniTaskVoid InvokeOnSeverConnected(ConnectorInfo _serverInfo)
        {
            await UniTask.SwitchToMainThread();
            m_OnServerConnected?.Invoke(_serverInfo);
        }
        private async UniTaskVoid InvokeOnServerDisconnect(ConnectorInfo _serverInfo)
        {
            await UniTask.SwitchToMainThread();
            m_OnServerDisconnected?.Invoke(_serverInfo);
        }
        private async UniTaskVoid InvokeOnClientConnected(ConnectorInfo _clientInfo)
        {
            await UniTask.SwitchToMainThread();
            m_OnClientConnected?.Invoke(_clientInfo);
        }

        private async UniTaskVoid InvokeOnClientDisconnected(ConnectorInfo _clientInfo)
        {
            await UniTask.SwitchToMainThread();
            m_OnClientDisconnected?.Invoke(_clientInfo);
        }




        #endregion


        #region Public Methods

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
            packetSendQueue.Clear();
            packetWaitConfirmList.Clear();
            receivedChunks.Clear();

            Log("UDP client closed");
        }


        public async UniTask Connect(string _host, int _port, bool _isServer)
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
                m_Host = _host;
                m_Port = _port;
                m_IsRunning = false;
                m_IsConnected = false;
                m_IsWaitingReconnect = false;
                m_ClientInfoList.Clear();
                m_ServerInfo.Clear();
                packetSendQueue.Clear();
                packetWaitConfirmList.Clear();
                receivedChunks.Clear();
                m_Id = GetRandomId();

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
                    PingProcess(1000, token),
                    ReceiveDataProcess(token),
                    SendDataProcess(token),
                    ConnectionMonitorProcess(1000, 1100, token)
                );



            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException ex)
            {
                Log($"Socket Error: {ex.Message}");
                if (!_isServer)
                    Reconnect(3000).Forget();
            }
            catch (ObjectDisposedException)
            {

            }
            catch (Exception ex)
            {

                Log($"Error: {ex.Message}");
                if (!_isServer)
                    Reconnect(3000).Forget();
            }


        }

        /// <summary>
        /// Send data to server or client
        /// </summary>

        public async UniTask SendDataAsync(ushort _action, DataType _dataType, byte[] _data, bool _requestComplete = false, CancellationToken _token = default)
        {
            if (udpClient == null || !m_IsRunning || _data == null || _data.Length == 0)
                return;


            //incase when client have not server info 
            if (!m_IsServer && m_ServerInfo.IsEmpty())
                return;

            //incase when server have not client info 
            if (m_IsServer && m_ClientInfoList.Count == 0)
                return;

            await UniTask.SwitchToThreadPool();
            try
            {

                var messageId = GetRandomId();
                AddPacketToQueue(messageId, PacketType.SEND_DATA, _dataType, _action, _data, _requestComplete);

                //find request in queue
                var requests = packetSendQueue.FindAll(x => x.messageId == messageId);

                //wait for all requests to be isCompleted is true or requests elements is null
                while (!_token.IsCancellationRequested)
                {
                    requests = packetSendQueue.FindAll(x => x.messageId == messageId);
                    if (requests == null || requests.Count == 0)
                        break;

                    // var allCompleted = requests.All(x => x.sendPacketStatus == SendPacketStatus.SEND_COMPLETE);
                    // if (allCompleted) break;

                    await UniTask.Yield(_token);
                }


            }

            catch (OperationCanceledException)
            {
            }
            catch (SocketException)
            {
                throw;

            }
            catch (ObjectDisposedException)
            {
                throw;

            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                await UniTask.SwitchToMainThread();
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

        #endregion




        #region Nested Classes
        [System.Serializable]
        public struct ConnectorInfo
        {
            public int id;
            public string ipAddress;
            public long lastPing;
            public IPEndPoint remoteEndPoint;

            public void Clear()
            {
                ipAddress = string.Empty;
                lastPing = -1;
                remoteEndPoint = null;
            }
            public bool IsEmpty()
            {
                return string.IsNullOrEmpty(ipAddress) && lastPing == -1 && remoteEndPoint == null;
            }
        }

        [System.Serializable]
        public class PacketConfirm
        {
            public int messageId;
            public ushort index;
            public int id;
        }


        [System.Serializable]
        public class PacketResponse
        {
            public int messageId;
            public int senderId;
            public PacketType packetType;
            public DataType dataType;
            public ushort action;
            public ushort index;
            public bool isCompleted;
            public byte[] data;
            public IPEndPoint remoteEndPoint;
            public bool requestConfirm;
            public long createdAt;

        }

        [System.Serializable]
        public class PacketRequest
        {
            public int messageId;
            public int senderId;
            public PacketType packetType;
            public DataType dataType;
            public ushort action;
            public int index;
            public SendPacketStatus sendPacketStatus;
            public byte[] packetData;
            public IPEndPoint remoteEndPoint;
            public long createdAt;
            public bool requestConfirm;
        }


        public enum PacketType
        {
            PING = 0,
            SEND_DATA = 1,
            SEND_DATA_CONFIRM = 3,

        }
        public enum SendPacketStatus
        {
            NONE,
            SEND,
            SEND_COMPLETE,
        }

        public enum DataType
        {
            NONE,
            STRING,
            BYTES,
            FLOAT,
            INT,
            LONG,
            VECTOR2,
            VECTOR3,
            VECTOR4,
            QUATERNION,
            COLOR,
            BOOL,
            DOUBLE,
            TRANSFORM

        }





        #endregion





    }
}

#if UNITY_EDITOR

namespace Modules.Utilities
{

    [CustomEditor(typeof(UDPConnector))]
    public class UDPConnectorEditor : UnityEditor.Editor
    {

        private SerializedProperty m_IpAddress;

        private void OnEnable()
        {
            m_IpAddress = serializedObject.FindProperty("m_IpAddress");
            m_IpAddress.stringValue = NetworkUtility.GetLocalIPv4();

            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            // base.OnInspectorGUI();
            var udpConnector = (UDPConnector)target;
            serializedObject.Update();
            var id = serializedObject.FindProperty("m_Id");
            var isServer = serializedObject.FindProperty("m_IsServer");

            var isRunning = serializedObject.FindProperty("m_IsRunning");
            var isConnected = serializedObject.FindProperty("m_IsConnected");
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
            EditorGUILayout.LabelField("Id", id.intValue.ToString());
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
                            udpConnector.Connect(host.stringValue, port.intValue, isServer.boolValue).Forget();

                        }
                    }



                }
                else
                {

                    GUI.color = Color.green;
                    EditorGUILayout.LabelField($"Running on Ip:{m_IpAddress.stringValue} - Port:{port.intValue}", EditorStyles.boldLabel);


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
