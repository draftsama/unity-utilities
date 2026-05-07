using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;

namespace Modules.Utilities
{
    public class DataTransceiver : MonoBehaviour
    {
        #region Constants

        private const ushort MAGIC = 0xD7C0;
        private const byte VERSION = 1;
        private const int HEADER_SIZE = 24;
        private const int MTU = 1200;
        private const int MAX_FRAGMENT_PAYLOAD = MTU - HEADER_SIZE;
        private const int CHANNEL_COUNT = 4;

        private const byte FLAG_FRAGMENTED = 0x01;
        private const byte FLAG_RELAY = 0x02;

        private const int SERVER_PEER_ID = 0;

        #endregion

        #region Inspector

        [Header("Connection")]
        [SerializeField, Tooltip("Server: bind interface (empty = all). Client: server host to connect to.")]
        public string m_Host = "127.0.0.1";

        [SerializeField, Tooltip("UDP port")]
        public int m_Port = 55555;

        [SerializeField, Tooltip("Server mode (true) or client mode (false)")]
        public bool m_IsServer = false;

        [SerializeField] public bool m_StartOnEnable = true;
        [SerializeField] public bool m_IsDebug = true;

        [Header("Reliability")]
        [SerializeField, Range(50, 2000)] public int m_InitialRtoMs = 200;
        [SerializeField, Range(200, 10000)] public int m_MaxRtoMs = 2000;
        [SerializeField, Range(0, 32), Tooltip("0 = infinite for ReliableOrdered; otherwise drop after N retries")]
        public int m_MaxRetries = 8;

        [Header("Heartbeat / Timeout")]
        [SerializeField, Range(100, 10000)] public int m_HeartbeatMs = 1000;
        [SerializeField, Range(1000, 60000)] public int m_PeerTimeoutMs = 5000;

        [Header("Handshake")]
        [SerializeField, Range(500, 30000)] public int m_HandshakeTimeoutMs = 5000;

        [Header("Auto Reconnect (client)")]
        [SerializeField] public bool m_AutoReconnect = true;
        [SerializeField] public int[] m_ReconnectDelaysMs = new int[] { 1000, 2000, 5000, 10000, 30000 };

        [Header("Discovery (UDP broadcast)")]
        [SerializeField] public bool m_EnableDiscovery = false;
        [SerializeField] public int m_DiscoveryPort = 55556;
        [SerializeField, Range(200, 10000)] public int m_DiscoveryIntervalMs = 1000;
        [SerializeField] public string m_DiscoveryTag = "DT-DISCOVER";

        [Header("Performance")]
        [SerializeField, Range(1, 4096)] public int m_MaxPayloadKB = 256;
        [SerializeField, Range(500, 60000)] public int m_FragmentTtlMs = 5000;

        [Header("Status (read-only)")]
        [SerializeField] private bool m_IsRunning;
        [SerializeField] private bool m_IsConnected;
        [SerializeField] private bool m_IsHandshaking;
        [SerializeField] private bool m_IsReconnecting;
        [SerializeField] private int m_LocalPeerId;
        [SerializeField] private int m_PeerCount;

        [Header("Events")]
        [SerializeField] private UnityEvent<PeerInfo> m_OnPeerConnected = new UnityEvent<PeerInfo>();
        [SerializeField] private UnityEvent<PeerInfo> m_OnPeerDisconnected = new UnityEvent<PeerInfo>();
        [SerializeField] private UnityEvent<DataMessage> m_OnMessageReceived = new UnityEvent<DataMessage>();
        [SerializeField] private UnityEvent m_OnReady = new UnityEvent();
        [SerializeField] private UnityEvent m_OnReconnecting = new UnityEvent();
        [SerializeField] private UnityEvent m_OnReconnected = new UnityEvent();
        [SerializeField] private UnityEvent<ErrorInfo> m_OnError = new UnityEvent<ErrorInfo>();

        #endregion

        #region Internal state

        private UdpClient _udp;
        private UdpClient _discoveryUdp;
        private IPEndPoint _serverEndpoint;
        private CancellationTokenSource _cts;
        private SemaphoreSlim _sendLock;

        private readonly ConcurrentDictionary<int, PeerState> _peers = new ConcurrentDictionary<int, PeerState>();
        private readonly ConcurrentDictionary<string, int> _endpointToPeerId = new ConcurrentDictionary<string, int>();

        private int _nextServerPeerId = 0;
        private volatile bool _handshakeReceived;
        private int _pendingAssignedPeerId = -1;

        private readonly ConcurrentQueue<Action> _mainQueue = new ConcurrentQueue<Action>();
        private readonly Dictionary<ushort, List<Action<DataMessage>>> _actionSubscribers = new Dictionary<ushort, List<Action<DataMessage>>>();
        private readonly object _subLock = new object();

        #endregion

        #region Public surface

        public UnityEvent<PeerInfo> OnPeerConnected => m_OnPeerConnected;
        public UnityEvent<PeerInfo> OnPeerDisconnected => m_OnPeerDisconnected;
        public UnityEvent<DataMessage> OnMessageReceived => m_OnMessageReceived;
        public UnityEvent OnReady => m_OnReady;
        public UnityEvent OnReconnecting => m_OnReconnecting;
        public UnityEvent OnReconnected => m_OnReconnected;
        public UnityEvent<ErrorInfo> OnError => m_OnError;

        public bool IsRunning => m_IsRunning;
        public bool IsConnected => m_IsConnected;
        public bool IsHandshaking => m_IsHandshaking;
        public bool IsReconnecting => m_IsReconnecting;
        public bool IsServer => m_IsServer;
        public int LocalPeerId => m_LocalPeerId;
        public int PeerCount => m_PeerCount;

