using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Modules.Utilities.Editor
{
    /// <summary>
    /// Stores machine-specific path settings in PlayerPrefs instead of the shared
    /// BuildManagerSettings asset. Absolute paths differ per machine, so keeping them
    /// in the committed asset breaks when the same project is opened on another machine.
    /// Keyed by profile name.
    /// </summary>
    public static class BuildManagerPathPrefs
    {
        private const string PREFIX = "BuildManager_Path_";

        [Serializable]
        private class StringListWrapper { public List<string> items = new List<string>(); }

        private static string Key(string profileName, string field) => $"{PREFIX}{profileName}_{field}";

        private static string SerializeList(List<string> list)
        {
            var wrapper = new StringListWrapper { items = list ?? new List<string>() };
            return JsonUtility.ToJson(wrapper);
        }

        private static List<string> DeserializeList(string json)
        {
            if (string.IsNullOrEmpty(json)) return new List<string>();
            var wrapper = JsonUtility.FromJson<StringListWrapper>(json);
            return wrapper?.items ?? new List<string>();
        }

        public static string GetBuildFolder(string profileName) =>
            PlayerPrefs.GetString(Key(profileName, "buildFolderPath"), string.Empty);
        public static void SetBuildFolder(string profileName, string value) =>
            PlayerPrefs.SetString(Key(profileName, "buildFolderPath"), value ?? string.Empty);

        public static bool GetEnableCopyFolders(string profileName) =>
            PlayerPrefs.GetInt(Key(profileName, "enableCopyFolders"), 1) == 1;
        public static void SetEnableCopyFolders(string profileName, bool value) =>
            PlayerPrefs.SetInt(Key(profileName, "enableCopyFolders"), value ? 1 : 0);

        public static List<string> GetCopyFolders(string profileName) =>
            DeserializeList(PlayerPrefs.GetString(Key(profileName, "copyFolderPaths"), string.Empty));
        public static void SetCopyFolders(string profileName, List<string> value) =>
            PlayerPrefs.SetString(Key(profileName, "copyFolderPaths"), SerializeList(value));

        public static bool GetEnableCopyFiles(string profileName) =>
            PlayerPrefs.GetInt(Key(profileName, "enableCopyFiles"), 1) == 1;
        public static void SetEnableCopyFiles(string profileName, bool value) =>
            PlayerPrefs.SetInt(Key(profileName, "enableCopyFiles"), value ? 1 : 0);

        public static List<string> GetCopyFiles(string profileName) =>
            DeserializeList(PlayerPrefs.GetString(Key(profileName, "copyFilePaths"), string.Empty));
        public static void SetCopyFiles(string profileName, List<string> value) =>
            PlayerPrefs.SetString(Key(profileName, "copyFilePaths"), SerializeList(value));
    }

    /// <summary>
    /// Data container for Build Manager settings.
    /// Separates data from UI logic to create a single source of truth.
    /// </summary>
    public class BuildManagerData
    {
        public string buildFolderPath = string.Empty;
        public string buildName = string.Empty;
        public string buildSuffix = string.Empty;
        public string buildVersion = string.Empty;
        
        public bool enableCopyFolders = true;
        public List<string> copyFolderPaths = new List<string>();
        
        public bool enableCopyFiles = true;
        public List<string> copyFilePaths = new List<string>();
        
        public bool isNotify = false;
        public bool isSoundNotify = false;
        public bool developmentBuild = false;

        /// <summary>
        /// Load data from profile settings into this data object.
        /// </summary>
        public void LoadFrom(BuildManagerSettings.ProfileSettings profileSettings, BuildManagerSettings globalSettings)
        {
            if (profileSettings == null)
            {
                UnityEngine.Debug.LogWarning("Cannot load from null profile settings");
                return;
            }

            // Load profile-specific settings from the shared asset
            buildName = profileSettings.buildName ?? string.Empty;
            buildSuffix = profileSettings.buildSuffix ?? string.Empty;
            buildVersion = profileSettings.buildVersion ?? PlayerSettings.bundleVersion;

            developmentBuild = profileSettings.developmentBuild;

            // Load machine-specific paths from PlayerPrefs (not shared across machines)
            string profileName = profileSettings.profileName;
            buildFolderPath = BuildManagerPathPrefs.GetBuildFolder(profileName);
            enableCopyFolders = BuildManagerPathPrefs.GetEnableCopyFolders(profileName);
            copyFolderPaths = BuildManagerPathPrefs.GetCopyFolders(profileName);
            enableCopyFiles = BuildManagerPathPrefs.GetEnableCopyFiles(profileName);
            copyFilePaths = BuildManagerPathPrefs.GetCopyFiles(profileName);

            // Load global settings
            if (globalSettings != null)
            {
                isNotify = globalSettings.isNotify;
                isSoundNotify = globalSettings.isSoundNotify;
            }
        }

        /// <summary>
        /// Save data from this object to profile settings.
        /// </summary>
        public void SaveTo(BuildManagerSettings.ProfileSettings profileSettings, BuildManagerSettings globalSettings)
        {
            if (profileSettings == null)
            {
                UnityEngine.Debug.LogWarning("Cannot save to null profile settings");
                return;
            }

            // Save profile-specific settings to the shared asset
            profileSettings.buildName = buildName;
            profileSettings.buildSuffix = buildSuffix;
            profileSettings.buildVersion = buildVersion;

            profileSettings.developmentBuild = developmentBuild;

            // Save machine-specific paths to PlayerPrefs (not shared across machines)
            string profileName = profileSettings.profileName;
            BuildManagerPathPrefs.SetBuildFolder(profileName, buildFolderPath);
            BuildManagerPathPrefs.SetEnableCopyFolders(profileName, enableCopyFolders);
            BuildManagerPathPrefs.SetCopyFolders(profileName, copyFolderPaths);
            BuildManagerPathPrefs.SetEnableCopyFiles(profileName, enableCopyFiles);
            BuildManagerPathPrefs.SetCopyFiles(profileName, copyFilePaths);
            PlayerPrefs.Save();

            // Save global settings
            if (globalSettings != null)
            {
                globalSettings.isNotify = isNotify;
                globalSettings.isSoundNotify = isSoundNotify;
            }
        }

        /// <summary>
        /// Validate the data for errors.
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            if (string.IsNullOrEmpty(buildFolderPath))
            {
                errorMessage = "Build folder path is required";
                return false;
            }

            if (string.IsNullOrEmpty(buildName))
            {
                errorMessage = "Build name is required";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
