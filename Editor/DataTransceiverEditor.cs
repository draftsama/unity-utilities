#if UNITY_EDITOR
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Modules.Utilities
{
    [CustomEditor(typeof(DataTransceiver))]
    public class DataTransceiverEditor : UnityEditor.Editor
    {
        private string _testMessage = "Hello RUDP!";
        private ushort _testAction = 1;
        private DataTransceiver.ReliabilityMode _testMode = DataTransceiver.ReliabilityMode.ReliableOrdered;
        private int _testRelayTargetId = 1;

        private bool _connectionExpanded = true;
        private bool _reliabilityExpanded = false;
        private bool _heartbeatExpanded = false;
        private bool _handshakeExpanded = false;
        private bool _reconnectExpanded = false;
        private bool _discoveryExpanded = false;
        private bool _performanceExpanded = false;
        private bool _statusExpanded = true;
        private bool _eventsExpanded = false;
        private bool _testHarnessExpanded = true;

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            var dt = (DataTransceiver)target;
            if (dt == null) return;

            serializedObject.Update();
            var isServer = serializedObject.FindProperty("m_IsServer");

            DrawModeToggle(isServer);
            EditorGUILayout.Space();

            DrawConnectionBox(isServer);
            DrawReliabilityBox();
            DrawHeartbeatBox();
            DrawHandshakeBox();
            if (!isServer.boolValue) DrawReconnectBox();
            DrawDiscoveryBox();
            DrawPerformanceBox();
            DrawStatusBox(dt);
            DrawEventsBox(isServer);

            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                DrawRuntimeControls(dt);
                DrawTestHarness(dt, isServer.boolValue);
            }

            if (GUI.changed) EditorUtility.SetDirty(target);
        }

        private void DrawModeToggle(SerializedProperty isServer)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.color = isServer.boolValue ? Color.green : Color.gray;
            if (GUILayout.Button("Server")) isServer.boolValue = true;
            GUI.color = isServer.boolValue ? Color.gray : Color.green;
            if (GUILayout.Button("Client")) isServer.boolValue = false;
            EditorGUILayout.EndHorizontal();
            GUI.color = Color.white;
        }

        private void DrawConnectionBox(SerializedProperty isServer)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _connectionExpanded = EditorGUILayout.Foldout(_connectionExpanded, "Connection", true);
            if (_connectionExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Host"),
                    new GUIContent(isServer.boolValue ? "Bind Host (empty=any)" : "Server Host"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Port"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_StartOnEnable"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_IsDebug"));
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawReliabilityBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _reliabilityExpanded = EditorGUILayout.Foldout(_reliabilityExpanded, "Reliability", true);
            if (_reliabilityExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_InitialRtoMs"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_MaxRtoMs"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_MaxRetries"));
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawHeartbeatBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _heartbeatExpanded = EditorGUILayout.Foldout(_heartbeatExpanded, "Heartbeat / Timeout", true);
            if (_heartbeatExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_HeartbeatMs"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_PeerTimeoutMs"));
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawHandshakeBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _handshakeExpanded = EditorGUILayout.Foldout(_handshakeExpanded, "Handshake", true);
            if (_handshakeExpanded)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_HandshakeTimeoutMs"));
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawReconnectBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _reconnectExpanded = EditorGUILayout.Foldout(_reconnectExpanded, "Auto Reconnect (client)", true);
            if (_reconnectExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_AutoReconnect"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ReconnectDelaysMs"), true);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawDiscoveryBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _discoveryExpanded = EditorGUILayout.Foldout(_discoveryExpanded, "Discovery (UDP broadcast)", true);
            if (_discoveryExpanded)
            {
                var enabled = serializedObject.FindProperty("m_EnableDiscovery");
                EditorGUILayout.PropertyField(enabled);
                if (enabled.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DiscoveryPort"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DiscoveryIntervalMs"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DiscoveryTag"));
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawPerformanceBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _performanceExpanded = EditorGUILayout.Foldout(_performanceExpanded, "Performance", true);
            if (_performanceExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_MaxPayloadKB"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_FragmentTtlMs"));
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawStatusBox(DataTransceiver dt)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _statusExpanded = EditorGUILayout.Foldout(_statusExpanded, "Status", true);
            if (_statusExpanded)
            {
                GUI.enabled = false;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_IsRunning"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_IsConnected"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_IsHandshaking"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_IsReconnecting"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_LocalPeerId"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_PeerCount"));
                GUI.enabled = true;

                if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField("Peers", EditorStyles.boldLabel);
                    foreach (var peer in dt.Peers)
                    {
                        var stats = dt.GetStats(peer.Id);
                        EditorGUILayout.BeginHorizontal("box");
                        EditorGUILayout.LabelField(
                            $"#{peer.Id}  {peer.IpAddress}:{peer.Port}  RTT:{stats.RttMs:F0}ms  Pending:{stats.PendingAcks}",
                            GUILayout.MinWidth(280));
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawEventsBox(SerializedProperty isServer)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _eventsExpanded = EditorGUILayout.Foldout(_eventsExpanded, "Events", true);
            if (_eventsExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnPeerConnected"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnPeerDisconnected"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnMessageReceived"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnReady"));
                if (!isServer.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnReconnecting"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnReconnected"));
                }
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnError"));
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawRuntimeControls(DataTransceiver dt)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (!dt.IsRunning)
            {
                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("Start")) dt.StartAsync().Forget();
            }
            else
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Stop")) dt.StopAsync().Forget();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            var statusColor = dt.IsConnected ? Color.green : (dt.IsRunning ? Color.yellow : Color.gray);
            GUI.color = statusColor;
            var label = dt.IsServer
                ? (dt.IsRunning ? "SERVER LISTENING" : "STOPPED")
                : (dt.IsConnected ? "CONNECTED" : (dt.IsRunning ? "CONNECTING..." : "STOPPED"));
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
        }

        private void DrawTestHarness(DataTransceiver dt, bool isServer)
        {
            EditorGUILayout.BeginVertical("box");
            _testHarnessExpanded = EditorGUILayout.Foldout(_testHarnessExpanded, "Test Harness", true);
            if (!_testHarnessExpanded) { EditorGUILayout.EndVertical(); return; }

            _testAction = (ushort)EditorGUILayout.IntField("Action", _testAction);
            _testMode = (DataTransceiver.ReliabilityMode)EditorGUILayout.EnumPopup("Reliability", _testMode);
            _testMessage = EditorGUILayout.TextField("Payload (UTF-8)", _testMessage);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button(isServer ? "Broadcast" : "Send"))
            {
                var bytes = Encoding.UTF8.GetBytes(_testMessage ?? string.Empty);
                dt.SendAsync(_testAction, bytes, _testMode).Forget();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (isServer)
            {
                EditorGUILayout.LabelField("Send to specific peer:", EditorStyles.miniBoldLabel);
                foreach (var peer in dt.Peers)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"#{peer.Id} {peer.IpAddress}:{peer.Port}", GUILayout.MinWidth(180));
                    GUI.backgroundColor = Color.yellow;
                    if (GUILayout.Button("Send To", GUILayout.MaxWidth(80)))
                    {
                        var bytes = Encoding.UTF8.GetBytes(_testMessage ?? string.Empty);
                        dt.SendToAsync(peer.Id, _testAction, bytes, _testMode).Forget();
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                _testRelayTargetId = EditorGUILayout.IntField("Relay Target Peer Id", _testRelayTargetId);
                GUI.backgroundColor = Color.magenta;
                if (GUILayout.Button("Relay", GUILayout.MaxWidth(80)))
                {
                    var bytes = Encoding.UTF8.GetBytes(_testMessage ?? string.Empty);
                    dt.RelayAsync(_testRelayTargetId, _testAction, bytes, _testMode).Forget();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }
    }
}
#endif
