using System;
using Cysharp.Threading.Tasks;
using Modules.Utilities;
using UnityEngine;
namespace Modules.Utilities
{
    public class DataTransformSync : MonoBehaviour
    {
        [Header("Network")]
        public DataTransceiver transceiver;
        public ushort actionId = 1;

        [Header("Sync Settings")]
        [Tooltip("Key used to match sender ↔ receiver. Must be identical on both sides.")]
        public string syncKey = "default";
        public bool isSender = true;
        public bool useLocalSpace = true;
        public bool syncPosition = true;
        public bool syncRotation = true;
        public bool syncScale = true;

        [Header("Send Rate")]
        [Tooltip("Only send when transform has changed (reduces bandwidth)")]
        public bool sendOnChange = false;
        [Range(1, 60)]
        public int sendRateHz = 20;

        [Header("Smoothing (receiver)")]
        [Tooltip("Interpolate toward received values instead of snapping")]
        public bool enableSmoothing = true;
        [Range(1f, 50f)] public float positionLerpSpeed = 15f;
        [Range(1f, 50f)] public float rotationLerpSpeed = 15f;
        [Range(1f, 50f)] public float scaleLerpSpeed    = 15f;

        // ── private ──────────────────────────────────────────────
        private float _sendInterval;
        private float _timer;
        private int   _syncKeyHash;
        private IDisposable _sub;

        // send-change detection
        private Vector3 _lastPos;
        private Vector3 _lastEuler;
        private Vector3 _lastScale;

        // receive targets
        private Vector3    _targetPos;
        private Quaternion _targetRot;
        private Vector3    _targetScale;
        private bool       _targetIsLocal;
        private bool       _hasTarget;

        // rigidbody
        private Rigidbody _rb;

        // ── lifecycle ─────────────────────────────────────────────
        void Start()
        {
            _sendInterval  = 1f / sendRateHz;
            _syncKeyHash   = syncKey.GetHashCode();
            _rb            = GetComponent<Rigidbody>();
            _sub           = transceiver.Subscribe(actionId, OnTransformReceived);

            // init targets from current transform
            _targetPos     = useLocalSpace ? transform.localPosition    : transform.position;
            _targetRot     = useLocalSpace ? transform.localRotation    : transform.rotation;
            _targetScale   = transform.localScale;
            _targetIsLocal = useLocalSpace;
        }

        void OnDestroy() => _sub?.Dispose();

        void Update()
        {
            // ── sender: broadcast transform ───────────────────────
            if (isSender && transceiver.IsConnected)
            {
                _timer += Time.deltaTime;
                if (_timer >= _sendInterval)
                {
                    _timer = 0f;
                    if (!sendOnChange || HasChanged())
                        transceiver.SendAsync(actionId, PackTransform(),
                            DataTransceiver.ReliabilityMode.UnreliableSequenced).Forget();
                }
            }

            // ── receiver: smooth interpolation (no Rigidbody) ────
            if (!isSender && enableSmoothing && _hasTarget && _rb == null)
            {
                var t = Time.deltaTime;
                if (syncPosition)
                {
                    if (_targetIsLocal) transform.localPosition = Vector3.Lerp(transform.localPosition, _targetPos, positionLerpSpeed * t);
                    else                transform.position      = Vector3.Lerp(transform.position,      _targetPos, positionLerpSpeed * t);
                }
                if (syncRotation)
                {
                    if (_targetIsLocal) transform.localRotation = Quaternion.Slerp(transform.localRotation, _targetRot, rotationLerpSpeed * t);
                    else                transform.rotation      = Quaternion.Slerp(transform.rotation,      _targetRot, rotationLerpSpeed * t);
                }
                if (syncScale)
                    transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, scaleLerpSpeed * t);
            }
        }

        void FixedUpdate()
        {
            // ── receiver: Rigidbody path (MovePosition / MoveRotation) ──
            if (isSender || _rb == null || !_hasTarget) return;

            // convert local → world if needed
            var worldPos = (_targetIsLocal && transform.parent != null)
                ? transform.parent.TransformPoint(_targetPos) : _targetPos;
            var worldRot = (_targetIsLocal && transform.parent != null)
                ? transform.parent.rotation * _targetRot : _targetRot;

            var ft = Time.fixedDeltaTime;
            if (syncPosition)
                _rb.MovePosition(enableSmoothing
                    ? Vector3.Lerp(_rb.position, worldPos, positionLerpSpeed * ft)
                    : worldPos);
            if (syncRotation)
                _rb.MoveRotation(enableSmoothing
                    ? Quaternion.Slerp(_rb.rotation, worldRot, rotationLerpSpeed * ft)
                    : worldRot);
            // scale is not a Rigidbody concept — apply via transform directly
            if (syncScale)
                transform.localScale = enableSmoothing
                    ? Vector3.Lerp(transform.localScale, _targetScale, scaleLerpSpeed * ft)
                    : _targetScale;
        }

        // ── send helpers ─────────────────────────────────────────
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

        // pack: key(4) + flags(1) + pos?(12) + euler?(12) + scale?(12)
        // flags: bit0=localSpace  bit1=hasPosition  bit2=hasRotation  bit3=hasScale
        private byte[] PackTransform()
        {
            var pos   = useLocalSpace ? transform.localPosition    : transform.position;
            var rot   = useLocalSpace ? transform.localEulerAngles : transform.eulerAngles;
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
            if (syncRotation) { WriteFloat(buf, ref o, rot.x);   WriteFloat(buf, ref o, rot.y);   WriteFloat(buf, ref o, rot.z); }
            if (syncScale)    { WriteFloat(buf, ref o, scale.x); WriteFloat(buf, ref o, scale.y); WriteFloat(buf, ref o, scale.z); }
            return buf;
        }

        // ── receive ───────────────────────────────────────────────
        private void OnTransformReceived(DataTransceiver.DataMessage msg)
        {
            if (msg.Data == null || msg.Data.Length < 5) return;

            int o = 0;
            var incomingKey = BitConverter.ToInt32(msg.Data, o); o += 4;

            // กรอง: key ต้องตรงกัน และถ้าเป็น sender ไม่ต้อง apply (ป้องกัน echo)
            if (incomingKey != _syncKeyHash || isSender) return;

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

            // snap immediately when smoothing is off and no Rigidbody
            if (!enableSmoothing && _rb == null)
            {
                if (hasPosition) { if (isLocalSpace) transform.localPosition    = _targetPos; else transform.position      = _targetPos; }
                if (hasRotation) { if (isLocalSpace) transform.localRotation    = _targetRot; else transform.rotation      = _targetRot; }
                if (hasScale)    transform.localScale = _targetScale;
            }
        }

        // ── byte helpers ──────────────────────────────────────────
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