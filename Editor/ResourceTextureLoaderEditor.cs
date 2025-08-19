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
        private int _InputNameID;
        private string _ResourceFolder;

        private ResourceManager _ResourceManager;

        private string _CurrentNameInput;

        private string[] _FilePathsFilter;

        private Texture2D _PreviewTexture;

        private SerializedProperty _OutputTypeProperty;


        private SerializedProperty _TextureWrapMode;


        private SerializedProperty _FilterMode;
        private SerializedProperty _FileNameProperty;


        private SerializedProperty _ContentSizeModeProperty;

        private SerializedProperty _SpriteFitScreenProperty;
        private SerializedProperty _PixelPerUnitProperty;

        private SerializedProperty _SpritePivotProperty;



        //output property
        private SerializedProperty _RawImage,
        _Image,
        _AspectRatioFitter,
        _SpriteRenderer;



        private void OnEnable()
        {


            _CurrentNameInput = string.Empty;
            _InputNameID = GUIUtility.keyboardControl;

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

            var editorSource = serializedObject.FindProperty(nameof(instance.m_EditorSource));
            _PreviewTexture = editorSource.objectReferenceValue as Texture2D;
        }
        public override void OnInspectorGUI()
        {

            serializedObject.Update();
            var instance = target as ResourceTextureLoader;


            EditorGUI.BeginChangeCheck();

            //if _ResourceSettingAssetsProperty is null 

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

            if (instance.m_EditorSource != null)
            {
                GUILayout.Label("Preview", GUILayout.ExpandWidth(true));
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                var aspectRatio = (float)_PreviewTexture.height / (float)_PreviewTexture.width;
                GUILayout.Box(_PreviewTexture, GUILayout.Width(240), GUILayout.Height(aspectRatio * 240f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(10);
            }


            //name input field
            EditorGUILayout.PropertyField(_FileNameProperty);



            if (EditorGUI.EndChangeCheck())
                _InputNameID = GUIUtility.keyboardControl;



            if (_FilePathsFilter != null && _FilePathsFilter.Length > 0 && GUIUtility.keyboardControl == _InputNameID)
            {

                EditorGUILayout.BeginVertical("box");

                GUI.color = Color.cyan;
                foreach (var file in _FilePathsFilter)
                {
                    var name = Path.GetFileName(file);

                    if (GUILayout.Button(name))
                    {
                        _FileNameProperty.stringValue = name;
                        //delay load image

                        LoadImage(_FileNameProperty.stringValue);
                        GUIUtility.keyboardControl = 0;

                    }
                }
                GUI.color = Color.white;
                EditorGUILayout.EndVertical();
            }




            EditorGUILayout.PropertyField(_TextureWrapMode);
            EditorGUILayout.PropertyField(_FilterMode);

            // Texture Size Settings
            EditorGUILayout.Space();

          

            if (_CurrentNameInput != _FileNameProperty.stringValue || _FileNameProperty.stringValue == string.Empty)
            {
                _CurrentNameInput = _FileNameProperty.stringValue;

                Regex regexPattern = new Regex(_CurrentNameInput, RegexOptions.IgnoreCase);

                if (Directory.Exists(_ResourceFolder))
                {



                    _FilePathsFilter = Directory.GetFiles(_ResourceFolder, "*.*", SearchOption.AllDirectories)
                                        .Where(file => new string[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(file)) && regexPattern.IsMatch(Path.GetFileName(file)))
                                        .Take(10)
                                        .ToArray();


                }
                else
                {

                    //HelpBox if folder not found
                    EditorGUILayout.HelpBox("Resource folder not found: " + _ResourceFolder, MessageType.Warning);
                    _FilePathsFilter = new string[0];
                }

            }
            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
            serializedObject.ApplyModifiedProperties();

            GUI.color = Color.white;



            EditorGUILayout.PropertyField(_ContentSizeModeProperty);


            EditorGUILayout.BeginVertical("box");

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

            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
            serializedObject.ApplyModifiedProperties();
        }

        public async void LoadImage(string _filename)
        {
            //delay load image
            if (string.IsNullOrEmpty(_filename))
                return;

            await Task.Delay(100);

            var folderName = ResourceManager.GetResourceSettingAssets().m_ExternalResourcesFolderName;
            var relativeFolder = Path.Combine("ResourcesEditor", "Editor", folderName);
            var fileAssetPath = Path.Combine("Assets", relativeFolder, _filename);




            var assetfolder = Path.Combine(Application.dataPath, relativeFolder);
            var filePath = Path.Combine(assetfolder, _filename);

            var path = Directory.GetFiles(_ResourceFolder, "*.*", SearchOption.AllDirectories)
                  .FirstOrDefault(file => Path.GetFileName(file) == _filename);
            if (!string.IsNullOrEmpty(path))
            {
                if (!Directory.Exists(assetfolder))
                    Directory.CreateDirectory(assetfolder);

                File.Copy(path, filePath, true);
                AssetDatabase.Refresh();

                //modify texture import setting
                //  var importer = AssetImporter.GetAtPath(fileAssetPath) as TextureImporter;
                // importer.textureType = (TextureImporterType)_TextureTypeValueProperty.intValue;
                // importer.mipmapEnabled = _GenerateMipMaps.boolValue;
                // importer.alphaSource = TextureImporterAlphaSource.FromInput;
                // importer.alphaIsTransparency = _AlphaIsTransparency.boolValue;
                // importer.wrapMode = (TextureWrapMode)_TextureWrapMode.intValue;
                // importer.filterMode = (FilterMode)_FilterMode.intValue;
                // importer.isReadable = true;
                // importer.spritePixelsPerUnit = _PixelPerUnitProperty.floatValue;

                // // Set texture size/resolution settings
                // importer.maxTextureSize = _MaxTextureSizeProperty.intValue;
                // importer.textureCompression = (TextureImporterCompression)_TextureCompressionProperty.intValue;
                // importer.npotScale = (TextureImporterNPOTScale)_NPOTScaleProperty.intValue;

                // importer.SaveAndReimport();



                // texture = AssetDatabase.LoadAssetAtPath<Texture2D>(fileAssetPath);

                var instance = target as ResourceTextureLoader;
                instance.LoadTexture().ContinueWith(t =>
                {
                    _PreviewTexture = t;
                    instance.SetEditorSource(t);
                    Debug.Log($"loaded image: {_filename} image size: {t.width}x{t.height}");

                }).Forget();

            }

        }
    }
}