        public IReadOnlyCollection<PeerInfo> Peers
        {
            get
            {
                var list = new List<PeerInfo>(_peers.Count);
                foreach (var kv in _peers) list.Add(kv.Value.ToInfo());
                return list;
            }
        }

        public PeerStats GetStats(int peerId)
        {
            return _peers.TryGetValue(peerId, out var p) ? p.GetStats() : default;
        }

        public IDisposable Subscribe(ushort action, Action<DataMessage> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            lock (_subLock)
            {
                if (!_actionSubscribers.TryGetValue(action, out var list))
                {
                    list = new List<Action<DataMessage>>();
                    _actionSubscribers[action] = list;
                }
                list.Add(handler);
            }
            return new SubscriptionToken(this, action, handler);
        }

        #endregion

        #region Unity lifecycle

        private void OnEnable()
        {
            if (m_StartOnEnable) StartAsync().Forget();
        }

        private void OnDisable() => StopInternal(fireEvents: true);
        private void OnDestroy() => StopInternal(fireEvents: false);

        private void Update()
        {
            // Drain main-thread dispatch queue.
            while (_mainQueue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { Log($"Main dispatch error: {ex.Message}"); }
            }

            m_PeerCount = _peers.Count;
        }

        #endregion

        #region Public API — lifecycle

        public async UniTask StartAsync(CancellationToken ct = default)
        {
            if (m_IsRunning) return;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = SafeToken();
            _sendLock = new SemaphoreSlim(1, 1);
            _peers.Clear();
            _endpointToPeerId.Clear();
            _nextServerPeerId = 0;
            _handshakeReceived = false;
            _pendingAssignedPeerId = -1;

            try
            {
                if (m_IsServer) StartServerSocket();
                else StartClientSocket();

                m_IsRunning = true;

                if (m_EnableDiscovery)
                {
                    if (m_IsServer) DiscoveryServerLoop(token).Forget();
                    else DiscoveryClientLoop(token).Forget();
                }

                ReceiveLoop(token).Forget();
                RetransmitLoop(token).Forget();
                HeartbeatLoop(token).Forget();
                TimeoutLoop(token).Forget();
                FragmentGcLoop(token).Forget();

                if (m_IsServer)
                {
                    m_LocalPeerId = SERVER_PEER_ID;
                    m_IsConnected = true;
                    DispatchMain(() => m_OnReady?.Invoke());
                }
                else
                {
                    await PerformClientHandshake(token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ReportError(ErrorType.StartFailed, ex.Message, ex, default);
                StopInternal(fireEvents: false);
                if (!m_IsServer && m_AutoReconnect) ScheduleReconnect();
            }
        }

        public UniTask StopAsync()
        {
            StopInternal(fireEvents: true);
            return UniTask.CompletedTask;
        }

        #endregion

        #region Public API — send

        public UniTask SendAsync(ushort action, byte[] data, ReliabilityMode mode = ReliabilityMode.ReliableOrdered, CancellationToken ct = default)
        {
            if (!m_IsRunning) return UniTask.CompletedTask;
            if (data == null) data = Array.Empty<byte>();
            ValidatePayloadSize(data.Length);

            if (m_IsServer)
            {
                // Server: broadcast to every peer.
                return BroadcastInternal(action, data, mode, exceptPeerId: -1, isRelay: false, ct);
            }
            // Client: send to server.
            return SendToPeerAsync(SERVER_PEER_ID, action, data, mode, isRelay: false, ct);
        }

        public UniTask SendAsync<T>(ushort action, T data, ReliabilityMode mode = ReliabilityMode.ReliableOrdered, CancellationToken ct = default)
        {
            var json = JsonConvert.SerializeObject(data);
            return SendAsync(action, Encoding.UTF8.GetBytes(json), mode, ct);
        }

        public UniTask SendToAsync(int peerId, ushort action, byte[] data, ReliabilityMode mode = ReliabilityMode.ReliableOrdered, CancellationToken ct = default)
        {
            if (!m_IsServer) throw new InvalidOperationException("SendToAsync is server-only. Use SendAsync from client.");
            if (data == null) data = Array.Empty<byte>();
            ValidatePayloadSize(data.Length);
            return SendToPeerAsync(peerId, action, data, mode, isRelay: false, ct);
        }

        public UniTask BroadcastAsync(ushort action, byte[] data, ReliabilityMode mode = ReliabilityMode.ReliableOrdered, int exceptPeerId = -1, CancellationToken ct = default)
        {
            if (!m_IsServer) throw new InvalidOperationException("BroadcastAsync is server-only.");
            if (data == null) data = Array.Empty<byte>();
            ValidatePayloadSize(data.Length);
            return BroadcastInternal(action, data, mode, exceptPeerId, isRelay: false, ct);
        }

        public UniTask RelayAsync(int targetPeerId, ushort action, byte[] data, ReliabilityMode mode = ReliabilityMode.ReliableOrdered, CancellationToken ct = default)
        {
            if (m_IsServer) throw new InvalidOperationException("RelayAsync is client-only.");
            if (data == null) data = Array.Empty<byte>();
            ValidatePayloadSize(data.Length + 4);
            // Prefix payload with target peer id so the server can route.
            var routed = new byte[4 + data.Length];
            BitConverter.GetBytes(targetPeerId).CopyTo(routed, 0);
            Buffer.BlockCopy(data, 0, routed, 4, data.Length);
            return SendToPeerAsync(SERVER_PEER_ID, action, routed, mode, isRelay: true, ct);
        }

        #endregion

        #region Send pipeline

        private async UniTask SendToPeerAsync(int peerId, ushort action, byte[] payload, ReliabilityMode mode, bool isRelay, CancellationToken ct)
        {
            if (!_peers.TryGetValue(peerId, out var peer))
            {
                Log($"SendToPeerAsync: peer {peerId} not found");
                return;
            }

            var fragments = Fragment(payload);
            var ch = (byte)mode;
            var fragmentId = (uint)Mathf.Abs(Guid.NewGuid().GetHashCode());
            var flags = (byte)((fragments.Count > 1 ? FLAG_FRAGMENTED : 0) | (isRelay ? FLAG_RELAY : 0));

            var sendTasks = new List<UniTask>(fragments.Count);
            for (var i = 0; i < fragments.Count; i++)
            {
                var seq = peer.Channels[ch].NextSendSeq();
                var pkt = BuildPacket(PacketType.Data, mode, flags, action, seq,
                    peer.Channels[ch].LastReceivedSeq, fragmentId, (ushort)i, (ushort)fragments.Count, fragments[i]);

                if (mode == ReliabilityMode.ReliableOrdered || mode == ReliabilityMode.ReliableUnordered)
                {
                    var nowTicks = DateTime.UtcNow.Ticks;
                    var pending = new PendingPacket
                    {
                        Sequence = seq,
                        Channel = ch,
                        Bytes = pkt,
                        SentAtTicks = nowTicks,
                        NextRetryTicks = nowTicks + TimeSpan.TicksPerMillisecond * m_InitialRtoMs,
                        Retries = 0,
                        Mode = mode,
                    };
                    peer.Channels[ch].Pending[seq] = pending;
                }

                sendTasks.Add(SendBytesToEndpoint(pkt, peer.EndPoint, ct));
            }
            await UniTask.WhenAll(sendTasks);
        }

        private async UniTask BroadcastInternal(ushort action, byte[] payload, ReliabilityMode mode, int exceptPeerId, bool isRelay, CancellationToken ct)
        {
            var tasks = new List<UniTask>();
            foreach (var kv in _peers)
            {
                if (kv.Key == exceptPeerId) continue;
                tasks.Add(SendToPeerAsync(kv.Key, action, payload, mode, isRelay, ct));
            }
            if (tasks.Count > 0) await UniTask.WhenAll(tasks);
        }

        private async UniTask SendBytesToEndpoint(byte[] bytes, IPEndPoint endpoint, CancellationToken ct)
        {
            if (_udp == null) return;
            await _sendLock.WaitAsync(ct);
            try
            {
                await _udp.SendAsync(bytes, bytes.Length, endpoint).AsUniTask().AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                Log($"SendBytes error: {ex.Message}");
            }
            finally
            {
                if (_sendLock != null) _sendLock.Release();
            }
        }

        private List<byte[]> Fragment(byte[] payload)
        {
            var result = new List<byte[]>();
            if (payload.Length == 0)
            {
                result.Add(Array.Empty<byte>());
                return result;
            }
            for (var offset = 0; offset < payload.Length; offset += MAX_FRAGMENT_PAYLOAD)
            {
                var size = Math.Min(MAX_FRAGMENT_PAYLOAD, payload.Length - offset);
                var slice = new byte[size];
                Buffer.BlockCopy(payload, offset, slice, 0, size);
                result.Add(slice);
            }
            return result;
        }

        #endregion

        #region Receive pipeline

        private async UniTaskVoid ReceiveLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await _udp.ReceiveAsync().AsUniTask().AttachExternalCancellation(ct);
                }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (Exception ex)
                {
                    Log($"Receive error: {ex.Message}");
                    await UniTask.Delay(50, cancellationToken: ct);
                    continue;
                }

                try
                {
                    HandleIncoming(result.Buffer, result.RemoteEndPoint, ct);
                }
                catch (Exception ex)
                {
                    Log($"Handle error: {ex.Message}");
                }
            }
        }

