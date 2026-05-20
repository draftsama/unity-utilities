#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Modules.Utilities
{
    [CustomEditor(typeof(DataTransformSync))]
    public class DataTransformSyncEditor : UnityEditor.Editor
    {
        private bool _networkExpanded    = true;
        private bool _syncExpanded       = true;
        private bool _sendRateExpanded   = true;
        private bool _smoothingExpanded  = true;

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
                DrawSmoothingBox();
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
                EditorGUILayout.PropertyField(serializedObject.FindProperty("actionId"),
                    new GUIContent("Action ID", "Must match on both sender and receiver"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("syncKey"),
                    new GUIContent("Sync Key", "Must be identical on both sides — used to pair sender ↔ receiver and prevent echo"));
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        // ── Sync Settings (sender only) ──────────────────────────
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

                // show packet size hint
                var hasPos   = serializedObject.FindProperty("syncPosition").boolValue;
                var hasRot   = serializedObject.FindProperty("syncRotation").boolValue;
                var hasScale = serializedObject.FindProperty("syncScale").boolValue;
                var size     = 5 + (hasPos ? 12 : 0) + (hasRot ? 12 : 0) + (hasScale ? 12 : 0);
                EditorGUILayout.HelpBox($"Packet payload: {size} bytes per send", MessageType.None);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        // ── Send Rate (sender only) ──────────────────────────────
        private void DrawSendRateBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _sendRateExpanded = EditorGUILayout.Foldout(_sendRateExpanded, "Send Rate", true);
            if (_sendRateExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sendRateHz"),
                    new GUIContent("Send Rate (Hz)", "Packets sent per second"));

                var hz = serializedObject.FindProperty("sendRateHz").intValue;
                EditorGUILayout.HelpBox($"Interval: {1000f / hz:F0} ms  (~{hz} packets/sec)", MessageType.None);

                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sendOnChange"),
                    new GUIContent("Send On Change Only", "Skip sending if position/rotation/scale hasn't changed — saves bandwidth for stationary objects"));
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        // ── Smoothing (receiver only) ─────────────────────────────
        private void DrawSmoothingBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            _smoothingExpanded = EditorGUILayout.Foldout(_smoothingExpanded, "Smoothing", true);
            if (_smoothingExpanded)
            {
                var enableProp = serializedObject.FindProperty("enableSmoothing");
                EditorGUILayout.PropertyField(enableProp,
                    new GUIContent("Enable Smoothing", "Lerp/Slerp toward received values each frame instead of snapping"));

                if (enableProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("positionLerpSpeed"),
                        new GUIContent("Position Speed"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationLerpSpeed"),
                        new GUIContent("Rotation Speed"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("scaleLerpSpeed"),
                        new GUIContent("Scale Speed"));
                    EditorGUI.indentLevel--;

                    EditorGUILayout.HelpBox(
                        "Higher value = snappier.  Lower value = smoother but more lag.\n" +
                        "Recommended: 10–20 for 20 Hz send rate.",
                        MessageType.None);
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        // ── Runtime status ────────────────────────────────────────
        private void DrawRuntimeStatus()
        {
            var sync = (DataTransformSync)target;
            var rb   = sync.GetComponent<Rigidbody>();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);

            var connected = sync.transceiver != null && sync.transceiver.IsConnected;
            GUI.color = connected ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.5f, 0.5f);
            EditorGUILayout.LabelField("Transceiver", connected ? "Connected ✓" : "Disconnected");
            GUI.color = Color.white;

            if (rb != null)
            {
                GUI.color = new Color(0.4f, 0.8f, 1f);
                EditorGUILayout.LabelField("Physics", $"Rigidbody detected — using MovePosition/MoveRotation");
                GUI.color = Color.white;
            }
            else
            {
                EditorGUILayout.LabelField("Physics", "Transform (no Rigidbody)");
            }

            EditorGUILayout.EndVertical();
        }
    }
}
#endif
