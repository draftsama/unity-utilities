#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Modules.Utilities
{
    [CustomEditor(typeof(GameObjectSync))]
    public class GameObjectSyncEditor : UnityEditor.Editor
    {
        private bool _networkExpanded   = true;
        private bool _syncExpanded      = true;
        private bool _sendRateExpanded  = true;
        private bool _receiverExpanded  = true;
        private bool _smoothingExpanded = true;
        private bool _eventsExpanded    = false;

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var isSenderProp = serializedObject.FindProperty("isSender");

            DrawModeToggle(isSenderProp);
            EditorGUILayout.Space(4);

            DrawNetworkBox();
            EditorGUILayout.Space(2);

            if (isSenderProp.boolValue)
            {
                DrawSyncSettingsBox();
                EditorGUILayout.Space(2);
                DrawSendRateBox();
            }
            else
            {
                DrawReceiverSettingsBox();
                EditorGUILayout.Space(2);
                DrawSmoothingBox();
                EditorGUILayout.Space(2);
                DrawEventsBox();
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(4);
                DrawRuntimeStatus();
            }

            serializedObject.ApplyModifiedProperties();
            if (GUI.changed) EditorUtility.SetDirty(target);
        }

        // ── Mode toggle ──────────────────────────────────────────
        private void DrawModeToggle(SerializedProperty isSenderProp)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.color = isSenderProp.boolValue ? new Color(0.4f, 0.9f, 0.4f) : Color.gray;
            if (GUILayout.Button("Sender", GUILayout.Height(28)))
                isSenderProp.boolValue = true;
            GUI.color = isSenderProp.boolValue ? Color.gray : new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("Receiver", GUILayout.Height(28)))
                isSenderProp.boolValue = false;
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        // ── Network (shared) ─────────────────────────────────────
        private void DrawNetworkBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _networkExpanded = EditorGUILayout.Foldout(_networkExpanded, "Network", true);
            if (_networkExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("transceiver"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnActionId"),
                    new GUIContent("Spawn Action ID", "For Spawn/Despawn lifecycle — must match on both sides"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("transformActionId"),
                    new GUIContent("Transform Action ID", "For ongoing transform stream — must match on both sides"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("syncKey"),
                    new GUIContent("Sync Key", "Must be identical on both sides to pair sender ↔ receiver"));
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        // ── Sync Settings (sender) ───────────────────────────────
        private void DrawSyncSettingsBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _syncExpanded = EditorGUILayout.Foldout(_syncExpanded, "Sync Settings", true);
            if (_syncExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("useLocalSpace"),
                    new GUIContent("Use Local Space", "Send localPosition/localRotation instead of world space"));

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("What to sync", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("syncPosition"), new GUIContent("Position"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("syncRotation"), new GUIContent("Rotation"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("syncScale"),    new GUIContent("Scale"));
                EditorGUI.indentLevel--;

                var hasPos   = serializedObject.FindProperty("syncPosition").boolValue;
                var hasRot   = serializedObject.FindProperty("syncRotation").boolValue;
                var hasSc    = serializedObject.FindProperty("syncScale").boolValue;
                var size     = 5 + (hasPos ? 12 : 0) + (hasRot ? 12 : 0) + (hasSc ? 12 : 0);
                EditorGUILayout.HelpBox($"Transform packet: {size} bytes  |  Spawn/Despawn: 41 bytes (always full)", MessageType.None);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        // ── Send Rate (sender) ───────────────────────────────────
        private void DrawSendRateBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _sendRateExpanded = EditorGUILayout.Foldout(_sendRateExpanded, "Send Rate", true);
            if (_sendRateExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sendRateHz"),
                    new GUIContent("Send Rate (Hz)"));
                var hz = serializedObject.FindProperty("sendRateHz").intValue;
                EditorGUILayout.HelpBox($"Interval: {1000f / hz:F0} ms  (~{hz} packets/sec)", MessageType.None);
                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sendOnChange"),
                    new GUIContent("Send On Change Only", "Skip sending if transform hasn't changed"));
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        // ── Receiver Settings (receiver) ─────────────────────────
        private void DrawReceiverSettingsBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _receiverExpanded = EditorGUILayout.Foldout(_receiverExpanded, "Receiver Settings", true);
            if (_receiverExpanded)
            {
                var modeProp = serializedObject.FindProperty("receiverMode");
                EditorGUILayout.PropertyField(modeProp, new GUIContent("Receiver Mode"));

                EditorGUILayout.Space(2);

                var isSpawnPrefab = modeProp.enumValueIndex == 0; // SpawnPrefab = 0
                if (isSpawnPrefab)
                {
                    var prefabProp = serializedObject.FindProperty("prefab");
                    EditorGUILayout.PropertyField(prefabProp,
                        new GUIContent("Prefab", "GameObject to instantiate when a Spawn packet arrives"));
                    if (prefabProp.objectReferenceValue == null)
                        EditorGUILayout.HelpBox("Prefab is required for SpawnPrefab mode.", MessageType.Warning);

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnParent"),
                        new GUIContent("Spawn Parent", "Parent transform for the spawned instance. Leave empty for scene root."));
                }
                else
                {
                    var targetProp = serializedObject.FindProperty("targetObject");
                    EditorGUILayout.PropertyField(targetProp,
                        new GUIContent("Target Object", "Existing scene object whose transform will be synced. Never instantiated or destroyed."));
                    if (targetProp.objectReferenceValue == null)
                        EditorGUILayout.HelpBox("Target Object is required for TargetObject mode.", MessageType.Warning);
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        // ── Smoothing (receiver) ──────────────────────────────────
        private void DrawSmoothingBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _smoothingExpanded = EditorGUILayout.Foldout(_smoothingExpanded, "Smoothing", true);
            if (_smoothingExpanded)
            {
                var enableProp = serializedObject.FindProperty("enableSmoothing");
                EditorGUILayout.PropertyField(enableProp,
                    new GUIContent("Enable Smoothing", "Lerp/Slerp toward received values instead of snapping"));
                if (enableProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("positionLerpSpeed"), new GUIContent("Position Speed"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationLerpSpeed"), new GUIContent("Rotation Speed"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("scaleLerpSpeed"),    new GUIContent("Scale Speed"));
                    EditorGUI.indentLevel--;
                    EditorGUILayout.HelpBox("Recommended: 10–20 for 20 Hz send rate.", MessageType.None);
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        // ── Events (receiver) ────────────────────────────────────
        private void DrawEventsBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _eventsExpanded = EditorGUILayout.Foldout(_eventsExpanded, "Events", true);
            if (_eventsExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onInstanceSpawned"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onInstanceDespawned"));
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        // ── Runtime status ────────────────────────────────────────
        private void DrawRuntimeStatus()
        {
            var sync = (GameObjectSync)target;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);

            var connected = sync.transceiver != null && sync.transceiver.IsConnected;
            GUI.color = connected ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.5f, 0.5f);
            EditorGUILayout.LabelField("Transceiver", connected ? "Connected ✓" : "Disconnected");
            GUI.color = Color.white;

            if (!sync.isSender)
            {
                // Use reflection to read private fields for display
                var instanceField    = typeof(GameObjectSync).GetField("_instance",     System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var senderPeerField  = typeof(GameObjectSync).GetField("_senderPeerId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var instance         = instanceField?.GetValue(sync)    as GameObject;
                var senderPeerId     = senderPeerField != null ? (int)senderPeerField.GetValue(sync) : -1;

                EditorGUILayout.LabelField("Mode", sync.receiverMode.ToString(), EditorStyles.miniLabel);

                GUI.color = instance != null ? new Color(0.4f, 0.8f, 1f) : Color.gray;
                EditorGUILayout.LabelField("Instance", instance != null ? $"{instance.name} (peer {senderPeerId})" : "None");
                GUI.color = Color.white;

                if (instance != null)
                {
                    var rb = instance.GetComponent<Rigidbody>();
                    EditorGUILayout.LabelField("Physics", rb != null ? "Rigidbody — MovePosition/MoveRotation" : "Transform");
                }
            }
            else
            {
                var spawnSentField = typeof(GameObjectSync).GetField("_spawnSent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var spawnSent      = spawnSentField != null && (bool)spawnSentField.GetValue(sync);
                GUI.color = spawnSent ? new Color(0.4f, 1f, 0.4f) : Color.gray;
                EditorGUILayout.LabelField("Spawn", spawnSent ? "Sent ✓ — streaming transform" : "Not sent yet");
                GUI.color = Color.white;
            }

            EditorGUILayout.EndVertical();
        }
    }
}
#endif
