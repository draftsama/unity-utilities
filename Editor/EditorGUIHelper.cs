using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class EditorGUIHelper
{
    public class FileSearchState
    {
        public int InputNameID;
        public string CurrentNameInput = string.Empty;
        public string[] FilePathsFilter = new string[0];
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
        // File name input with change detection
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(fileNameProperty);
        
        if (EditorGUI.EndChangeCheck())
        {
            state.InputNameID = GUIUtility.keyboardControl;
        }

        // Show autocomplete dropdown
        if (state.FilePathsFilter != null && state.FilePathsFilter.Length > 0 && 
            GUIUtility.keyboardControl == state.InputNameID)
        {
            EditorGUILayout.BeginVertical("box");
            GUI.color = Color.cyan;
            
            foreach (var file in state.FilePathsFilter)
            {
                var name = Path.GetFileName(file);
                if (GUILayout.Button(name))
                {
                    fileNameProperty.stringValue = name;
                    fileNameProperty.serializedObject.ApplyModifiedProperties();
                    
                    // Invoke callback
                    onFileSelected?.Invoke(name);
                    
                    GUIUtility.keyboardControl = 0;
                }
            }
            
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
        }

        // Update file search results
        if (state.CurrentNameInput != fileNameProperty.stringValue || 
            fileNameProperty.stringValue == string.Empty)
        {
            state.CurrentNameInput = fileNameProperty.stringValue;
            
            if (Directory.Exists(resourceFolder))
            {
                if (string.IsNullOrEmpty(state.CurrentNameInput))
                {
                    state.FilePathsFilter = new string[0];
                }
                else
                {
                    Regex regexPattern = new Regex(Regex.Escape(state.CurrentNameInput), RegexOptions.IgnoreCase);

                    state.FilePathsFilter = Directory.GetFiles(resourceFolder, "*.*", SearchOption.AllDirectories)
                                        .Where(file =>
                                        {
                                            var ext = Path.GetExtension(file).ToLowerInvariant();
                                            return validExtensions.Contains(ext) &&
                                                   regexPattern.IsMatch(Path.GetFileName(file));
                                        })
                                        .Take(maxResults)
                                        .ToArray();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Search folder not found: " + resourceFolder, MessageType.Warning);
                state.FilePathsFilter = new string[0];
            }
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
