using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace Modules.Utilities.Editor
{

    public class BuildManagerWindowEditor : EditorWindow, IPostprocessBuildWithReport, IPreprocessBuildWithReport
    {
        BuildProfile[] buildProfiles;
        int selectedBuildProfileIndex = 0;
        string buildFolderPath = string.Empty;

        bool enableCopyFolders = true;
        List<string> copyFolderPaths = new List<string>();

        public int callbackOrder { get; }
        string buildVersion = string.Empty;
        string buildName = string.Empty;
        string buildSuffix = string.Empty;

        bool isNotify = false;
        bool isSoundNotify = false;

        // ScriptableObject settings
        private BuildManagerSettings settings;
        private const string SETTINGS_PATH = "Assets/Settings/BuildManagerSettings.asset";
        private const string SETTINGS_FOLDER = "Assets/Settings";

        // Focus tracking
        private bool hadFocusLastFrame = false;


        [MenuItem("Utilities/Build Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<BuildManagerWindowEditor>(false, "Build Manager", false);
            window.Show(true);

        }

        void OnEnable()
        {
            // Debug.Log("Build Manager Window Enabled");
            // Subscribe to compilation events
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            // Enable mouse move events for focus detection
            wantsMouseMove = true;
            hadFocusLastFrame = false;

            LoadBuildProfiles();
            LoadOrCreateSettings(); // Load settings after profiles are loaded
            LoadProfileSpecificSettings();
        }

        void OnDisable()
        {
            // Unsubscribe from compilation events
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
        }

        void OnBecameVisible()
        {
            // Debug.Log("Build Manager Window became visible (focused)");
            // Refresh data when window becomes visible/focused
            LoadBuildProfiles();
            LoadOrCreateSettings();
            LoadProfileSpecificSettings();
            Repaint();
        }

        void OnBecameInvisible()
        {
           // Debug.Log("Build Manager Window became invisible (lost focus)");
        }

        void OnWindowFocused()
        {
           // Debug.Log("Build Manager Window gained focus");
            Repaint();
        }

        void OnWindowLostFocus()
        {
           // Debug.Log("Build Manager Window lost focus");
        }

        void OnCompilationStarted(object obj)
        {
            // Force repaint during compilation
            Repaint();
        }

        void OnCompilationFinished(object obj)
        {
            // Just repaint - OnGUI will handle reloading if needed
            Repaint();
        }

        void LoadOrCreateSettings()
        {
            settings = AssetDatabase.LoadAssetAtPath<BuildManagerSettings>(SETTINGS_PATH);
            if (settings == null)
            {
                // Settings don't exist, will be created when user clicks the button
                Debug.Log("Build Manager Settings not found. Use 'Create Settings' button to create one.");
            }
        }

        void CreateSettings()
        {
            // Ensure the Settings folder exists
            if (!AssetDatabase.IsValidFolder(SETTINGS_FOLDER))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }

            // Create new settings
            settings = ScriptableObject.CreateInstance<BuildManagerSettings>();
            AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created Build Manager Settings at: {SETTINGS_PATH}");
        }

        void CreateSettingsForProfile(string profileName)
        {
            CreateSettings();

            // Initialize with current profile
            if (settings != null && buildProfiles != null && buildProfiles.Length > 0)
            {
                settings.selectedBuildProfileIndex = selectedBuildProfileIndex;
                var profileSettings = settings.GetOrCreateProfileSettings(profileName);
                profileSettings.buildName = profileName;
                SaveSettings();

                // Load settings for this profile
                LoadProfileSpecificSettings();
            }

            Debug.Log($"Created Build Manager Settings for profile: {profileName}");
        }

        void LoadProfileSpecificSettings()
        {
            if (settings == null || buildProfiles == null || buildProfiles.Length == 0) return;

            var selectedProfile = buildProfiles[selectedBuildProfileIndex];
            var profileSettings = settings.GetOrCreateProfileSettings(selectedProfile.name);

            // Load profile-specific settings
            buildFolderPath = profileSettings.buildFolderPath;
            copyFolderPaths = new List<string>(profileSettings.copyFolderPaths);
            enableCopyFolders = profileSettings.enableCopyFolders;
            buildName = !string.IsNullOrEmpty(profileSettings.buildName) ? profileSettings.buildName : selectedProfile.name;
            buildSuffix = profileSettings.buildSuffix;
            buildVersion = !string.IsNullOrEmpty(profileSettings.buildVersion) ? profileSettings.buildVersion : PlayerSettings.bundleVersion;

            // Load global settings
            isNotify = settings.isNotify;
            isSoundNotify = settings.isSoundNotify;
        }

        void ShowProfileSettingsUI(BuildProfile selectedBuildProfile)
        {
            GUILayout.BeginVertical("box");

            // Current platform 
            try
            {
                GUILayout.Label("Platform: " + EditorUserBuildSettings.activeBuildTarget, EditorStyles.label);
            }
            catch
            {
                GUILayout.Label("Platform: Loading...", EditorStyles.label);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Build Name: ", EditorStyles.label);
            buildName = GUILayout.TextField(buildName, GUILayout.Width(200));
            GUILayout.EndHorizontal();
            if (GUI.changed)
            {
                // Update settings
                var profileSettings = settings.GetOrCreateProfileSettings(selectedBuildProfile.name);
                profileSettings.buildName = buildName;
                SaveSettings();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Build Suffix: ", EditorStyles.label);
            buildSuffix = GUILayout.TextField(buildSuffix, GUILayout.Width(200));
            GUILayout.EndHorizontal();
            if (GUI.changed)
            {
                // Update settings
                var profileSettings = settings.GetOrCreateProfileSettings(selectedBuildProfile.name);
                profileSettings.buildSuffix = buildSuffix;
                SaveSettings();
            }

            // Current version
            GUILayout.BeginHorizontal();
            GUILayout.Label("Version: ", EditorStyles.label);

            buildVersion = GUILayout.TextField(buildVersion, GUILayout.Width(100));
            if (GUI.changed)
            {
                // Update settings
                var profileSettings = settings.GetOrCreateProfileSettings(selectedBuildProfile.name);
                profileSettings.buildVersion = buildVersion;
                SaveSettings();
            }

            // Disable update button during compilation
            GUI.enabled = !EditorApplication.isCompiling && !EditorApplication.isUpdating;

            if (PlayerSettings.bundleVersion != buildVersion && GUILayout.Button("Update", GUILayout.Width(60)))
            {
                try
                {
                    PlayerSettings.bundleVersion = buildVersion;
                    var profileSettings = settings.GetOrCreateProfileSettings(selectedBuildProfile.name);
                    profileSettings.buildVersion = buildVersion;
                    Debug.Log($"Updated build version to: {buildVersion}");
                    AssetDatabase.SaveAssets();
                    SaveSettings();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to update version: {e.Message}");
                }
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            //select build folder using dialog
            GUILayout.BeginHorizontal();
            GUILayout.Label("Build Folder: ", EditorStyles.label);
            buildFolderPath = GUILayout.TextField(buildFolderPath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Build Folder", buildFolderPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    buildFolderPath = path;
                    // Save to settings
                    var profileSettings = settings.GetOrCreateProfileSettings(selectedBuildProfile.name);
                    profileSettings.buildFolderPath = buildFolderPath;
                    SaveSettings();
                }
                // Focus back to this window
                Focus();
            }
            GUILayout.EndHorizontal();

            // Show warning if build folder is empty
            if (string.IsNullOrEmpty(buildFolderPath))
            {
                EditorGUILayout.HelpBox("Build folder path is required. Please select a folder using the Browse button above.", MessageType.Error);
            }

            //copy folder support for windows only
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows ||
               EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64 ||
               EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneOSX)
            {
                // Display copy folder paths
                GUILayout.BeginVertical("box");
                GUILayout.BeginHorizontal();
                GUILayout.Label("Copy Folders:", EditorStyles.label);
                GUILayout.FlexibleSpace();
                enableCopyFolders = GUILayout.Toggle(enableCopyFolders, "Enable Copy Folders", GUILayout.Width(150));
                // Save toggle state to settings
                if (GUI.changed)
                {
                    var profileSettings = settings.GetOrCreateProfileSettings(selectedBuildProfile.name);
                    profileSettings.enableCopyFolders = enableCopyFolders;
                    SaveSettings();
                }
                GUILayout.EndHorizontal();

                if (copyFolderPaths != null && copyFolderPaths.Count > 0)
                {
                    int indexToRemove = -1;
                    for (int i = 0; i < copyFolderPaths.Count; i++)
                    {
                        EditorGUI.indentLevel++;

                        GUILayout.BeginHorizontal();
                        GUILayout.Label(copyFolderPaths[i], EditorStyles.label);
                        if (GUILayout.Button("DEL", GUILayout.Width(60)))
                        {
                            indexToRemove = i;
                        }
                        GUILayout.EndHorizontal();
                        EditorGUI.indentLevel--;
                    }

                    if (indexToRemove >= 0)
                    {
                        copyFolderPaths.RemoveAt(indexToRemove);
                        var profileSettings = settings.GetOrCreateProfileSettings(selectedBuildProfile.name);
                        profileSettings.copyFolderPaths = copyFolderPaths;
                        SaveSettings();
                    }
                }
                else
                {
                    GUILayout.Label("No copy folders specified.", EditorStyles.label);
                }

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                // Button to add a new copy folder path
                if (GUILayout.Button("Add Copy Folder"))
                {
                    string path = EditorUtility.OpenFolderPanel("Select Copy Folder", System.Environment.CurrentDirectory, "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        copyFolderPaths.Add(path);
                        var profileSettings = settings.GetOrCreateProfileSettings(selectedBuildProfile.name);
                        profileSettings.copyFolderPaths = copyFolderPaths;
                        SaveSettings();
                    }
                    // Focus back to this window
                    Focus();
                }
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }
            else
            {
                copyFolderPaths.Clear();
                enableCopyFolders = false;
            }

            //draw checkbox for isNotify
            isNotify = GUILayout.Toggle(isNotify, "Enable Message Notify", GUILayout.Width(150));
            if (GUI.changed)
            {
                settings.isNotify = isNotify;
                SaveSettings();
            }

            //draw checkbox for isSoundNotify
            isSoundNotify = GUILayout.Toggle(isSoundNotify, "Enable Sound Notify", GUILayout.Width(150));
            if (GUI.changed)
            {
                settings.isSoundNotify = isSoundNotify;
                SaveSettings();
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // Disable Build button if build folder is empty
            GUI.enabled = !string.IsNullOrEmpty(buildFolderPath);
            if (GUILayout.Button("Build", GUILayout.Width(100)))
            {
                Build(selectedBuildProfile);
            }
            GUI.enabled = true; // Reset GUI.enabled

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            // Auto-save any changes
            if (GUI.changed)
            {
                UpdateCurrentProfileSettings();
            }
        }

        void SaveSettings()
        {
            if (settings != null)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        void UpdateCurrentProfileSettings()
        {
            if (settings != null && buildProfiles != null && buildProfiles.Length > 0)
            {
                var selectedBuildProfile = buildProfiles[selectedBuildProfileIndex];
                var profileSettings = settings.GetOrCreateProfileSettings(selectedBuildProfile.name);

                profileSettings.buildFolderPath = buildFolderPath;
                profileSettings.copyFolderPaths = new List<string>(copyFolderPaths);
                profileSettings.enableCopyFolders = enableCopyFolders;
                profileSettings.buildName = buildName;
                profileSettings.buildSuffix = buildSuffix;
                profileSettings.buildVersion = buildVersion;

                settings.selectedBuildProfileIndex = selectedBuildProfileIndex;
                settings.isNotify = isNotify;
                settings.isSoundNotify = isSoundNotify;

                SaveSettings();
            }
        }

        void LoadBuildProfiles()
        {
            // Only load if not compiling
            if (EditorApplication.isCompiling)
                return;

            try
            {
                string[] profilePaths = AssetDatabase.FindAssets("t:BuildProfile", new[] { "Assets/Settings/Build Profiles" });
                buildProfiles = new BuildProfile[profilePaths.Length];
                for (int i = 0; i < profilePaths.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(profilePaths[i]);
                    buildProfiles[i] = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
                }

                if (settings != null && buildProfiles.Length > 0)
                {
                    // Load from ScriptableObject
                    selectedBuildProfileIndex = settings.selectedBuildProfileIndex;
                    if (selectedBuildProfileIndex < 0 || selectedBuildProfileIndex >= buildProfiles.Length)
                    {
                        selectedBuildProfileIndex = 0;
                        settings.selectedBuildProfileIndex = selectedBuildProfileIndex;
                        SaveSettings();
                    }

                    // Don't load profile settings here - let the caller decide
                }
                else if (buildProfiles.Length > 0)
                {
                    // Fallback to default values when no settings exist
                    selectedBuildProfileIndex = 0;
                    buildFolderPath = string.Empty;
                    copyFolderPaths = new List<string>();
                    enableCopyFolders = true;
                    buildVersion = PlayerSettings.bundleVersion;
                    buildName = buildProfiles[0].name;
                    buildSuffix = string.Empty;
                    isNotify = false;
                    isSoundNotify = false;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to load build profiles: {e.Message}");
                buildProfiles = new BuildProfile[0];
            }
        }

        private void OnGUI()
        {
            // Check for focus changes
            bool hasFocusNow = EditorWindow.focusedWindow == this;
            if (hasFocusNow && !hadFocusLastFrame)
            {
                // Window just gained focus
                OnWindowFocused();
            }
            else if (!hasFocusNow && hadFocusLastFrame)
            {
                // Window just lost focus
                OnWindowLostFocus();
            }
            hadFocusLastFrame = hasFocusNow;

            // Check if compiling and show appropriate UI
            if (EditorApplication.isCompiling)
            {
                GUILayout.Label("Build Manager", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                // Show compilation status
                GUILayout.BeginVertical("box");
                GUILayout.Label("Compiling Scripts...", EditorStyles.centeredGreyMiniLabel);

                // Show a simple progress indicator
                var rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(rect, -1, "Please wait...");

                GUILayout.EndVertical();

                // Repaint to show animation
                Repaint();
                return;
            }

            // Check if we need to reload profiles after compilation
            if (buildProfiles == null || buildProfiles.Length == 0)
            {
                LoadBuildProfiles();
                // Load profile settings after loading profiles
                if (buildProfiles != null && buildProfiles.Length > 0 && settings != null)
                {
                    LoadProfileSpecificSettings();
                }
            }


            // Dropdown to select build profile - show this first
            if (buildProfiles != null && buildProfiles.Length > 0)
            {
                BuildProfile activeProfile = null;
                try
                {
                    activeProfile = BuildProfile.GetActiveBuildProfile();
                }
                catch (System.Exception)
                {
                    // Handle case where BuildProfile API is not available
                    GUILayout.Label("Build Profile API temporarily unavailable", EditorStyles.helpBox);
                    return;
                }

                int activeProfileIndex = -1;
                string[] profileNames = new string[buildProfiles.Length];

                for (int i = 0; i < buildProfiles.Length; i++)
                {
                    var profile = buildProfiles[i];
                    if (profile == null) continue;

                    if (profile == activeProfile)
                    {
                        activeProfileIndex = i;
                        profileNames[i] = $"● {profile.name}"; // Active profile with bullet
                    }
                    else
                    {
                        profileNames[i] = $"○ {profile.name}"; // Inactive profile with hollow bullet
                    }
                }

                // Begin horizontal layout for profile selection
                GUILayout.BeginHorizontal();

                // Clamp selectedBuildProfileIndex to valid range
                selectedBuildProfileIndex = Mathf.Clamp(selectedBuildProfileIndex, 0, buildProfiles.Length - 1);
                var previousIndex = selectedBuildProfileIndex;

                // Label with normal color
                GUILayout.Label("Build Profile:", EditorStyles.label, GUILayout.Width(80));

                // Change GUI color only for dropdown
                var originalColor = GUI.color;
                if (activeProfileIndex == selectedBuildProfileIndex)
                {
                    GUI.color = Color.green; // Green color for active profile
                }

                selectedBuildProfileIndex = EditorGUILayout.Popup(selectedBuildProfileIndex, profileNames);

                // Restore original color
                GUI.color = originalColor;

                var selectedBuildProfile = buildProfiles[selectedBuildProfileIndex];
                var isActive = activeProfileIndex == selectedBuildProfileIndex;

                // Check if profile selection changed
                if (previousIndex != selectedBuildProfileIndex)
                {
                    // Profile changed, reload settings for new profile
                    LoadProfileSpecificSettings();
                }

                // Disable button during operations that might cause compilation
                GUI.enabled = !EditorApplication.isCompiling && !EditorApplication.isUpdating;

                if (!isActive && GUILayout.Button("Switch", GUILayout.Width(100)))
                {
                    try
                    {
                        BuildProfile.SetActiveBuildProfile(selectedBuildProfile);
                        Debug.Log($"Set {selectedBuildProfile.name} as active build profile.");
                        if (settings != null)
                        {
                            settings.selectedBuildProfileIndex = selectedBuildProfileIndex;
                            SaveSettings();
                        }
                        GUI.FocusControl(null);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Failed to switch build profile: {e.Message}");
                    }
                }
                
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                // Show settings status and create button if needed - AFTER profile selection
                GUILayout.BeginHorizontal("box");
                if (settings == null)
                {
                    GUILayout.Label($"⚠️ No Build Manager Settings found for profile: {selectedBuildProfile.name}", EditorStyles.helpBox);
                    if (GUILayout.Button("Create Settings", GUILayout.Width(120)))
                    {
                        CreateSettingsForProfile(selectedBuildProfile.name);
                    }
                }
                else
                {

                    // Show BuildManagerSettings object reference (disabled)
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField("BuildManagerSettings:", settings, typeof(BuildManagerSettings), false);
                    EditorGUI.EndDisabledGroup();

                    if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                    {
                        LoadOrCreateSettings();
                        LoadProfileSpecificSettings();
                    }
                }
                GUILayout.EndHorizontal();

                // Don't show the rest of the UI if no settings exist
                if (settings == null)
                {
                    EditorGUILayout.HelpBox($"Please create Build Manager Settings for profile '{selectedBuildProfile.name}' to continue.", MessageType.Info);
                    return;
                }

                // Show the active profile settings UI
                if (isActive)
                {
                    ShowProfileSettingsUI(selectedBuildProfile);
                }
            }
            else
            {
                GUILayout.Label("No Build Profiles found.", EditorStyles.label);

                if (GUILayout.Button("Refresh"))
                {
                    LoadBuildProfiles();
                }
            }
        }
        public void Build(BuildProfile selectedBuildProfile)
        {

            try
            {
                if (string.IsNullOrEmpty(buildFolderPath))
                {
                    Debug.LogError("Build folder path is not set.");
                    return;
                }

                //date: 251031
                var date = System.DateTime.Now;
                var dateString = $"{date:yyMMdd}";

                //version = 0.0.3 to v0-0-3
                var versionParts = PlayerSettings.bundleVersion.Split('.');
                var versionString = string.Join("-", versionParts);

                var buildTarget = EditorUserBuildSettings.activeBuildTarget;
                var extension = "";
                var folderName = string.IsNullOrEmpty(buildSuffix)
                    ? $"{buildName}-v{versionString}-{dateString}"
                    : $"{buildName}-v{versionString}-{dateString}-{buildSuffix}";
                var appName = buildName;

                switch (buildTarget)
                {
                    case BuildTarget.StandaloneWindows:
                    case BuildTarget.StandaloneWindows64:
                        extension = ".exe";
                        break;
                    case BuildTarget.StandaloneOSX:
                        extension = ".app";
                        break;
                    case BuildTarget.Android:
                        extension = ".apk";
                        appName = folderName;
                        break;

                }




                var buildPath = System.IO.Path.Combine(buildFolderPath, folderName, $"{appName}{extension}");

                BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, buildPath, buildTarget, BuildOptions.None);







            }
            catch (System.Exception e)
            {
                Debug.LogError($"Build failed: {e.Message}");
            }
        }



        private void DrawLine()
        {
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        }




        public void OnPreprocessBuild(BuildReport report)
        {
            Application.logMessageReceived += OnBuildError;

        }

        private void OnBuildError(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error)
            {

                // FAILED TO BUILD, STOP LISTENING FOR ERRORS
                if (true)
#if UNITY_EDITOR_WIN
                    RunCommand("rundll32 user32.dll,MessageBeep");
#elif UNITY_EDITOR_OSX
                    RunCommand("afplay /System/Library/Sounds/Glass.aiff");
#endif
                Application.logMessageReceived -= OnBuildError;

                if (condition.Contains("\n"))
                {
                    condition = condition.Replace("\n", " ");
                }
                if (stackTrace.Contains("\n"))
                {
                    stackTrace = stackTrace.Replace("\n", " ");
                }
                var profile = buildProfiles[selectedBuildProfileIndex];
                //write error message
                if (isNotify)
                {
                    SendMessage(
                        $"[Build Fail!]\nProfile Name: {profile.name}  \nVersion: {PlayerSettings.bundleVersion} \nPlatform: {EditorUserBuildSettings.activeBuildTarget} \nError: {condition} \nStackTrace: {stackTrace}");
                }


            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {

        Application.logMessageReceived -= OnBuildError;

        if (report.summary.result == BuildResult.Cancelled || report.summary.result == BuildResult.Failed)
        {
            Debug.LogError("Build failed: " + report.summary.result);
            return;
        }

        // Sync selectedBuildProfileIndex with active profile
        var activeProfile = GetActiveProfileAndSyncIndex();
        var profileSettings = settings?.GetOrCreateProfileSettings(activeProfile.name);
        var enableCopy = profileSettings?.enableCopyFolders ?? false;
        var copyFolders = profileSettings?.copyFolderPaths ?? new List<string>();
        var buildFileFolder = Path.GetDirectoryName(report.summary.outputPath);

        if (IsStandaloneBuild(report.summary.platform) && enableCopy && copyFolders.Count > 0)
        {
            foreach (var folderPath in copyFolders)
            {
                if (Directory.Exists(folderPath))
                {
                    var folderName = Path.GetFileName(folderPath);
                    var targetPath = Path.Combine(buildFileFolder, folderName);
                    CopyDirectory(folderPath, targetPath, true);
                    Debug.Log($"Copied folder: {folderPath} to {targetPath}");
                }
                else
                {
                    Debug.LogWarning($"Folder does not exist: {folderPath}");
                }
            }
        }

        if (isNotify)
        {
            SendMessage($"[Build Success!]\nProfile Name: {activeProfile.name}  \nVersion: {PlayerSettings.bundleVersion} \nPlatform: {EditorUserBuildSettings.activeBuildTarget}");
        }

        if (isSoundNotify)
        {
#if UNITY_EDITOR_WIN
            RunCommand("rundll32 user32.dll,MessageBeep");
#elif UNITY_EDITOR_OSX
            RunCommand("afplay /System/Library/Sounds/Glass.aiff");
#endif
        }

        EditorUtility.RevealInFinder(buildFileFolder);
        Debug.Log("Build completed successfully!");
    }

    private BuildProfile GetActiveProfileAndSyncIndex()
    {
        BuildProfile activeProfile = null;
        try { activeProfile = BuildProfile.GetActiveBuildProfile(); } catch { }
        if (activeProfile != null && buildProfiles != null)
        {
            for (int i = 0; i < buildProfiles.Length; i++)
            {
                if (buildProfiles[i] == activeProfile)
                {
                    selectedBuildProfileIndex = i;
                    break;
                }
            }
        }
        return buildProfiles[selectedBuildProfileIndex];
    }

    private bool IsStandaloneBuild(BuildTarget target)
    {
        return target == BuildTarget.StandaloneWindows ||
               target == BuildTarget.StandaloneWindows64 ||
               target == BuildTarget.StandaloneOSX;

        }


        private const string _API_MESSAGE = "https://api.telegram.org/bot1671713978:AAGGuzmbA2IQlZlQz66Z9yNWtckivBZZuuw/sendMessage?chat_id=1575164820&text=";
        private void SendMessage(string _message)
        {
            //4096 //characters limit for telegram message
            if (_message.Length > 4096)
            {
                Debug.LogWarning("Message exceeds Telegram character limit, truncating.");
                _message = _message.Substring(0, 4096);
            }
            // Replace spaces with %20 for URL encoding
            _message = Uri.EscapeDataString(_message);
            // Send the message to Telegram
            Get($"{_API_MESSAGE}{_message}");
        }

        private string Get(string _uri)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_uri);
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }

            }
            catch (WebException ex)
            {
                Debug.LogError($"Error fetching data from {_uri}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unexpected error: {ex.Message}");
                return null;
            }
        }

        string RunCommand(string _cmd)
        {
            System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo();
#if UNITY_EDITOR_WIN
            start.FileName = "cmd.exe";
#elif UNITY_EDITOR_OSX
            start.FileName = "/bin/zsh";
#endif
            start.Arguments = "-c \" " + _cmd + " \"";
            start.UseShellExecute = false; // Do not use OS shell
            start.CreateNoWindow = true; // We don't need new window
            start.RedirectStandardOutput = true; // Any output, generated by application will be redirected back
            start.RedirectStandardError =
                true; // Any error in standard output will be redirected back (for example exceptions)
            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(start))
            {
                if (process != null)
                {
                    using (StreamReader reader = process.StandardOutput)
                    {
                        string stderr =
                            process.StandardError.ReadToEnd(); // Here are the exceptions from our Python script
                        string result = reader.ReadToEnd(); // Here is the result of StdOut(for example: print "test")
                        return result;
                    }
                }
                else
                {
                    return null;
                }
            }
        }
        void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }
    }



}