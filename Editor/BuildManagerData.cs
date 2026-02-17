using System.Collections.Generic;
using UnityEditor;

namespace Modules.Utilities.Editor
{
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

            // Load profile-specific settings
            buildFolderPath = profileSettings.buildFolderPath ?? string.Empty;
            buildName = profileSettings.buildName ?? string.Empty;
            buildSuffix = profileSettings.buildSuffix ?? string.Empty;
            buildVersion = profileSettings.buildVersion ?? PlayerSettings.bundleVersion;
            
            enableCopyFolders = profileSettings.enableCopyFolders;
            copyFolderPaths = profileSettings.copyFolderPaths != null 
                ? new List<string>(profileSettings.copyFolderPaths) 
                : new List<string>();
            
            enableCopyFiles = profileSettings.enableCopyFiles;
            copyFilePaths = profileSettings.copyFilePaths != null 
                ? new List<string>(profileSettings.copyFilePaths) 
                : new List<string>();

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

            // Save profile-specific settings
            profileSettings.buildFolderPath = buildFolderPath;
            profileSettings.buildName = buildName;
            profileSettings.buildSuffix = buildSuffix;
            profileSettings.buildVersion = buildVersion;
            
            profileSettings.enableCopyFolders = enableCopyFolders;
            profileSettings.copyFolderPaths = new List<string>(copyFolderPaths);
            
            profileSettings.enableCopyFiles = enableCopyFiles;
            profileSettings.copyFilePaths = new List<string>(copyFilePaths);

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
