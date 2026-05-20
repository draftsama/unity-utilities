using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Modules.Utilities
{
    /// <summary>
    /// Spawns and syncs a GameObject across the network.
    /// Sender: broadcasts Spawn on connect, streams transform, sends Despawn on destroy.
    /// Receiver — SpawnPrefab mode: instantiates a prefab on Spawn, destroys on Despawn or peer disconnect.
    /// Receiver — TargetObject mode: syncs transform to an existing scene object, no instantiation or destruction.
    /// </summary>
    public class GameObjectSync : MonoBehaviour
    {
        public enum ReceiverMode
        {
            /// <summary>Instantiate a prefab when the sender spawns; destroy it on despawn/disconnect.</summary>
            SpawnPrefab,
            /// <summary>Sync transform to an existing scene GameObject; never instantiate or destroy.</summary>
            TargetObject
        }
        // ── Inspector ─────────────────────────────────────────────

        [Header("Network")]
        public DataTransceiver transceiver;

        [Tooltip("Action ID for Spawn/Despawn lifecycle packets. Must match on both sides.")]
        public ushort spawnActionId = 100;

        [Tooltip("Action ID for ongoing transform stream. Must match on both sides.")]
        public ushort transformActionId = 1;

        [Tooltip("Key used to pair sender ↔ receiver. Must be identical on both sides.")]
        public string syncKey = "default";

        [Header("Mode")]
        public bool isSender = true;

        [Header("Receiver Settings")]
        public ReceiverMode receiverMode = ReceiverMode.SpawnPrefab;

        [Tooltip("(SpawnPrefab) Prefab to instantiate when a Spawn packet arrives.")]
        public GameObject prefab;

        [Tooltip("(SpawnPrefab) Parent transform for the spawned instance. Leave null for scene root.")]
        public Transform spawnParent;

        [Tooltip("(TargetObject) Existing scene GameObject to sync transform to. Not instantiated or destroyed.")]
        public GameObject targetObject;

        [Header("Sync Settings (sender)")]
        public bool useLocalSpace  = true;
        public bool syncPosition   = true;
        public bool syncRotation   = true;
        public bool syncScale      = true;
        public bool sendOnChange   = false;
        [Range(1, 60)] public int sendRateHz = 20;

        [Header("Smoothing (receiver)")]
        public bool enableSmoothing = true;
        [Range(1f, 50f)] public float positionLerpSpeed = 15f;
        [Range(1f, 50f)] public float rotationLerpSpeed = 15f;
        [Range(1f, 50f)] public float scaleLerpSpeed    = 15f;

        [Header("Events (receiver)")]
        public UnityEvent<GameObject> onInstanceSpawned   = new UnityEvent<GameObject>();
        public UnityEvent             onInstanceDespawned = new UnityEvent();

        // ── Private — shared ──────────────────────────────────────
        private int         _syncKeyHash;
        private IDisposable _spawnSub;
        private IDisposable _transformSub;

        // ── Private — sender ──────────────────────────────────────
        private float _sendInterval;
        private float _timer;
        private bool  _spawnSent;

        // send-change detection
        private Vector3 _lastPos;
        private Vector3 _lastEuler;
        private Vector3 _lastScale;

        // ── Private — receiver ────────────────────────────────────
        private GameObject _instance;
        private Rigidbody  _instanceRb;
        private int        _senderPeerId = -1;
        private bool       _hasTarget;

        // receive targets (applied to _instance)
        private Vector3    _targetPos;
        private Quaternion _targetRot;
        private Vector3    _targetScale;
        private bool       _targetIsLocal;

        // ── Lifecycle ─────────────────────────────────────────────

        void Start()
        {
            _syncKeyHash  = syncKey.GetHashCode();
            _sendInterval = 1f / sendRateHz;

            if (isSender)
            {
                transceiver.OnReady.AddListener(OnSenderReady);
                transceiver.OnReconnected.AddListener(OnSenderReady);

                // Already connected when Start() runs (e.g. StartOnEnable completed before this component started)
                if (transceiver.IsConnected) SendSpawn();
            }
            else
            {
                _spawnSub     = transceiver.Subscribe(spawnActionId,     OnSpawnPacketReceived);
                _transformSub = transceiver.Subscribe(transformActionId, OnTransformReceived);
                transceiver.OnPeerDisconnected.AddListener(OnPeerDisconnected);
            }
        }

        void OnDestroy()
        {
            if (isSender)
            {
                transceiver.OnReady.RemoveListener(OnSenderReady);
                transceiver.OnReconnected.RemoveListener(OnSenderReady);
                if (transceiver != null && transceiver.IsConnected)
                    SendDespawn();
            }
            else
            {
                _spawnSub?.Dispose();
                _transformSub?.Dispose();
                if (transceiver != null)
                    transceiver.OnPeerDisconnected.RemoveListener(OnPeerDisconnected);
                DestroyInstance();
            }
        }

        void Update()
        {
            // ── Sender: stream transform ──────────────────────────
            if (!isSender || !transceiver.IsConnected || !_spawnSent) return;

            _timer += Time.deltaTime;
            if (_timer < _sendInterval) return;
            _timer = 0f;

            if (!sendOnChange || HasChanged())
                transceiver.SendAsync(transformActionId, PackTransform(),
                    DataTransceiver.ReliabilityMode.UnreliableSequenced).Forget();

            // ── Receiver: smooth interpolation (no Rigidbody) ────
        }

        // Separate method to avoid putting receiver logic inside the sender guard
        void LateUpdate()
        {
            if (isSender || !enableSmoothing || !_hasTarget || _instance == null || _instanceRb != null) return;

            var t = Time.deltaTime;
            if (syncPosition)
            {
                if (_targetIsLocal) _instance.transform.localPosition = Vector3.Lerp(_instance.transform.localPosition, _targetPos, positionLerpSpeed * t);
                else                _instance.transform.position      = Vector3.Lerp(_instance.transform.position,      _targetPos, positionLerpSpeed * t);
            }
            if (syncRotation)
            {
                if (_targetIsLocal) _instance.transform.localRotation = Quaternion.Slerp(_instance.transform.localRotation, _targetRot, rotationLerpSpeed * t);
                else                _instance.transform.rotation      = Quaternion.Slerp(_instance.transform.rotation,      _targetRot, rotationLerpSpeed * t);
            }
            if (syncScale)
                _instance.transform.localScale = Vector3.Lerp(_instance.transform.localScale, _targetScale, scaleLerpSpeed * t);
        }

        void FixedUpdate()
        {
            if (isSender || _instanceRb == null || !_hasTarget) return;

            var worldPos = (_targetIsLocal && _instance.transform.parent != null)
                ? _instance.transform.parent.TransformPoint(_targetPos) : _targetPos;
            var worldRot = (_targetIsLocal && _instance.transform.parent != null)
                ? _instance.transform.parent.rotation * _targetRot : _targetRot;

            var ft = Time.fixedDeltaTime;
            if (syncPosition)
                _instanceRb.MovePosition(enableSmoothing
                    ? Vector3.Lerp(_instanceRb.position, worldPos, positionLerpSpeed * ft)
                    : worldPos);
            if (syncRotation)
                _instanceRb.MoveRotation(enableSmoothing
                    ? Quaternion.Slerp(_instanceRb.rotation, worldRot, rotationLerpSpeed * ft)
                    : worldRot);
            if (syncScale)
                _instance.transform.localScale = enableSmoothing
                    ? Vector3.Lerp(_instance.transform.localScale, _targetScale, scaleLerpSpeed * ft)
                    : _targetScale;
        }

        // ── Sender helpers ────────────────────────────────────────

        private void OnSenderReady()
        {
            _spawnSent = false;
            SendSpawn();
        }

        private void SendSpawn()
        {
            if (!transceiver.IsConnected) return;
            transceiver.SendAsync(spawnActionId, PackSpawnPacket(0x01),
                DataTransceiver.ReliabilityMode.ReliableOrdered).Forget();
            _spawnSent = true;
        }

        private void SendDespawn()
        {
            transceiver.SendAsync(spawnActionId, PackSpawnPacket(0x02),
                DataTransceiver.ReliabilityMode.ReliableOrdered).Forget();
            _spawnSent = false;
        }

        private bool HasChanged()
        {
            var pos   = useLocalSpace ? transform.localPosition    : transform.position;
            var euler = useLocalSpace ? transform.localEulerAngles : transform.eulerAngles;
            var scale = transform.localScale;
            if (syncPosition && pos   != _lastPos)   { _lastPos   = pos;   return true; }
            if (syncRotation && euler != _lastEuler) { _lastEuler = euler; return true; }
            if (syncScale    && scale != _lastScale) { _lastScale = scale; return true; }
            return false;
        }

        // ── Packet encoding ───────────────────────────────────────

        // Spawn/Despawn: key(4) + type(1) + pos(12) + euler(12) + scale(12) = 41 bytes
        private byte[] PackSpawnPacket(byte type)
        {
            var pos   = useLocalSpace ? transform.localPosition    : transform.position;
            var euler = useLocalSpace ? transform.localEulerAngles : transform.eulerAngles;
            var scale = transform.localScale;

            var buf = new byte[41];
            int o = 0;
            BitConverter.GetBytes(_syncKeyHash).CopyTo(buf, o); o += 4;
            buf[o++] = type;
            WriteFloat(buf, ref o, pos.x);   WriteFloat(buf, ref o, pos.y);   WriteFloat(buf, ref o, pos.z);
            WriteFloat(buf, ref o, euler.x); WriteFloat(buf, ref o, euler.y); WriteFloat(buf, ref o, euler.z);
            WriteFloat(buf, ref o, scale.x); WriteFloat(buf, ref o, scale.y); WriteFloat(buf, ref o, scale.z);
            return buf;
        }

        // Transform stream: key(4) + flags(1) + pos?(12) + euler?(12) + scale?(12)
        // flags: bit0=localSpace  bit1=hasPosition  bit2=hasRotation  bit3=hasScale
        private byte[] PackTransform()
        {
            var pos   = useLocalSpace ? transform.localPosition    : transform.position;
            var euler = useLocalSpace ? transform.localEulerAngles : transform.eulerAngles;
            var scale = transform.localScale;

            byte flags = 0;
            if (useLocalSpace) flags |= 0x01;
            if (syncPosition)  flags |= 0x02;
            if (syncRotation)  flags |= 0x04;
            if (syncScale)     flags |= 0x08;

            var size = 5 + (syncPosition ? 12 : 0) + (syncRotation ? 12 : 0) + (syncScale ? 12 : 0);
            var buf = new byte[size];
            int o = 0;
            BitConverter.GetBytes(_syncKeyHash).CopyTo(buf, o); o += 4;
            buf[o++] = flags;
            if (syncPosition) { WriteFloat(buf, ref o, pos.x);   WriteFloat(buf, ref o, pos.y);   WriteFloat(buf, ref o, pos.z); }
            if (syncRotation) { WriteFloat(buf, ref o, euler.x); WriteFloat(buf, ref o, euler.y); WriteFloat(buf, ref o, euler.z); }
            if (syncScale)    { WriteFloat(buf, ref o, scale.x); WriteFloat(buf, ref o, scale.y); WriteFloat(buf, ref o, scale.z); }
            return buf;
        }

        // ── Receiver handlers ─────────────────────────────────────

        private void OnSpawnPacketReceived(DataTransceiver.DataMessage msg)
        {
            if (msg.Data == null || msg.Data.Length < 41) return;

            int o = 0;
            var incomingKey = BitConverter.ToInt32(msg.Data, o); o += 4;
            if (incomingKey != _syncKeyHash) return;

            var type = msg.Data[o++];
            var pos   = new Vector3(ReadFloat(msg.Data, ref o), ReadFloat(msg.Data, ref o), ReadFloat(msg.Data, ref o));
            var euler = new Vector3(ReadFloat(msg.Data, ref o), ReadFloat(msg.Data, ref o), ReadFloat(msg.Data, ref o));
            var scale = new Vector3(ReadFloat(msg.Data, ref o), ReadFloat(msg.Data, ref o), ReadFloat(msg.Data, ref o));

            if (type == 0x01)       // Spawn
            {
                SpawnInstance(msg.SenderPeerId, pos, Quaternion.Euler(euler), scale);
            }
            else if (type == 0x02)  // Despawn
            {
                DestroyInstance();
            }
        }

        private void OnTransformReceived(DataTransceiver.DataMessage msg)
        {
            if (_instance == null || msg.Data == null || msg.Data.Length < 5) return;

            int o = 0;
            var incomingKey = BitConverter.ToInt32(msg.Data, o); o += 4;
            if (incomingKey != _syncKeyHash) return;

            var flags        = msg.Data[o++];
            var isLocalSpace = (flags & 0x01) != 0;
            var hasPosition  = (flags & 0x02) != 0;
            var hasRotation  = (flags & 0x04) != 0;
            var hasScale     = (flags & 0x08) != 0;

            _targetIsLocal = isLocalSpace;
            if (hasPosition)
                _targetPos = new Vector3(ReadFloat(msg.Data, ref o), ReadFloat(msg.Data, ref o), ReadFloat(msg.Data, ref o));
            if (hasRotation)
            {
                var euler = new Vector3(ReadFloat(msg.Data, ref o), ReadFloat(msg.Data, ref o), ReadFloat(msg.Data, ref o));
                _targetRot = Quaternion.Euler(euler);
            }
            if (hasScale)
                _targetScale = new Vector3(ReadFloat(msg.Data, ref o), ReadFloat(msg.Data, ref o), ReadFloat(msg.Data, ref o));

            _hasTarget = true;

            // Snap when smoothing is off and no Rigidbody
            if (!enableSmoothing && _instanceRb == null)
            {
                if (hasPosition) { if (isLocalSpace) _instance.transform.localPosition = _targetPos; else _instance.transform.position = _targetPos; }
                if (hasRotation) { if (isLocalSpace) _instance.transform.localRotation = _targetRot; else _instance.transform.rotation = _targetRot; }
                if (hasScale)    _instance.transform.localScale = _targetScale;
            }
        }

        private void OnPeerDisconnected(DataTransceiver.PeerInfo peer)
        {
            if (peer.Id == _senderPeerId)
                DestroyInstance();
        }

        // ── Instance management ───────────────────────────────────

        private void SpawnInstance(int senderPeerId, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            if (_instance != null) DestroyInstance();

            _senderPeerId = senderPeerId;

            if (receiverMode == ReceiverMode.SpawnPrefab)
            {
                if (prefab == null)
                {
                    Debug.LogWarning($"[GameObjectSync] prefab is not assigned on receiver (syncKey={syncKey})");
                    return;
                }
                _instance = Instantiate(prefab, pos, rot, spawnParent);
                _instance.transform.localScale = scale;
            }
            else // TargetObject
            {
                if (targetObject == null)
                {
                    Debug.LogWarning($"[GameObjectSync] targetObject is not assigned on receiver (syncKey={syncKey})");
                    return;
                }
                _instance = targetObject;
                _instance.transform.SetPositionAndRotation(pos, rot);
                _instance.transform.localScale = scale;
            }

            _instanceRb = _instance.GetComponent<Rigidbody>();

            // Init lerp targets so there's no snap on first frame
            _targetPos     = pos;
            _targetRot     = rot;
            _targetScale   = scale;
            _targetIsLocal = false; // spawn packet always uses world space for initial placement
            _hasTarget     = false; // wait for first transform stream packet

            onInstanceSpawned?.Invoke(_instance);
        }

        private void DestroyInstance()
        {
            if (_instance != null)
            {
                if (receiverMode == ReceiverMode.SpawnPrefab)
                    Destroy(_instance);   // TargetObject: leave the scene object alive, only clear the reference

                _instance     = null;
                _instanceRb   = null;
                _senderPeerId = -1;
                _hasTarget    = false;
                onInstanceDespawned?.Invoke();
            }
        }

        // ── Byte helpers ──────────────────────────────────────────

        private static void WriteFloat(byte[] buf, ref int offset, float value)
        {
            BitConverter.GetBytes(value).CopyTo(buf, offset);
            offset += 4;
        }

        private static float ReadFloat(byte[] data, ref int offset)
        {
            var v = BitConverter.ToSingle(data, offset);
            offset += 4;
            return v;
        }
    }
}