        private void HandleIncoming(byte[] buffer, IPEndPoint sender, CancellationToken ct)
        {
            if (buffer == null || buffer.Length < HEADER_SIZE) return;
            if (BitConverter.ToUInt16(buffer, 0) != MAGIC) return;
            if (buffer[2] != VERSION) return;

            var packetType = (PacketType)buffer[3];
            var channel = buffer[4];
            var flags = buffer[5];
            var action = BitConverter.ToUInt16(buffer, 6);
            var sequence = BitConverter.ToUInt32(buffer, 8);
            var ackSequence = BitConverter.ToUInt32(buffer, 12);
            var fragmentId = BitConverter.ToUInt32(buffer, 16);
            var fragIndex = BitConverter.ToUInt16(buffer, 20);
            var fragTotal = BitConverter.ToUInt16(buffer, 22);

            switch (packetType)
            {
                case PacketType.Syn: HandleSyn(sender, sequence, ct); return;
                case PacketType.SynAck: HandleSynAck(sender, sequence, action); return;
                case PacketType.Fin: HandleFin(sender); return;
                case PacketType.Ping: HandlePing(sender); return;
                case PacketType.Pong: HandlePong(sender); return;
                case PacketType.Ack: HandleAck(sender, channel, sequence); return;
                case PacketType.Data: break;
                default: return;
            }

            // DATA path
            if (channel >= CHANNEL_COUNT) return;
            if (!_endpointToPeerId.TryGetValue(EndpointKey(sender), out var peerId)) return;
            if (!_peers.TryGetValue(peerId, out var peer)) return;

            peer.LastSeenTicks = DateTime.UtcNow.Ticks;
            var chState = peer.Channels[channel];

            // Process piggyback ack from sender (their lastReceivedSeq from us).
            if (ackSequence != 0) chState.RemovePending(ackSequence);

            // For reliable channels: send dedicated ACK back.
            if (channel == (byte)ReliabilityMode.ReliableOrdered || channel == (byte)ReliabilityMode.ReliableUnordered)
            {
                SendAckPacket(sender, channel, sequence, ct);
            }

            // Sequenced drop check.
            if (channel == (byte)ReliabilityMode.UnreliableSequenced)
            {
                if (SeqLessOrEqual(sequence, chState.LastDeliveredSeq) && chState.LastDeliveredSeq != 0) return;
                chState.LastDeliveredSeq = sequence;
            }

            // Ordered: dedup by seq.
            if (channel == (byte)ReliabilityMode.ReliableOrdered || channel == (byte)ReliabilityMode.ReliableUnordered)
            {
                if (!chState.MarkReceived(sequence)) return; // duplicate
            }

            chState.LastReceivedSeq = sequence;

            // Reassemble payload bytes.
            var payloadLen = buffer.Length - HEADER_SIZE;
            var payload = new byte[payloadLen];
            Buffer.BlockCopy(buffer, HEADER_SIZE, payload, 0, payloadLen);

            byte[] complete = null;
            if ((flags & FLAG_FRAGMENTED) == 0 || fragTotal <= 1)
            {
                complete = payload;
            }
            else
            {
                complete = peer.AddFragment(fragmentId, fragIndex, fragTotal, payload, m_FragmentTtlMs);
                if (complete == null) return; // not yet complete
            }

            // For ordered channel: deliver in order, buffering gaps.
            if (channel == (byte)ReliabilityMode.ReliableOrdered)
            {
                chState.PushOrdered(sequence, action, complete);
                while (chState.TryPopNextOrdered(out var ordered))
                {
                    DeliverMessage(peer, ordered.Action, ordered.Payload, ReliabilityMode.ReliableOrdered, flags);
                }
                return;
            }

            DeliverMessage(peer, action, complete, (ReliabilityMode)channel, flags);
        }

