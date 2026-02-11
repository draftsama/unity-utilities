using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class EditorGUIHelper
{
    // Folders to exclude from file search to prevent performance issues
    private static readonly string[] ExcludedFolders = new string[]
    {
        "Library", "Temp", "Logs", ".git", "Packages", "obj", "Build", "Builds"
    };

    public class FileSearchState
    {
        public int InputNameID;
        public string CurrentNameInput = string.Empty;
        public string[] FilePathsFilter = new string[0];
        public bool IsSearchDisabled = false;
        public string DisabledReason = string.Empty;
    }

    /// <summary>
    /// Checks if the folder is safe to search (not project root containing excluded folders)
    /// </summary>
    private static bool IsSafeToSearch(string folderPath, out string reason)
    {
        reason = string.Empty;

        if (string.IsNullOrEmpty(folderPath))
        {
            reason = "Folder path is empty.";
            return false;
        }

        if (!Directory.Exists(folderPath))
        {
            return true; // Let the caller handle non-existent folders
        }

        // Check if folder contains any excluded folders (likely project root)
        foreach (var excludedFolder in ExcludedFolders)
        {
            var excludedPath = Path.Combine(folderPath, excludedFolder);
            if (Directory.Exists(excludedPath))
            {
                reason = $"Search disabled: Folder appears to be project root (contains '{excludedFolder}'). Please specify a subfolder in 'Folder Name' to avoid performance issues.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets filtered files from directory, excluding Unity system folders
    /// </summary>
    private static IEnumerable<string> GetFilteredFiles(string folderPath, string[] validExtensions)
    {
        var files = new List<string>();

        try
        {
            // Get files in current directory
            foreach (var file in Directory.GetFiles(folderPath))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (validExtensions.Contains(ext))
                {
                    files.Add(file);
                }
            }

            // Recursively get files from subdirectories, excluding system folders
            foreach (var dir in Directory.GetDirectories(folderPath))
            {
                var dirName = Path.GetFileName(dir);
                if (!ExcludedFolders.Contains(dirName))
                {
                    files.AddRange(GetFilteredFiles(dir, validExtensions));
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }
        catch (Exception)
        {
            // Skip any other errors
        }

        return files;
    }

    /// <summary>
    /// Checks if a SerializedProperty is still valid and not disposed
    /// </summary>
    private static bool IsPropertyValid(SerializedProperty property)
    {
        if (property == null)
            return false;

        try
        {
            // Try to access the serializedObject to verify it's not disposed
            var _ = property.serializedObject;
            return _ != null;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Draws a file name field with autocomplete dropdown
    /// </summary>
    /// <param name="fileNameProperty">SerializedProperty for the file name</param>
    /// <param name="resourceFolder">Folder to search in</param>
    /// <param name="validExtensions">Valid file extensions (e.g., ".png", ".jpg")</param>
    /// <param name="state">State object to maintain search state</param>
    /// <param name="onFileSelected">Callback when a file is selected from dropdown</param>
    /// <param name="maxResults">Maximum number of search results to show</param>
    public static void DrawFileSearchField(
        SerializedProperty fileNameProperty,
        string resourceFolder,
        string[] validExtensions,
        FileSearchState state,
        System.Action<string> onFileSelected = null,
        int maxResults = 10)
    {
        // Safety check for disposed SerializedProperty
        if (!IsPropertyValid(fileNameProperty))
        {
            return;
        }

        // Cache the string value to avoid multiple accesses
        string currentValue;
        try
        {
            currentValue = fileNameProperty.stringValue;
        }
        catch (System.Exception)
        {
            return;
        }

        // File name input with change detection
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(fileNameProperty);

        if (EditorGUI.EndChangeCheck())
        {
            state.InputNameID = GUIUtility.keyboardControl;
        }

        // Re-check after PropertyField (may have been disposed)
        if (!IsPropertyValid(fileNameProperty))
        {
            return;
        }

        // Update currentValue after PropertyField
        try
        {
            currentValue = fileNameProperty.stringValue;
        }
        catch (System.Exception)
        {
            return;
        }

        // Show autocomplete dropdown when field is focused (InputNameID != 0 means it has been set)
        if (GUIUtility.keyboardControl == state.InputNameID && state.InputNameID != 0)
        {
            // Check if current value matches any file in the list
            bool valueMatchesFile = false;
            if (!string.IsNullOrEmpty(currentValue))
            {
                valueMatchesFile = state.FilePathsFilter.Any(f =>
                    Path.GetFileName(f).Equals(currentValue, StringComparison.OrdinalIgnoreCase));
            }

            // Show dropdown if: empty, has results, or doesn't match any file
            bool shouldShowDropdown = string.IsNullOrEmpty(currentValue) ||
                                     state.FilePathsFilter.Length > 0 ||
                                     !valueMatchesFile;

            if (shouldShowDropdown)
            {
                EditorGUILayout.BeginVertical("box");

                if (state.FilePathsFilter.Length > 0)
                {
                    GUI.color = Color.cyan;
                    foreach (var file in state.FilePathsFilter)
                    {
                        var name = Path.GetFileName(file);
                        if (GUILayout.Button(name))
                        {
                            // Check property is still valid before modifying
                            if (IsPropertyValid(fileNameProperty))
                            {
                                fileNameProperty.stringValue = name;
                                fileNameProperty.serializedObject.ApplyModifiedProperties();
                            }

                            // Clear the filter to prevent dropdown from showing again
                            state.FilePathsFilter = new string[0];
                            
                            // Unfocus the field
                            GUIUtility.keyboardControl = 0;

                            // Reset state after selection (use -1 to avoid 0 == 0 match)
                            state.InputNameID = -1;

                            // Invoke callback (this may cause the property to be disposed)
                            onFileSelected?.Invoke(name);

                            // Force GUI to repaint
                            GUI.changed = true;
                        }
                    }
                    GUI.color = Color.white;
                }
                else if (!string.IsNullOrEmpty(currentValue))
                {
                    // Show "no results" message if user typed something but nothing matched
                    EditorGUILayout.HelpBox($"No files found matching '{currentValue}'", MessageType.Info);
                }

                EditorGUILayout.EndVertical();
            }
        }

        // Update file search results - use cached value or try to get new one
        string valueForSearch = currentValue;
        if (IsPropertyValid(fileNameProperty))
        {
            try
            {
                valueForSearch = fileNameProperty.stringValue;
            }
            catch (System.Exception)
            {
                // Use cached value
            }
        }

        if (state.CurrentNameInput != valueForSearch ||
            string.IsNullOrEmpty(valueForSearch))
        {
            state.CurrentNameInput = valueForSearch;

            // Check if folder is safe to search (not project root or excluded)
            if (!IsSafeToSearch(resourceFolder, out string disabledReason))
            {
                state.IsSearchDisabled = true;
                state.DisabledReason = disabledReason;
                state.FilePathsFilter = new string[0];
            }
            else if (Directory.Exists(resourceFolder))
            {
                state.IsSearchDisabled = false;
                state.DisabledReason = string.Empty;

                if (string.IsNullOrEmpty(state.CurrentNameInput))
                {
                    // Show recent files when empty (sorted by last write time)
                    state.FilePathsFilter = GetFilteredFiles(resourceFolder, validExtensions)
                                        .OrderByDescending(f => File.GetLastWriteTime(f))
                                        .Take(maxResults)
                                        .ToArray();
                }
                else
                {
                    // Search by pattern
                    Regex regexPattern = new Regex(Regex.Escape(state.CurrentNameInput), RegexOptions.IgnoreCase);

                    state.FilePathsFilter = GetFilteredFiles(resourceFolder, validExtensions)
                                        .Where(file => regexPattern.IsMatch(Path.GetFileName(file)))
                                        .Take(maxResults)
                                        .ToArray();
                }
            }
            else
            {
                state.IsSearchDisabled = false;
                state.FilePathsFilter = new string[0];
            }
        }

        // Show warning if search is disabled
        if (state.IsSearchDisabled)
        {
            EditorGUILayout.HelpBox(state.DisabledReason, MessageType.Warning);
        }
    }

    public static void DrawComponentProperty(GameObject instance, SerializedProperty _property, Type _componentType)
    {
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = false;
        EditorGUILayout.PropertyField(_property);
        GUI.enabled = true;
        GUI.color = Color.green;
        if (_property.objectReferenceValue == null)
        {
            //try to get
            var component = instance.GetComponent(_componentType);

            //if not found, add component
            if (component == null && GUILayout.Button("Add"))
            {
                component = instance.AddComponent(_componentType);
            }

            _property.objectReferenceValue = component;
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
    }
}
