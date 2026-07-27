using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Modules.Utilities.Editor
{
    [CustomEditor(typeof(FileTransfer))]
    public class FileTransferEditor : UnityEditor.Editor
    {
        private string _harnessLocalPath = "";
        private string _harnessRemoteName = "";
        private string _harnessFolder = "";
        private string _harnessStatus = "";

        private bool _showConnection = true;
        private bool _showFolders = true;
        private bool _showLimits;
        private bool _showPolicy;
        private bool _showEvents;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var component = (FileTransfer)target;

            DrawModeToggle();

            EditorGUILayout.Space();
            DrawGroup(ref _showConnection, "Connection", "m_Host", "m_Port", "m_BindAddress", "m_ConnectTimeoutMs");
            DrawGroup(ref _showFolders, "Folders", "m_RootFolder", "m_DownloadFolder");
            DrawGroup(ref _showLimits, "Limits", "m_MaxFileSizeMB", "m_MaxConcurrentTransfers", "m_BufferSizeKB", "m_IdleTimeoutMs");
            DrawGroup(ref _showPolicy, "Policy", "m_AllowUpload", "m_AllowDownload", "m_VerifyHash", "m_SharedSecret", "m_ControlChannel");
            DrawGroup(ref _showEvents, "Events",
                "m_OnTransferStarted", "m_OnTransferProgress", "m_OnTransferCompleted",
                "m_OnTransferFailed", "m_OnFileOffered", "m_OnServerReady");

            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_StartOnEnable"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_IsDebug"));

            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                DrawStatus(component);
                DrawHarness(component);
                Repaint();
            }
        }

        private void DrawModeToggle()
        {
            var isServer = serializedObject.FindProperty("m_IsServer");

            EditorGUILayout.BeginHorizontal();
            var previous = GUI.backgroundColor;

            GUI.backgroundColor = isServer.boolValue ? Color.cyan : previous;
            if (GUILayout.Button("Server", GUILayout.Height(26))) isServer.boolValue = true;

            GUI.backgroundColor = !isServer.boolValue ? Color.cyan : previous;
            if (GUILayout.Button("Client", GUILayout.Height(26))) isServer.boolValue = false;

            GUI.backgroundColor = previous;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGroup(ref bool expanded, string title, params string[] properties)
        {
            expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, title);
            if (expanded)
            {
                EditorGUI.indentLevel++;
                for (var i = 0; i < properties.Length; i++)
                {
                    var property = serializedObject.FindProperty(properties[i]);
                    if (property != null) EditorGUILayout.PropertyField(property);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawStatus(FileTransfer component)
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Running", component.IsRunning ? "yes" : "no");
            EditorGUILayout.LabelField("Role", component.IsServer ? "server" : "client");

            if (component.IsServer)
            {
                EditorGUILayout.LabelField("Bound port", component.ServerPort.ToString());
                EditorGUILayout.LabelField("Active transfers", component.ActiveTransfers.ToString());
                EditorGUILayout.LabelField("Root", component.RootFolderPath);
            }
            else
            {
                EditorGUILayout.LabelField("Downloads", component.DownloadFolderPath);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawHarness(FileTransfer component)
        {
            if (component.IsServer)
            {
                EditorGUILayout.HelpBox("The test harness is client-only. Switch to Client mode to send or fetch a file.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Test Harness", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            _harnessLocalPath = EditorGUILayout.TextField("Local file", _harnessLocalPath);
            if (GUILayout.Button("...", GUILayout.Width(26)))
            {
                var picked = EditorUtility.OpenFilePanel("File to upload", "", "");
                if (!string.IsNullOrEmpty(picked)) _harnessLocalPath = picked;
            }
            EditorGUILayout.EndHorizontal();

            _harnessRemoteName = EditorGUILayout.TextField("Remote name", _harnessRemoteName);
            _harnessFolder = EditorGUILayout.TextField("Remote folder", _harnessFolder);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Upload"))
            {
                _harnessStatus = "uploading...";
                RunHarnessAsync(component.UploadAsync(_harnessLocalPath, _harnessFolder)).Forget();
            }

            if (GUILayout.Button("Download"))
            {
                _harnessStatus = "downloading...";
                RunHarnessAsync(component.DownloadAsync(_harnessRemoteName, _harnessFolder)).Forget();
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_harnessStatus)) EditorGUILayout.LabelField(_harnessStatus);

            EditorGUILayout.EndVertical();
        }

        private async UniTaskVoid RunHarnessAsync(UniTask<FileTransferResult> task)
        {
            var result = await task;
            _harnessStatus = result.ToString();
            Repaint();
        }
    }
}