        private void DeliverMessage(PeerState peer, ushort action, byte[] payload, ReliabilityMode mode, byte flags)
        {
            // Server-side relay: route payload to target peer instead of delivering locally.
            if (m_IsServer && (flags & FLAG_RELAY) != 0 && payload != null && payload.Length >= 4)
            {
                var targetId = BitConverter.ToInt32(payload, 0);
                var inner = new byte[payload.Length - 4];
                Buffer.BlockCopy(payload, 4, inner, 0, inner.Length);

                if (targetId == SERVER_PEER_ID)
                {
                    // Treat as message to server.
                    DispatchMessage(peer.Id, action, inner, mode);
                    return;
                }
                SendToPeerAsync(targetId, action, inner, mode, isRelay: false, SafeToken()).Forget();
                return;
            }

            DispatchMessage(peer.Id, action, payload, mode);
        }

        private void DispatchMessage(int senderId, ushort action, byte[] payload, ReliabilityMode mode)
        {
            var msg = new DataMessage(senderId, action, payload, mode, DateTime.UtcNow.Ticks);
            DispatchMain(() =>
            {
                Log($"Received message from peer {senderId}: action={action}, size={payload?.Length ?? 0}, mode={mode}");
                try { m_OnMessageReceived?.Invoke(msg); } catch (Exception ex) { Log($"OnMessageReceived handler threw: {ex.Message}"); }

                List<Action<DataMessage>> handlers = null;
                lock (_subLock)
                {
                    if (_actionSubscribers.TryGetValue(action, out var list))
                        handlers = new List<Action<DataMessage>>(list);
                }
                if (handlers != null)
                {
                    foreach (var h in handlers)
                    {
                        try { h(msg); } catch (Exception ex) { Log($"Subscriber threw: {ex.Message}"); }
                    }
                }
            });
        }

        #endregion

        #region Handshake & control packets

        private async UniTask PerformClientHandshake(CancellationToken ct)
        {
            m_IsHandshaking = true;
            _handshakeReceived = false;
            _pendingAssignedPeerId = -1;

            // Pre-register the server as peer 0 so we can send/receive immediately.
            var serverPeer = _peers.GetOrAdd(SERVER_PEER_ID, _ => new PeerState(SERVER_PEER_ID, _serverEndpoint));
            _endpointToPeerId[EndpointKey(_serverEndpoint)] = SERVER_PEER_ID;

            var syn = BuildPacket(PacketType.Syn, ReliabilityMode.Unreliable, 0, action: 0, sequence: 0, ackSeq: 0, 0, 0, 0, Array.Empty<byte>());

            var deadline = DateTime.UtcNow.AddMilliseconds(m_HandshakeTimeoutMs);
            while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested && !_handshakeReceived)
            {
                SendBytesToEndpoint(syn, _serverEndpoint, ct).Forget();
                for (var i = 0; i < 50 && !_handshakeReceived && !ct.IsCancellationRequested; i++)
                    await UniTask.Delay(10, cancellationToken: ct);
            }

            if (_handshakeReceived)
            {
                m_LocalPeerId = _pendingAssignedPeerId;
                m_IsHandshaking = false;
                m_IsConnected = true;
                DispatchMain(() =>
                {
                    m_OnPeerConnected?.Invoke(serverPeer.ToInfo());
                    m_OnReady?.Invoke();
                    if (m_IsReconnecting)
                    {
                        m_IsReconnecting = false;
                        m_OnReconnected?.Invoke();
                    }
                });
                return;
            }

