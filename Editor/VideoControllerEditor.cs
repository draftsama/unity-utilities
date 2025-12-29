using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Modules.Utilities.Editor
{

    [CustomEditor(typeof(VideoController))]
    [CanEditMultipleObjects]
    public class VideoControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty _RawImage;
        private SerializedProperty _CanvasGroup;
        private SerializedProperty _AspectRatioFitter;
        private SerializedProperty _PlayWithParentShow;
        private SerializedProperty _ParentCanvasGroup;
        private SerializedProperty _CanvasGroupThreshold;

        private SerializedProperty _MeshFilter;
        private SerializedProperty _MeshRenderer;
        private SerializedProperty _Material;

        private SerializedProperty _ContentSizeMode;
        private SerializedProperty _Progress;

        // File search fields
        private string _ResourceFolder;
        private EditorGUIHelper.FileSearchState _FileSearchState = new EditorGUIHelper.FileSearchState();
        private SerializedProperty _FileNameProperty;
        private SerializedProperty _FolderNameProperty;
        private SerializedProperty _PathTypeProperty;

        private VideoController instance;
        public void OnEnable()
        {
            instance = (VideoController)target;
            if (!Application.isPlaying) instance.Init();
            serializedObject.Update();
            _RawImage = serializedObject.FindProperty("_RawImage");
            _CanvasGroup = serializedObject.FindProperty("_CanvasGroup");
            _AspectRatioFitter = serializedObject.FindProperty("_AspectRatioFitter");
            _PlayWithParentShow = serializedObject.FindProperty("_PlayWithParentShow");
            _ParentCanvasGroup = serializedObject.FindProperty("_ParentCanvasGroup");
            _CanvasGroupThreshold = serializedObject.FindProperty("_CanvasGroupThreshold");

            _MeshFilter = serializedObject.FindProperty("_MeshFilter");
            _MeshRenderer = serializedObject.FindProperty("_MeshRenderer");
            _Material = serializedObject.FindProperty("_Material");

            _ContentSizeMode = serializedObject.FindProperty("_ContentSizeMode");
            _Progress = serializedObject.FindProperty("m_Progress");

            // Initialize file search properties
            _FileNameProperty = serializedObject.FindProperty("m_FileName");
            _FolderNameProperty = serializedObject.FindProperty("m_FolderName");
            _PathTypeProperty = serializedObject.FindProperty("m_PathType");
            
            // Get resource folder based on PathType
            UpdateResourceFolder();
        }

        // ReSharper disable Unity.PerformanceAnalysis
        // private void DrawObjectProperty(SerializedProperty _property, Type _componentType)
        // {
        //     EditorGUILayout.BeginHorizontal();
        //     GUI.enabled = false;
        //     EditorGUILayout.PropertyField(_property);
        //     GUI.enabled = true;
        //     GUI.color = Color.green;
        //     if (_property.objectReferenceValue == null && GUILayout.Button("Add"))
        //     {
        //         var componentObject = instance.gameObject.AddComponent(_componentType);
        //         _property.objectReferenceValue = componentObject;
        //     }

        //     GUI.color = Color.white;

        //     EditorGUILayout.EndHorizontal();
        // }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Draw file name field with autocomplete
            DrawFileSearchField();
            
            // Draw other properties manually (exclude m_FileName to avoid duplication)
            var outputType = serializedObject.FindProperty("m_OutputType");
            var startMode = serializedObject.FindProperty("m_StartMode");
            var loop = serializedObject.FindProperty("m_Loop");
            var fadeVideo = serializedObject.FindProperty("m_FadeVideo");
            var fadeAudio = serializedObject.FindProperty("m_FadeAudio");
            var fadeTime = serializedObject.FindProperty("m_FadeTime");
            var keepLastFrame = serializedObject.FindProperty("m_KeepLastframe");
            
            EditorGUILayout.PropertyField(startMode);
            EditorGUILayout.PropertyField(loop);
            EditorGUILayout.PropertyField(fadeVideo);
            EditorGUILayout.PropertyField(fadeAudio);
            EditorGUILayout.PropertyField(fadeTime);
            EditorGUILayout.PropertyField(keepLastFrame);
            EditorGUILayout.PropertyField(outputType);

            EditorGUILayout.PropertyField(_ContentSizeMode);

            if (outputType.enumValueIndex == (int)VideoOutputType.Renderer)
            {


                EditorGUIHelper.DrawComponentProperty(instance.gameObject, _MeshFilter, typeof(MeshFilter));
                EditorGUIHelper.DrawComponentProperty(instance.gameObject, _MeshRenderer, typeof(MeshRenderer));
                _PlayWithParentShow.boolValue = false;
                EditorGUILayout.PropertyField(_Material);
            }
            else if (outputType.enumValueIndex == (int)VideoOutputType.RawImage)
            {
                EditorGUIHelper.DrawComponentProperty(instance.gameObject, _RawImage, typeof(RawImage));
                EditorGUIHelper.DrawComponentProperty(instance.gameObject, _CanvasGroup, typeof(CanvasGroup));
                EditorGUIHelper.DrawComponentProperty(instance.gameObject, _AspectRatioFitter, typeof(AspectRatioFitter));
                if (startMode.enumValueIndex == (int)VideoStartMode.AutoPlay)
                {

                    EditorGUILayout.PropertyField(_PlayWithParentShow);
                    if (_PlayWithParentShow.boolValue)
                    {
                        if (_ParentCanvasGroup.objectReferenceValue == null)
                            _ParentCanvasGroup.objectReferenceValue = instance.transform.parent.GetComponent<CanvasGroup>();

                        EditorGUILayout.PropertyField(_ParentCanvasGroup);
                        if (_ParentCanvasGroup.objectReferenceValue == null)
                        {
                            //helpbox
                            EditorGUILayout.HelpBox("Parent CanvasGroup is null, please assign it.", MessageType.Error);
                        }

                        EditorGUILayout.Slider(_CanvasGroupThreshold, 0.1f, 1f);
                    }
                }
            }

            if (Application.isPlaying)
            {


                if (GUILayout.Button("Play"))
                {
                    instance.PlayAsync().Forget();
                }
                if (GUILayout.Button("Pause"))
                {
                    instance.Pause();
                }
                if (GUILayout.Button("Resume"))
                {
                    var frame = instance.GetCurrentFrame();
                    instance.PlayAsync(frame, _resume: true).Forget();
                }
                if (GUILayout.Button("Stop"))
                {
                    instance.Stop();
                }
                if (GUILayout.Button("Prepare with First Frame"))
                {
                    instance.PrepareFirstFrame().Forget();
                }

                if (GUILayout.Button("Prepare"))
                {
                    instance.Prepare().Forget();
                }

                // slider for progress
                var updateProgress = EditorGUILayout.Slider("Progress", _Progress.floatValue, 0f, 1f);
                
                if (GUI.changed && updateProgress != _Progress.floatValue)
                {
                    _Progress.floatValue = updateProgress;
                    instance.Seek(updateProgress);
                }
               

                //slider for volume
            }
          

            if (GUI.changed)
                {
                    instance.Init();
                    EditorUtility.SetDirty(instance);
                }

            serializedObject.ApplyModifiedProperties();
        }

        private void UpdateResourceFolder()
        {
            var pathType = (PathType)_PathTypeProperty.enumValueIndex;
            
            switch (pathType)
            {
                case PathType.ExternalResources:
                    _ResourceFolder = ResourceManager.GetResourceFolderPath();
                    break;
                case PathType.StreamingAssets:
                    _ResourceFolder = Application.streamingAssetsPath;
                    break;
                case PathType.Relative:
                    _ResourceFolder = System.Environment.CurrentDirectory;
                    if (!string.IsNullOrEmpty(_FolderNameProperty.stringValue))
                    {
                        _ResourceFolder = Path.Combine(_ResourceFolder, _FolderNameProperty.stringValue);
                    }
                    break;
                case PathType.Absolute:
                    _ResourceFolder = _FolderNameProperty.stringValue;
                    break;
            }
        }

        private void DrawFileSearchField()
        {
            EditorGUILayout.BeginVertical("box");
            
            // Show resource folder info
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = false;
            EditorGUILayout.TextField("Search Folder", _ResourceFolder);
            GUI.enabled = true;
            
            if (GUILayout.Button("📁", GUILayout.Width(30)))
            {
                if (Directory.Exists(_ResourceFolder))
                {
                    EditorUtility.RevealInFinder(_ResourceFolder);
                }
                else
                {
                    EditorUtility.DisplayDialog("Folder Not Found", $"Folder does not exist:\n{_ResourceFolder}", "OK");
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // PathType field
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_PathTypeProperty);
            bool pathTypeChanged = EditorGUI.EndChangeCheck();
            
            // FolderName field with validation
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_FolderNameProperty);
            bool folderChanged = EditorGUI.EndChangeCheck();
            
            if (pathTypeChanged || folderChanged)
            {
                UpdateResourceFolder();
            }
            
            // Show warning if folder doesn't exist
            if (!string.IsNullOrEmpty(_ResourceFolder) && !Directory.Exists(_ResourceFolder))
            {
                EditorGUILayout.HelpBox($"Warning: Folder does not exist!\nPath: {_ResourceFolder}", MessageType.Warning);
            }

            // File name input with autocomplete using EditorGUIHelper
            string[] videoExtensions = { ".mp4", ".mov", ".avi", ".webm", ".mkv", ".flv", ".wmv" };
            EditorGUIHelper.DrawFileSearchField(
                _FileNameProperty,
                _ResourceFolder,
                videoExtensions,
                _FileSearchState,
                (selectedFileName) =>
                {
                    // Update URL in VideoController
                    instance.SetupURL(selectedFileName, (PathType)_PathTypeProperty.enumValueIndex, _FolderNameProperty.stringValue);
                    EditorUtility.SetDirty(instance);
                }
            );
            
            EditorGUILayout.EndVertical();
        }
    }
}

