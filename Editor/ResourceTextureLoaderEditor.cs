using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

namespace Modules.Utilities.Editor
{
    [CustomEditor(typeof(ResourceTextureLoader))]
    public class ResourceTextureLoaderEditor : UnityEditor.Editor
    {
        private string _ResourceFolder;
        private ResourceManager _ResourceManager;
        private EditorGUIHelper.FileSearchState _FileSearchState = new EditorGUIHelper.FileSearchState();

        private SerializedProperty _OutputTypeProperty;
        private SerializedProperty _TextureWrapMode;
        private SerializedProperty _FilterMode;
        private SerializedProperty _FileNameProperty;
        private SerializedProperty _ContentSizeModeProperty;
        private SerializedProperty _SpriteFitScreenProperty;
        private SerializedProperty _PixelPerUnitProperty;
        private SerializedProperty _SpritePivotProperty;

        private SerializedProperty _PreviewSourceProperty;

        //output property
        private SerializedProperty _RawImage,
        _Image,
        _AspectRatioFitter,
        _SpriteRenderer;

        // Deferred load - to prevent disposed SerializedObject errors
        private string _PendingLoadFileName = null;



        private void OnEnable()
        {
            _ResourceManager = ResourceManager.GetInstance();
            _ResourceFolder = ResourceManager.GetResourceFolderPath();

            var instance = target as ResourceTextureLoader;

            _OutputTypeProperty = serializedObject.FindProperty(nameof(instance.m_OutputType));
            _TextureWrapMode = serializedObject.FindProperty(nameof(instance.m_TextureWrapMode));
            _FilterMode = serializedObject.FindProperty(nameof(instance.m_FilterMode));
            _ContentSizeModeProperty = serializedObject.FindProperty(nameof(instance.m_ContentSizeMode));
            _RawImage = serializedObject.FindProperty(nameof(instance.m_RawImage));
            _Image = serializedObject.FindProperty(nameof(instance.m_Image));
            _SpriteRenderer = serializedObject.FindProperty(nameof(instance.m_SpriteRenderer));
            _AspectRatioFitter = serializedObject.FindProperty(nameof(instance.m_AspectRatioFitter));
            _PixelPerUnitProperty = serializedObject.FindProperty(nameof(instance.m_PixelPerUnit));
            _SpritePivotProperty = serializedObject.FindProperty(nameof(instance.m_SpritePivot));
            _SpriteFitScreenProperty = serializedObject.FindProperty(nameof(instance.m_SpriteFitScreen));
            _FileNameProperty = serializedObject.FindProperty(nameof(instance.m_FileName));
            _PreviewSourceProperty = serializedObject.FindProperty(nameof(instance.m_PreviewSource));


        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var instance = target as ResourceTextureLoader;

            if (ResourceManager.GetInstance().m_ResourceSettingAssets == null)
            {
                EditorGUILayout.HelpBox("ResourceSettingAssets is not set.", MessageType.Error);
                return;
            }


            GUI.enabled = false;
            //show resource manager object 
            EditorGUILayout.ObjectField("Resource Manager", _ResourceManager, typeof(ResourceManager), false);

            //show resource folder
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Resource Folder", _ResourceFolder);
            GUI.enabled = true;

            if (GUILayout.Button("Open", GUILayout.Width(60)))
            {
                EditorUtility.RevealInFinder(_ResourceFolder);
            }
            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;


            //Preview Texture2D
            if (_PreviewSourceProperty.objectReferenceValue != null)
            {
                GUILayout.Label("Preview", GUILayout.ExpandWidth(true));
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                var previewTexture = (Texture2D)_PreviewSourceProperty.objectReferenceValue;
                var aspectRatio = (float)previewTexture.height / (float)previewTexture.width;
                GUILayout.Box(previewTexture, GUILayout.Width(240), GUILayout.Height(aspectRatio * 240f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(10);
            }


            //name input field
            string[] imageExtensions = { ".png", ".jpg", ".jpeg" };
            EditorGUIHelper.DrawFileSearchField(
                _FileNameProperty,
                _ResourceFolder,
                imageExtensions,
                _FileSearchState,
                (selectedFileName) =>
                {
                    // Defer loading to prevent disposed SerializedObject errors
                    _PendingLoadFileName = Path.GetFileName(selectedFileName);
                }
            );

            // Handle deferred load at end of GUI frame
            if (!string.IsNullOrEmpty(_PendingLoadFileName))
            {
                var fileToLoad = _PendingLoadFileName;
                _PendingLoadFileName = null;

                // Apply any pending changes before loading
                if (serializedObject.hasModifiedProperties)
                {
                    serializedObject.ApplyModifiedProperties();
                }

                // Use delayCall to load after GUI frame completes
                EditorApplication.delayCall += () =>
                {
                    Debug.Log("Selected file: " + fileToLoad);
                    LoadImage(fileToLoad);
                    if (target != null)
                    {
                        EditorUtility.SetDirty(target);
                    }
                };

                // Return early to prevent accessing disposed properties
                return;
            }

            EditorGUILayout.PropertyField(_TextureWrapMode);
            EditorGUILayout.PropertyField(_FilterMode);

            // Texture Size Settings
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(_ContentSizeModeProperty);


            EditorGUILayout.BeginVertical("box");
            // EditorGUILayout.PropertyField(_OutputTypeProperty);

            //draw popup enum
            EditorGUILayout.PropertyField(_OutputTypeProperty);
            

            var outputMode = (OutputType)_OutputTypeProperty.enumValueIndex;
            if (outputMode == OutputType.None)
            {
                EditorGUILayout.HelpBox("Please select an output type.", MessageType.Warning);

            }
            else if (outputMode == OutputType.Image)
            {
                EditorGUIHelper.DrawComponentProperty(instance.gameObject, _Image, typeof(Image));
                EditorGUIHelper.DrawComponentProperty(instance.gameObject, _AspectRatioFitter, typeof(AspectRatioFitter));
            }
            else if (outputMode == OutputType.RawImage)
            {
                EditorGUIHelper.DrawComponentProperty(instance.gameObject, _RawImage, typeof(RawImage));
                EditorGUIHelper.DrawComponentProperty(instance.gameObject, _AspectRatioFitter, typeof(AspectRatioFitter));
            }
            else if (outputMode == OutputType.SpriteRenderer)
            {

                EditorGUILayout.PropertyField(_SpriteFitScreenProperty);
                GUI.enabled = !_SpriteFitScreenProperty.boolValue;
                EditorGUILayout.PropertyField(_PixelPerUnitProperty);
                GUI.enabled = true;
                EditorGUILayout.PropertyField(_SpritePivotProperty);
                EditorGUIHelper.DrawComponentProperty(instance.gameObject, _SpriteRenderer, typeof(SpriteRenderer));
            }

            else if (outputMode == OutputType.Material)
            {
                var currentMaterialIndex = serializedObject.FindProperty("_CurrentMaterialIndex");
                var currentTexturePropertyIndex = serializedObject.FindProperty("_CurrentTexturePropertyIndex");
                EditorGUILayout.HelpBox("the Material can not share with other object", MessageType.Info);

                //get all materials in this object
                var renderers = instance.GetComponentsInChildren<Renderer>();

                //generate gameobject name and materials name all 
                //ex: "Cube - Material1" "Cube - Material2"

                try
                {
                    var materials = renderers.SelectMany(x => x.sharedMaterials).ToList();
                    //get material key

                    var popupMaterialsName = renderers.SelectMany(x => x.sharedMaterials.Select(y => x.name + " - " + y.name)).ToList();


                    var index = currentMaterialIndex.intValue;
                    var texturePropertyIndex = currentTexturePropertyIndex.intValue;

                    index = Mathf.Clamp(index, 0, materials.Count - 1);


                    if (popupMaterialsName.Count > 1)
                    {

                        index = EditorGUILayout.Popup("Material", index, popupMaterialsName.ToArray());
                    }
                    else
                    {
                        index = 0;
                        EditorGUILayout.LabelField("Material", popupMaterialsName[index]);

                    }


#if UNITY_2022_1_OR_NEWER
                    var propNames = materials[index].GetPropertyNames(MaterialPropertyType.Texture);

                    if (propNames.Length > 0)
                    {
                        texturePropertyIndex = Mathf.Clamp(texturePropertyIndex, 0, propNames.Length - 1);

                        texturePropertyIndex = EditorGUILayout.Popup("Texture Property", texturePropertyIndex, propNames.ToArray());
                        currentTexturePropertyIndex.intValue = texturePropertyIndex;
                    }

#else
            var propNames = materials[index].GetTexturePropertyNames().ToList();
            if (propNames.Count > 0)
            {
                texturePropertyIndex = Mathf.Clamp(texturePropertyIndex, 0, propNames.Count - 1);

                texturePropertyIndex = EditorGUILayout.Popup("Texture Property", texturePropertyIndex, propNames.ToArray());
                currentTexturePropertyIndex.intValue = texturePropertyIndex;
            }
           
#endif





                    currentMaterialIndex.intValue = index;


                    GUI.contentColor = Color.yellow;
                    GUI.backgroundColor = Color.gray;
                    if (GUILayout.Button("Clear Texture"))
                    {
                        instance.ApplyTexture(null);
                    }
                    if (GUILayout.Button("Clear All Texture"))
                    {
                        foreach (var p in propNames)
                        {
                            materials[index].SetTexture(p, null);
                        }
                    }
                    GUI.contentColor = Color.white;
                    GUI.backgroundColor = Color.white;

                }
                catch (System.Exception)
                {
                    EditorGUILayout.HelpBox("Material not found", MessageType.Error);

                }

            }



            EditorGUILayout.EndVertical();


            GUI.color = Color.green;
            if (GUILayout.Button("Load"))
            {
                LoadImage(_FileNameProperty.stringValue);
            }
            GUI.color = Color.white;

            if (serializedObject.hasModifiedProperties)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
        }

        public async Task LoadImage(string _filename)
        {
            if (string.IsNullOrEmpty(_filename))
                return;

            // Use ResourceManager to find the file path (already searched in autocomplete)
            var path = ResourceManager.GetPathByNameAsync(_filename);
            Debug.Log("Loading image from path: " + path);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"File not found: {_filename}");
                return;
            }

            var folderName = ResourceManager.GetResourceSettingAssets().m_ExternalResourcesFolderName;
            var relativeFolder = Path.Combine("ResourcesEditor", "Editor", folderName);
            var fileAssetPath = Path.Combine("Assets", relativeFolder, _filename);
            var assetfolder = Path.Combine(Application.dataPath, relativeFolder);
            var filePath = Path.Combine(assetfolder, _filename);


            if (!string.IsNullOrEmpty(path))
            {
                if (!Directory.Exists(assetfolder))
                    Directory.CreateDirectory(assetfolder);

                File.Copy(path, filePath, true);
                AssetDatabase.Refresh();

                //    Debug.Log($"Copied file to: {filePath}");


                //modify texture import setting
                var importer = AssetImporter.GetAtPath(fileAssetPath) as TextureImporter;
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = (TextureWrapMode)_TextureWrapMode.enumValueIndex;
                importer.filterMode = (FilterMode)_FilterMode.enumValueIndex;
                importer.mipmapEnabled = false;
                importer.isReadable = true;
                importer.alphaIsTransparency = true;


                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;

                importer.SaveAndReimport();
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(fileAssetPath);
                var instance = target as ResourceTextureLoader;
                
                // Don't use Update() here as it will discard any pending changes
                // Just find and set the property directly
                var previewProp = serializedObject.FindProperty(nameof(instance.m_PreviewSource));
                previewProp.objectReferenceValue = texture;
                serializedObject.ApplyModifiedProperties();

                Debug.Log($"Loaded texture: {_filename} size: {texture.width}x{texture.height}");

                instance.ApplyTexture(texture);



            }

        }
    }
}