            m_IsHandshaking = false;
            ReportError(ErrorType.HandshakeTimeout, "Handshake timed out", null, serverPeer.ToInfo());
            StopInternal(fireEvents: false);
            if (m_AutoReconnect) ScheduleReconnect();
        }

        private void HandleSyn(IPEndPoint sender, uint senderSeq, CancellationToken ct)
        {
            if (!m_IsServer) return;
            var key = EndpointKey(sender);
            int peerId;
            if (_endpointToPeerId.TryGetValue(key, out peerId))
            {
                // Existing peer re-sending SYN; just resend SYN_ACK.
            }
            else
            {
                peerId = Interlocked.Increment(ref _nextServerPeerId);
                var peer = new PeerState(peerId, sender);
                _peers[peerId] = peer;
                _endpointToPeerId[key] = peerId;
                DispatchMain(() => m_OnPeerConnected?.Invoke(peer.ToInfo()));
            }

            // SYN_ACK: encode assigned peerId in the action field (small ints fit; larger via payload too for safety).
            var payload = BitConverter.GetBytes(peerId);
            var synAck = BuildPacket(PacketType.SynAck, ReliabilityMode.Unreliable, 0,
                action: (ushort)(peerId & 0xFFFF), sequence: 0, ackSeq: 0, 0, 0, 0, payload);
            SendBytesToEndpoint(synAck, sender, ct).Forget();
        }

        private void HandleSynAck(IPEndPoint sender, uint seq, ushort actionLowBits)
        {
            if (m_IsServer || _handshakeReceived) return;
            _pendingAssignedPeerId = actionLowBits;
            _handshakeReceived = true;
        }

        private void HandleFin(IPEndPoint sender)
        {
            var key = EndpointKey(sender);
            if (!_endpointToPeerId.TryRemove(key, out var peerId)) return;
            if (_peers.TryRemove(peerId, out var peer))
                DispatchMain(() => m_OnPeerDisconnected?.Invoke(peer.ToInfo()));
        }

        private void HandlePing(IPEndPoint sender)
        {
            var key = EndpointKey(sender);
            if (_endpointToPeerId.TryGetValue(key, out var peerId) && _peers.TryGetValue(peerId, out var peer))
            {
                peer.LastSeenTicks = DateTime.UtcNow.Ticks;
            }
            var pong = BuildPacket(PacketType.Pong, ReliabilityMode.Unreliable, 0, 0, 0, 0, 0, 0, 0, Array.Empty<byte>());
            SendBytesToEndpoint(pong, sender, SafeToken()).Forget();
        }

        private void HandlePong(IPEndPoint sender)
        {
            var key = EndpointKey(sender);
            if (_endpointToPeerId.TryGetValue(key, out var peerId) && _peers.TryGetValue(peerId, out var peer))
            {
                peer.LastSeenTicks = DateTime.UtcNow.Ticks;
            }
        }

        private void HandleAck(IPEndPoint sender, byte channel, uint sequence)
        {
            if (channel >= CHANNEL_COUNT) return;
            var key = EndpointKey(sender);
            if (!_endpointToPeerId.TryGetValue(key, out var peerId)) return;
            if (!_peers.TryGetValue(peerId, out var peer)) return;
            peer.Channels[channel].RemovePending(sequence, recordRtt: peer);
        }

        private void SendAckPacket(IPEndPoint dest, byte channel, uint sequence, CancellationToken ct)
        {
            var ack = BuildPacket(PacketType.Ack, (ReliabilityMode)channel, 0, 0, sequence, 0, 0, 0, 0, Array.Empty<byte>());
            SendBytesToEndpoint(ack, dest, ct).Forget();
        }

        #endregion

        #region Background loops

        private async UniTaskVoid RetransmitLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(50, cancellationToken: ct);
                var nowTicks = DateTime.UtcNow.Ticks;
                foreach (var kv in _peers)
                {
                    var peer = kv.Value;
                    for (var c = 0; c < CHANNEL_COUNT; c++)
                    {
                        var ch = peer.Channels[c];
                        if (ch.Pending.Count == 0) continue;
                        foreach (var pendingKv in ch.Pending)
                        {
                            var p = pendingKv.Value;
                            if (p.NextRetryTicks > nowTicks) continue;
                            if (m_MaxRetries > 0 && p.Retries >= m_MaxRetries && p.Mode == ReliabilityMode.ReliableUnordered)
                            {
                                ch.Pending.TryRemove(pendingKv.Key, out _);
                                continue;
                            }
                            p.Retries++;
                            var rtoMs = Math.Min(m_MaxRtoMs, m_InitialRtoMs * (1 << Math.Min(p.Retries, 6)));
                            p.NextRetryTicks = nowTicks + TimeSpan.TicksPerMillisecond * rtoMs;
                            SendBytesToEndpoint(p.Bytes, peer.EndPoint, ct).Forget();
                        }
                    }
                }
            }
        }

        private async UniTaskVoid HeartbeatLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(m_HeartbeatMs, cancellationToken: ct);
                var ping = BuildPacket(PacketType.Ping, ReliabilityMode.Unreliable, 0, 0, 0, 0, 0, 0, 0, Array.Empty<byte>());
                foreach (var kv in _peers)
                {
                    SendBytesToEndpoint(ping, kv.Value.EndPoint, ct).Forget();
                }
            }
        }

        private async UniTaskVoid TimeoutLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(500, cancellationToken: ct);
                var nowTicks = DateTime.UtcNow.Ticks;
                var timeoutTicks = TimeSpan.TicksPerMillisecond * m_PeerTimeoutMs;
                foreach (var kv in _peers)
                {
                    var peer = kv.Value;
                    if (peer.LastSeenTicks > 0 && (nowTicks - peer.LastSeenTicks) > timeoutTicks)
                    {
                        if (_peers.TryRemove(peer.Id, out _))
                        {
                            _endpointToPeerId.TryRemove(EndpointKey(peer.EndPoint), out _);
                            DispatchMain(() => m_OnPeerDisconnected?.Invoke(peer.ToInfo()));

                            // Client lost server: trigger reconnect.
                            if (!m_IsServer && peer.Id == SERVER_PEER_ID)
                            {
                                m_IsConnected = false;
                                if (m_AutoReconnect) ScheduleReconnect();
                            }
                        }
                    }
                }
            }
        }

        private async UniTaskVoid FragmentGcLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(1000, cancellationToken: ct);
                var nowTicks = DateTime.UtcNow.Ticks;
                var ttlTicks = TimeSpan.TicksPerMillisecond * m_FragmentTtlMs;
                foreach (var kv in _peers) kv.Value.GcFragments(nowTicks, ttlTicks);
            }
        }

        #endregion

        #region Discovery

        private async UniTaskVoid DiscoveryServerLoop(CancellationToken ct)
        {
            try
            {
                _discoveryUdp = new UdpClient();
                _discoveryUdp.EnableBroadcast = true;
                _discoveryUdp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                var bcast = new IPEndPoint(IPAddress.Broadcast, m_DiscoveryPort);
                var msgString = $"{m_DiscoveryTag}|{m_Port}";
                var bytes = Encoding.UTF8.GetBytes(msgString);

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await _discoveryUdp.SendAsync(bytes, bytes.Length, bcast).AsUniTask().AttachExternalCancellation(ct);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (ObjectDisposedException) { break; }
                    catch (Exception ex) { Log($"Discovery send error: {ex.Message}"); }
                    await UniTask.Delay(m_DiscoveryIntervalMs, cancellationToken: ct);
                }
            }
            finally
            {
                _discoveryUdp?.Close();
                _discoveryUdp = null;
            }
        }

        private async UniTaskVoid DiscoveryClientLoop(CancellationToken ct)
        {
            try
            {
                _discoveryUdp = new UdpClient();
                _discoveryUdp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _discoveryUdp.Client.Bind(new IPEndPoint(IPAddress.Any, m_DiscoveryPort));

                while (!ct.IsCancellationRequested)
                {
                    UdpReceiveResult res;
                    try { res = await _discoveryUdp.ReceiveAsync().AsUniTask().AttachExternalCancellation(ct); }
                    catch (OperationCanceledException) { return; }
                    catch (ObjectDisposedException) { return; }
                    catch (Exception ex) { Log($"Discovery recv error: {ex.Message}"); continue; }

                    var text = Encoding.UTF8.GetString(res.Buffer);
                    if (!text.StartsWith(m_DiscoveryTag)) continue;
                    var parts = text.Split('|');
                    if (parts.Length < 2) continue;
                    if (!int.TryParse(parts[1], out var serverPort)) continue;

                    var newHost = res.RemoteEndPoint.Address.ToString();
                    if (m_IsConnected || m_Host == newHost) continue;

                    m_Host = newHost;
                    m_Port = serverPort;
                    Log($"Discovered server at {newHost}:{serverPort}");
                }
            }
            finally
            {
                _discoveryUdp?.Close();
                _discoveryUdp = null;
            }
        }

        #endregion

        #region Reconnection

        private void ScheduleReconnect()
        {
            if (m_IsServer || !m_AutoReconnect) return;
            ReconnectAsync().Forget();
        }

        private async UniTaskVoid ReconnectAsync()
        {
            m_IsReconnecting = true;
            DispatchMain(() => m_OnReconnecting?.Invoke());
            var attempt = 0;
            var delays = (m_ReconnectDelaysMs == null || m_ReconnectDelaysMs.Length == 0)
                ? new[] { 1000, 2000, 5000 } : m_ReconnectDelaysMs;
            while (m_IsReconnecting && this != null)
            {
                var delay = delays[Math.Min(attempt, delays.Length - 1)];
                try { await UniTask.Delay(delay); }
                catch (OperationCanceledException) { return; }
                if (!m_IsReconnecting) return;
                attempt++;
                try { await StartAsync(); }
                catch (Exception ex) { Log($"Reconnect attempt failed: {ex.Message}"); }
                if (m_IsConnected) { m_IsReconnecting = false; return; }
            }
        }

        #endregion

        #region Socket setup & teardown

        private void StartServerSocket()
        {
            var bindAddr = IPAddress.Any;
            if (!string.IsNullOrEmpty(m_Host) && m_Host != "0.0.0.0" && !IPAddress.TryParse(m_Host, out bindAddr))
                bindAddr = IPAddress.Any;
            _udp = new UdpClient(new IPEndPoint(bindAddr, m_Port));
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            Log($"Server bound on {bindAddr}:{m_Port}");
        }

        private void StartClientSocket()
        {
            var resolved = ResolveHost(m_Host);
            _serverEndpoint = new IPEndPoint(resolved, m_Port);
            _udp = new UdpClient(0);
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            Log($"Client targeting {resolved}:{m_Port}");
        }

        private static IPAddress ResolveHost(string host)
        {
            if (IPAddress.TryParse(host, out var ip)) return ip;
            var entries = Dns.GetHostAddresses(host);
            foreach (var e in entries)
                if (e.AddressFamily == AddressFamily.InterNetwork) return e;
            throw new InvalidOperationException($"Cannot resolve host '{host}'");
        }

        private void StopInternal(bool fireEvents)
        {
            if (!m_IsRunning && _cts == null) return;

            // Send FIN to all peers (best effort).
            try
            {
                if (_udp != null)
                {
                    var fin = BuildPacket(PacketType.Fin, ReliabilityMode.Unreliable, 0, 0, 0, 0, 0, 0, 0, Array.Empty<byte>());
                    foreach (var kv in _peers)
                    {
                        try { _udp.Send(fin, fin.Length, kv.Value.EndPoint); } catch { }
                    }
                }
            }
            catch { }

            try { _cts?.Cancel(); } catch { }
            try { _cts?.Dispose(); } catch { }
            _cts = null;

            try { _udp?.Close(); } catch { }
            _udp = null;
            try { _discoveryUdp?.Close(); } catch { }
            _discoveryUdp = null;

            try { _sendLock?.Dispose(); } catch { }
            _sendLock = null;

            var snapshot = new List<PeerState>(_peers.Values);
            _peers.Clear();
            _endpointToPeerId.Clear();
            m_IsRunning = false;
            m_IsConnected = false;
            m_IsHandshaking = false;
            m_LocalPeerId = m_IsServer ? SERVER_PEER_ID : -1;
            _handshakeReceived = false;
            _pendingAssignedPeerId = -1;

            if (fireEvents)
            {
                foreach (var p in snapshot)
                    DispatchMain(() => m_OnPeerDisconnected?.Invoke(p.ToInfo()));
            }
        }

        #endregion

        #region Encode / decode

        private byte[] BuildPacket(PacketType type, ReliabilityMode mode, byte flags, ushort action,
            uint sequence, uint ackSeq, uint fragmentId, ushort fragIndex, ushort fragTotal, byte[] payload)
        {
            var len = HEADER_SIZE + (payload?.Length ?? 0);
            var pkt = new byte[len];
            BitConverter.GetBytes(MAGIC).CopyTo(pkt, 0);
            pkt[2] = VERSION;
            pkt[3] = (byte)type;
            pkt[4] = (byte)mode;
            pkt[5] = flags;
            BitConverter.GetBytes(action).CopyTo(pkt, 6);
            BitConverter.GetBytes(sequence).CopyTo(pkt, 8);
            BitConverter.GetBytes(ackSeq).CopyTo(pkt, 12);
            BitConverter.GetBytes(fragmentId).CopyTo(pkt, 16);
            BitConverter.GetBytes(fragIndex).CopyTo(pkt, 20);
            BitConverter.GetBytes(fragTotal).CopyTo(pkt, 22);
            if (payload != null && payload.Length > 0)
                Buffer.BlockCopy(payload, 0, pkt, HEADER_SIZE, payload.Length);
            return pkt;
        }

        private static string EndpointKey(IPEndPoint ep) => ep == null ? "" : $"{ep.Address}:{ep.Port}";

        private static bool SeqLessOrEqual(uint a, uint b)
        {
            // Modular comparison (treats wraparound).
            return ((int)(a - b)) <= 0;
        }

        #endregion

        #region Helpers / errors / unsubscribe

        private void DispatchMain(Action action) => _mainQueue.Enqueue(action);

        private CancellationToken SafeToken() => _cts != null ? _cts.Token : CancellationToken.None;

        internal void Unsubscribe(ushort action, Action<DataMessage> handler)
        {
            lock (_subLock)
            {
                if (_actionSubscribers.TryGetValue(action, out var list))
                {
                    list.Remove(handler);
                    if (list.Count == 0) _actionSubscribers.Remove(action);
                }
            }
        }

        private void ValidatePayloadSize(int sizeBytes)
        {
            var maxBytes = m_MaxPayloadKB * 1024;
            if (sizeBytes > maxBytes)
                throw new ArgumentException($"Payload {sizeBytes}B exceeds max {maxBytes}B (m_MaxPayloadKB={m_MaxPayloadKB})");
        }

        private void Log(object message)
        {
            if (!m_IsDebug) return;
            var prefix = m_IsServer ? "DT-Server" : "DT-Client";
            Debug.Log($"[{prefix}] {message}");
        }

        private void ReportError(ErrorType type, string message, Exception ex, PeerInfo peer)
        {
            Log($"ERROR [{type}] {message}");
            var info = new ErrorInfo { Type = type, Message = message, Exception = ex, Peer = peer };
            DispatchMain(() => m_OnError?.Invoke(info));
        }

        #endregion

        #region Nested types

        public enum ReliabilityMode : byte
        {
            ReliableOrdered = 0,
            ReliableUnordered = 1,
            UnreliableSequenced = 2,
            Unreliable = 3,
        }

        public enum PacketType : byte
        {
            Data = 0,
            Ack = 1,
            Syn = 2,
            SynAck = 3,
            Fin = 4,
            Ping = 5,
            Pong = 6,
        }

        public enum ErrorType
        {
            StartFailed,
            HandshakeTimeout,
            SendFailed,
            ReceiveFailed,
            PeerTimeout,
        }

        [Serializable]
        public struct PeerInfo
        {
            public int Id;
            public string IpAddress;
            public int Port;
            public long ConnectedAtTicks;
            public long LastSeenTicks;
        }

        public struct PeerStats
        {
            public float RttMs;
            public int PendingAcks;
        }

        public readonly struct DataMessage
        {
            public readonly int SenderPeerId;
            public readonly ushort Action;
            public readonly byte[] Data;
            public readonly ReliabilityMode Mode;
            public readonly long ReceivedTicks;

            public DataMessage(int senderPeerId, ushort action, byte[] data, ReliabilityMode mode, long ticks)
            {
                SenderPeerId = senderPeerId;
                Action = action;
                Data = data;
                Mode = mode;
                ReceivedTicks = ticks;
            }

            public string AsString() => Data == null || Data.Length == 0 ? string.Empty : Encoding.UTF8.GetString(Data);
            public T As<T>() => JsonConvert.DeserializeObject<T>(AsString());
        }

        public class ErrorInfo
        {
            public ErrorType Type;
            public string Message;
            public Exception Exception;
            public PeerInfo Peer;
        }

        private class PeerState
        {
            public readonly int Id;
            public IPEndPoint EndPoint;
            public long ConnectedAtTicks;
            public long LastSeenTicks;
            public float RttMs;
            public readonly ChannelState[] Channels = new ChannelState[CHANNEL_COUNT];
            private readonly ConcurrentDictionary<uint, FragmentBuffer> _fragments = new ConcurrentDictionary<uint, FragmentBuffer>();

            public PeerState(int id, IPEndPoint ep)
            {
                Id = id;
                EndPoint = ep;
                ConnectedAtTicks = DateTime.UtcNow.Ticks;
                LastSeenTicks = ConnectedAtTicks;
                for (var i = 0; i < CHANNEL_COUNT; i++) Channels[i] = new ChannelState();
            }

            public PeerInfo ToInfo() => new PeerInfo
            {
                Id = Id,
                IpAddress = EndPoint?.Address?.ToString() ?? "",
                Port = EndPoint?.Port ?? 0,
                ConnectedAtTicks = ConnectedAtTicks,
                LastSeenTicks = LastSeenTicks,
            };

            public PeerStats GetStats()
            {
                var pending = 0;
                for (var i = 0; i < Channels.Length; i++) pending += Channels[i].Pending.Count;
                return new PeerStats { RttMs = RttMs, PendingAcks = pending };
            }

            public byte[] AddFragment(uint fragmentId, ushort index, ushort total, byte[] data, int ttlMs)
            {
                var buf = _fragments.GetOrAdd(fragmentId, _ => new FragmentBuffer(total));
                buf.Touch();
                lock (buf.Lock)
                {
                    if (buf.Chunks[index] != null) return null;
                    buf.Chunks[index] = data;
                    buf.Received++;
                    if (buf.Received != buf.Total) return null;
                    var totalLen = 0;
                    for (var i = 0; i < buf.Total; i++) totalLen += buf.Chunks[i].Length;
                    var combined = new byte[totalLen];
                    var offset = 0;
                    for (var i = 0; i < buf.Total; i++)
                    {
                        Buffer.BlockCopy(buf.Chunks[i], 0, combined, offset, buf.Chunks[i].Length);
                        offset += buf.Chunks[i].Length;
                    }
                    _fragments.TryRemove(fragmentId, out _);
                    return combined;
                }
            }

            public void GcFragments(long nowTicks, long ttlTicks)
            {
                foreach (var kv in _fragments)
                {
                    if ((nowTicks - kv.Value.LastTouchTicks) > ttlTicks)
                        _fragments.TryRemove(kv.Key, out _);
                }
            }
        }

        private class FragmentBuffer
        {
            public readonly byte[][] Chunks;
            public int Received;
            public readonly int Total;
            public long LastTouchTicks;
            public readonly object Lock = new object();
            public FragmentBuffer(int total)
            {
                Total = total;
                Chunks = new byte[total][];
                LastTouchTicks = DateTime.UtcNow.Ticks;
            }
            public void Touch() => LastTouchTicks = DateTime.UtcNow.Ticks;
        }

        private class ChannelState
        {
            private uint _nextSendSeq = 1;
            public uint LastReceivedSeq;
            public uint LastDeliveredSeq;
            public readonly ConcurrentDictionary<uint, PendingPacket> Pending = new ConcurrentDictionary<uint, PendingPacket>();

            // Dedup window for reliable channels (last 1024 seqs received).
            private readonly HashSet<uint> _recentSeqs = new HashSet<uint>();
            private readonly Queue<uint> _recentOrder = new Queue<uint>();
            private const int RECENT_WINDOW = 1024;

            // Ordered delivery buffer.
            private readonly Dictionary<uint, OrderedItem> _orderedBuffer = new Dictionary<uint, OrderedItem>();
            private uint _nextDeliverSeq = 1;

            public uint NextSendSeq()
            {
                lock (this) { var s = _nextSendSeq++; return s; }
            }

            public bool MarkReceived(uint seq)
            {
                lock (_recentSeqs)
                {
                    if (!_recentSeqs.Add(seq)) return false;
                    _recentOrder.Enqueue(seq);
                    if (_recentOrder.Count > RECENT_WINDOW)
                    {
                        var old = _recentOrder.Dequeue();
                        _recentSeqs.Remove(old);
                    }
                    return true;
                }
            }

            public void RemovePending(uint seq, PeerState recordRtt = null)
            {
                if (Pending.TryRemove(seq, out var p) && recordRtt != null && p.Retries == 0)
                {
                    var rttMs = (DateTime.UtcNow.Ticks - p.SentAtTicks) / (float)TimeSpan.TicksPerMillisecond;
                    if (rttMs > 0 && rttMs < 5000)
                        recordRtt.RttMs = recordRtt.RttMs == 0 ? rttMs : (recordRtt.RttMs * 0.8f + rttMs * 0.2f);
                }
            }

            public void PushOrdered(uint seq, ushort action, byte[] payload)
            {
                lock (_orderedBuffer)
                {
                    if (_nextDeliverSeq == 1 && seq > 1) _nextDeliverSeq = seq;
                    _orderedBuffer[seq] = new OrderedItem { Action = action, Payload = payload };
                }
            }

            public bool TryPopNextOrdered(out OrderedItem item)
            {
                lock (_orderedBuffer)
                {
                    if (_orderedBuffer.TryGetValue(_nextDeliverSeq, out item))
                    {
                        _orderedBuffer.Remove(_nextDeliverSeq);
                        _nextDeliverSeq++;
                        return true;
                    }
                    item = default;
                    return false;
                }
            }
        }

        private class PendingPacket
        {
            public uint Sequence;
            public byte Channel;
            public byte[] Bytes;
            public long SentAtTicks;
            public long NextRetryTicks;
            public int Retries;
            public ReliabilityMode Mode;
        }

        public struct OrderedItem
        {
            public ushort Action;
            public byte[] Payload;
        }

        private class SubscriptionToken : IDisposable
        {
            private readonly DataTransceiver _owner;
            private readonly ushort _action;
            private readonly Action<DataMessage> _handler;
            private bool _disposed;
            public SubscriptionToken(DataTransceiver owner, ushort action, Action<DataMessage> handler)
            { _owner = owner; _action = action; _handler = handler; }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner?.Unsubscribe(_action, _handler);
            }
        }

        #endregion
    }

}
