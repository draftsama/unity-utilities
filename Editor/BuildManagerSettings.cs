using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Modules.Utilities.Editor
{
    [CreateAssetMenu(fileName = "BuildManagerSettings", menuName = "Build Manager/Settings")]
    public class BuildManagerSettings : ScriptableObject
    {
        [Header("Profile Settings")]
        public int selectedBuildProfileIndex = 0;
        
        [Header("Global Build Configuration")]
        public bool isNotify = false;
        public bool isSoundNotify = false;
        
        [Header("Profile-Specific Settings")]
        [SerializeField]
        public List<ProfileSettings> profileSettings = new List<ProfileSettings>();
        
        [System.Serializable]
        public class ProfileSettings
        {
            [Header("Profile Info")]
            public string profileName;
            
            [Header("Build Settings")]
            public string buildFolderPath = "";
            public string buildName = "";
            public string buildSuffix = "";
            public string buildVersion = "";
            
            [Header("Copy Folders (Windows Only)")]
            public bool enableCopyFolders = true;
            public List<string> copyFolderPaths = new List<string>();
            
            [Header("Copy Files (Windows Only)")]
            public bool enableCopyFiles = true;
            public List<string> copyFilePaths = new List<string>();
            
            public ProfileSettings(string name)
            {
                profileName = name;
                buildFolderPath = string.Empty;
                buildName = name;
                buildSuffix = string.Empty;
                buildVersion = UnityEditor.PlayerSettings.bundleVersion;
                enableCopyFolders = true;
                copyFolderPaths = new List<string>();
                enableCopyFiles = true;
                copyFilePaths = new List<string>();
            }
            
            public bool IsValid()
            {
                return !string.IsNullOrEmpty(profileName);
            }
        }
        
        /// <summary>
        /// Get or create profile settings for a specific profile name
        /// </summary>
        public ProfileSettings GetOrCreateProfileSettings(string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
            {
                Debug.LogWarning("Profile name cannot be null or empty");
                return null;
            }
            
            var existing = profileSettings.Find(p => p.profileName == profileName);
            if (existing != null)
                return existing;
                
            var newSettings = new ProfileSettings(profileName);
            profileSettings.Add(newSettings);
            
            Debug.Log($"Created new profile settings for: {profileName}");
            return newSettings;
        }
        
        /// <summary>
        /// Remove profile settings for a specific profile name
        /// </summary>
        public void RemoveProfileSettings(string profileName)
        {
            int removed = profileSettings.RemoveAll(p => p.profileName == profileName);
            if (removed > 0)
            {
                Debug.Log($"Removed {removed} profile settings for: {profileName}");
            }
        }
        
        /// <summary>
        /// Clean up settings for profiles that no longer exist
        /// </summary>
        public void CleanupOrphanedSettings(string[] existingProfileNames)
        {
            int initialCount = profileSettings.Count;
            profileSettings.RemoveAll(p => Array.IndexOf(existingProfileNames, p.profileName) == -1);
            int finalCount = profileSettings.Count;
            
            if (initialCount != finalCount)
            {
                Debug.Log($"Cleaned up {initialCount - finalCount} orphaned profile settings");
            }
        }
        
        /// <summary>
        /// Get all profile names that have settings
        /// </summary>
        public string[] GetConfiguredProfileNames()
        {
            return profileSettings.Where(p => p.IsValid()).Select(p => p.profileName).ToArray();
        }
        
        /// <summary>
        /// Check if settings exist for a specific profile
        /// </summary>
        public bool HasSettingsForProfile(string profileName)
        {
            return profileSettings.Any(p => p.profileName == profileName && p.IsValid());
        }
    }
}
