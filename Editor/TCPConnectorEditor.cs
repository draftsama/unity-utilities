#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace Modules.Utilities
{
    [CustomEditor(typeof(TCPConnector))]
    public class TCPConnectorEditor : UnityEditor.Editor
    {
        // Test message variables
        private string testMessage = "Hello World!";
        private ushort testAction = 1;

        // Foldout states
        private bool eventsExpanded = false;
        private bool settingsExpanded = true;
        private bool performanceExpanded = false;
        private bool healthExpanded = false;

        // Force inspector to repaint during play mode to show live status updates
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

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
            var enableDiscovery = serializedObject.FindProperty("m_EnableDiscovery");

            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            settingsExpanded = EditorGUILayout.Foldout(settingsExpanded, "Settings", true);

            if (settingsExpanded)
            {
                var isDebug = serializedObject.FindProperty("m_IsDebug");
                EditorGUILayout.PropertyField(isDebug);

                if (isDebug.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_LogSend"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_LogReceive"));
                }

                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_StartOnEnable"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Port"));

                EditorGUILayout.PropertyField(enableDiscovery);
                // Only show Host field if discovery is disabled
                if (!enableDiscovery.boolValue)
                {
                    if (!isServer.boolValue)
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Host"));
                }
                else
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DiscoveryPort"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DiscoveryMessage"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DiscoveryInterval"));
                }
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();

            // --- Performance Box ---
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;

            performanceExpanded = EditorGUILayout.Foldout(performanceExpanded, "Performance Settings", true);

            if (performanceExpanded)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_BufferSize"));

                var maxPacketProp = serializedObject.FindProperty("m_MaxPacketSizeMB");
                maxPacketProp.intValue = EditorGUILayout.IntField(
                    new GUIContent("Max Packet Size(MB)", "Maximum allowed size of a single received packet. Packets exceeding this limit will close the connection."),
                    maxPacketProp.intValue);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_MaxConcurrentConnections"));

                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_MaxRetryCount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_RetryDelay"));
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();

            // --- Connection Health Box ---
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;

            healthExpanded = EditorGUILayout.Foldout(healthExpanded, "Reconnection Settings", true);

            if (healthExpanded)
            {
                if (!isServer.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_EnableAutoReconnect"));
                    var enableReconnect = serializedObject.FindProperty("m_EnableAutoReconnect");
                    if (enableReconnect.boolValue)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ReconnectMaxAttempts"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ReconnectDelays"));
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Auto-reconnection is only available in Client mode", MessageType.Info);
                }
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();

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

                    // Show connected server info and synced client list for client mode
                    if (tcpConnector.IsConnected)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Connected Server:", EditorStyles.miniLabel);
                        var serverInfo = tcpConnector.ServerInfo;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"• Server: {serverInfo.ipAddress}:{serverInfo.port}", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                        GUI.backgroundColor = Color.cyan;
                        if (GUILayout.Button("Send", GUILayout.Width(50)))
                        {
                            // Send message directly to server
                            tcpConnector.SendDataAsync(testAction, testMessage).Forget();
                            Debug.Log($"[TCPConnector] Sent message to server: Action={testAction}, Message='{testMessage}'");
                        }
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();

                        // Show client's own ID
                        if (tcpConnector.MyClientId != -1)
                        {
                            EditorGUILayout.LabelField($"My Client ID: {tcpConnector.MyClientId}", EditorStyles.miniLabel);
                        }

                        // Show synced client list (other clients connected to same server, excluding self)
                        var syncedClients = tcpConnector.GetAllConnectedClientsInfo();
                        var otherClients = syncedClients.Where(c => c.id != tcpConnector.MyClientId).ToList();
                        if (otherClients.Count > 0)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField($"Other Connected Clients ({otherClients.Count}):", EditorStyles.miniLabel);
                            foreach (var client in otherClients)
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField($"• ID: {client.id}: {client.ipAddress}:{client.port}", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                                GUI.backgroundColor = Color.yellow;
                                if (GUILayout.Button("Send", GUILayout.Width(50)))
                                {
                                    // Send relay message to specific client via server
                                    tcpConnector.SendRelayMessageToClientAsync(client.id, testAction, testMessage).Forget();
                                    Debug.Log($"[TCPConnector] Sent relay message to client {client.id}: Action={testAction}, Message='{testMessage}'");
                                }
                                GUI.backgroundColor = Color.white;
                                EditorGUILayout.EndHorizontal();
                            }

                            // Broadcast to All other clients button
                            EditorGUILayout.Space();
                            GUI.backgroundColor = Color.green;
                            if (GUILayout.Button("📢 Broadcast to All Other Clients"))
                            {
                                // Broadcast relay message to all other clients via server
                                tcpConnector.BroadcastRelayMessageAsync(testAction, testMessage).Forget();
                                Debug.Log($"[TCPConnector] Broadcast relay message to {otherClients.Count} clients: Action={testAction}, Message='{testMessage}'");
                            }
                            GUI.backgroundColor = Color.white;
                        }
                    }
                }

                if (isServer.boolValue && tcpConnector.ConnectedClientCount > 0)
                {
                    EditorGUILayout.LabelField($"TCP Clients: {tcpConnector.ConnectedClientCount}");

                    // Show client details with individual Send buttons
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Clients:", EditorStyles.miniLabel);
                    var clientList = tcpConnector.GetAllConnectedClientsInfo();
                    foreach (var client in clientList)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"• ID: {client.id}: {client.ipAddress}:{client.port}", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                        GUI.backgroundColor = Color.yellow;
                        if (GUILayout.Button("Send", GUILayout.Width(50)))
                        {
                            tcpConnector.SendDataToClientsAsync(testAction, testMessage, new[] { client.id }).Forget();
                            Debug.Log($"[TCPConnector] Sent message to client {client.id}: Action={testAction}, Message='{testMessage}'");
                        }
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();
                    }

                    // Broadcast to All button
                    EditorGUILayout.Space();
                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button("📢 Broadcast to All Clients"))
                    {
                        int[] allClientIds = clientList.Select(c => c.id).ToArray();
                        tcpConnector.SendDataToClientsAsync(testAction, testMessage, allClientIds).Forget();
                        Debug.Log($"[TCPConnector] Broadcast message to {allClientIds.Length} clients: Action={testAction}, Message='{testMessage}'");
                    }
                    GUI.backgroundColor = Color.white;
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
            EditorGUI.indentLevel++;

            eventsExpanded = EditorGUILayout.Foldout(eventsExpanded, "Events", true);

            if (eventsExpanded)
            {
                EditorGUILayout.LabelField("Connection Events", EditorStyles.boldLabel);
                if (isServer.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnClientConnected"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnClientDisconnected"));
                }
                else
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnServerConnected"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnServerDisconnected"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnReconnecting"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnReconnected"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnReconnectFailed"));
                }
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Data Events", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnDataReceived"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnClientListUpdated"));
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("System Events", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OnError"));
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
