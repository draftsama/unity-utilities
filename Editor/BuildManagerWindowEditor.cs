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

        bool isNotify = false;



        const string PROFILE_SELECT_INDEX_KEY = "BM_PROFILE_SELECT_INDEX";
        const string BUILD_FOLDER_PATH_KEY = "BM_BUILD_FOLDER_PATH";
        const string COPY_FOLDER_PATHS_KEY = "BM_COPY_FOLDER_PATHS";
        const string ENABLE_COPY_FOLDERS_KEY = "BM_ENABLE_COPY_FOLDERS";
        const string BUILD_NAME_KEY = "BM_BUILD_NAME";
        const string IS_NOTIFY_KEY = "BM_IS_NOTIFY";

        void OnEnable()
        {
            // Subscribe to compilation events
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            LoadBuildProfiles();
        }

        void OnDisable()
        {
            // Unsubscribe from compilation events
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
        }

        void OnCompilationStarted(object obj)
        {
            // Force repaint during compilation
            Repaint();
        }

        void OnCompilationFinished(object obj)
        {
            // Reload profiles after compilation
            LoadBuildProfiles();
            Repaint();
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
                selectedBuildProfileIndex = PlayerPrefs.GetInt(PROFILE_SELECT_INDEX_KEY, -1);
                if (selectedBuildProfileIndex < 0 || selectedBuildProfileIndex >= buildProfiles.Length)
                {
                    selectedBuildProfileIndex = 0; // Default to first profile if index is invalid
                    PlayerPrefs.SetInt(PROFILE_SELECT_INDEX_KEY, selectedBuildProfileIndex);

                }
                var profile = buildProfiles[selectedBuildProfileIndex];
                buildFolderPath = PlayerPrefs.GetString($"{BUILD_FOLDER_PATH_KEY}_{profile.name}", string.Empty);
                // Debug.Log($"Loaded build folder path: {buildFolderPath} using key: {BUILD_FOLDER_PATH_KEY}_{profile.name}");
                string copyFolderPathsString = PlayerPrefs.GetString($"{COPY_FOLDER_PATHS_KEY}_{profile.name}", string.Empty);
                if (!string.IsNullOrEmpty(copyFolderPathsString))
                {
                    copyFolderPaths = new List<string>(copyFolderPathsString.Split(';'));
                }
                else
                {
                    copyFolderPaths.Clear();
                }
                enableCopyFolders = PlayerPrefs.GetInt($"{ENABLE_COPY_FOLDERS_KEY}_{profile.name}", 1) == 1;
                buildVersion = PlayerSettings.bundleVersion;
                buildName = PlayerPrefs.GetString($"{BUILD_NAME_KEY}_{profile.name}", profile.name);
                isNotify = PlayerPrefs.GetInt(IS_NOTIFY_KEY, 0) == 1;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to load build profiles: {e.Message}");
                buildProfiles = new BuildProfile[0];
            }
        }

        private void OnGUI()
        {
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
            }

            GUILayout.Label("Build Manager", EditorStyles.boldLabel);

            // Dropdown to select build profile
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

                    profileNames[i] = profile.name;

                    if (profile == activeProfile)
                    {
                        activeProfileIndex = i;
                        profileNames[i] += " [ACTIVE]";
                    }
                }

                // Begin horizontal layout
                GUI.color = Color.white;
                GUILayout.BeginHorizontal();

                // Clamp selectedBuildProfileIndex to valid range
                selectedBuildProfileIndex = Mathf.Clamp(selectedBuildProfileIndex, 0, buildProfiles.Length - 1);
                selectedBuildProfileIndex = EditorGUILayout.Popup("Build Profile:", selectedBuildProfileIndex, profileNames);

                var selectedBuildProfile = buildProfiles[selectedBuildProfileIndex];
                var isActive = activeProfileIndex == selectedBuildProfileIndex;

                // Disable button during operations that might cause compilation
                GUI.enabled = !EditorApplication.isCompiling && !EditorApplication.isUpdating;

                if (!isActive && GUILayout.Button("Switch", GUILayout.Width(100)))
                {
                    try
                    {
                        BuildProfile.SetActiveBuildProfile(selectedBuildProfile);
                        Debug.Log($"Set {selectedBuildProfile.name} as active build profile.");
                        PlayerPrefs.SetInt(PROFILE_SELECT_INDEX_KEY, selectedBuildProfileIndex);
                        GUI.FocusControl(null);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Failed to switch build profile: {e.Message}");
                    }
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUI.color = Color.white;

                if (isActive)
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
                        // Debug.Log($"Build Name changed to: {buildName}");
                        PlayerPrefs.SetString($"{BUILD_NAME_KEY}_{selectedBuildProfile.name}", buildName);
                    }

                    // Current version
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Version: ", EditorStyles.label);


                    buildVersion = GUILayout.TextField(buildVersion, GUILayout.Width(100));

                    // Disable update button during compilation
                    GUI.enabled = !EditorApplication.isCompiling && !EditorApplication.isUpdating;

                    if (PlayerSettings.bundleVersion != buildVersion && GUILayout.Button("Update", GUILayout.Width(60)))
                    {
                        try
                        {
                            PlayerSettings.bundleVersion = buildVersion;
                            Debug.Log($"Updated build version to: {buildVersion}");
                            AssetDatabase.SaveAssets();
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
                            // Save the path to PlayerPrefs for persistence
                            PlayerPrefs.SetString($"{BUILD_FOLDER_PATH_KEY}_{selectedBuildProfile.name}", buildFolderPath);
                        }
                    }
                    GUILayout.EndHorizontal();

                    //copy folder support for windows only
                    if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows ||
                       EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64)
                    {

                        // Display copy folder paths
                        GUILayout.BeginVertical("box");
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Copy Folders:", EditorStyles.label);
                        GUILayout.FlexibleSpace();
                        enableCopyFolders = GUILayout.Toggle(enableCopyFolders, "Enable Copy Folders", GUILayout.Width(150));
                        //save the toggle state to PlayerPrefs
                        PlayerPrefs.SetInt($"{ENABLE_COPY_FOLDERS_KEY}_{selectedBuildProfile.name}", enableCopyFolders ? 1 : 0);
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
                                PlayerPrefs.SetString($"{COPY_FOLDER_PATHS_KEY}_{selectedBuildProfile.name}", string.Join(";", copyFolderPaths));
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
                                PlayerPrefs.SetString($"{COPY_FOLDER_PATHS_KEY}_{selectedBuildProfile.name}", string.Join(";", copyFolderPaths));
                            }


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
                    isNotify = GUILayout.Toggle(isNotify, "Enable Notifications", GUILayout.Width(150));

                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Build", GUILayout.Width(100)))
                    {
                        Build(selectedBuildProfile);
                    }

                    GUILayout.EndHorizontal();




                    GUILayout.EndVertical();
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


            if (GUI.changed)
            {
                var selectedBuildProfile = buildProfiles[selectedBuildProfileIndex];
                PlayerPrefs.SetInt(PROFILE_SELECT_INDEX_KEY, selectedBuildProfileIndex);
                PlayerPrefs.SetString($"{BUILD_FOLDER_PATH_KEY}_{selectedBuildProfile.name}", buildFolderPath);
                PlayerPrefs.SetString($"{COPY_FOLDER_PATHS_KEY}_{selectedBuildProfile.name}", string.Join(";", copyFolderPaths));
                PlayerPrefs.SetInt($"{ENABLE_COPY_FOLDERS_KEY}_{selectedBuildProfile.name}", enableCopyFolders ? 1 : 0);
                PlayerPrefs.SetInt($"{IS_NOTIFY_KEY}_{selectedBuildProfile.name}", isNotify ? 1 : 0);
                
                PlayerPrefs.Save();

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

                //version = 0.0.3 to v0_0_3
                var versionParts = PlayerSettings.bundleVersion.Split('.');
                var versionString = string.Join("_", versionParts);

                var buildTarget = EditorUserBuildSettings.activeBuildTarget;
                var extension = "";
                var folderName = $"{buildName}_v{versionString}-{dateString}";
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
                if(isNotify)
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



            var buildFileFolder = Path.GetDirectoryName(report.summary.outputPath);

            if (report.summary.platform == BuildTarget.StandaloneWindows ||
                  report.summary.platform == BuildTarget.StandaloneWindows64)
            {
                if (enableCopyFolders && copyFolderPaths != null && copyFolderPaths.Count > 0)
                {
                    foreach (var folderPath in copyFolderPaths)
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
            }
            var profile = buildProfiles[selectedBuildProfileIndex];
            if(isNotify)
            {
                // Notify via Telegram
                SendMessage(
                    $"[Build Success!]\nProfile Name: {profile.name}  \nVersion: {PlayerSettings.bundleVersion} \nPlatform: {EditorUserBuildSettings.activeBuildTarget}");
            }

            // Optionally, you can reveal the build folder in the file explorer
            EditorUtility.RevealInFinder(buildFileFolder);
            Debug.Log("Build completed successfully!");

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