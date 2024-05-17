using System.Text.RegularExpressions;
using System.Reflection;
using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Modules.Utilities;
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modules.Utilities
{
    [RequireComponent(typeof(AspectRatioFitter))]
    public class ResourceImageLoader : MonoBehaviour, ILayoutSelfController
    {
        public enum Type
        {
            Image, Sprite
        }
        public enum AutoSizeMode
        {
            None, NativeSize, WidthControlHeight, HeightControlWidth
        }

        [SerializeField] public string m_FileName;


        private Sprite _Sprite;
        [SerializeField] public Texture2D _Source;



        [SerializeField] public Type m_Type;
        [SerializeField] private Image.Type m_ImageType;

        [SerializeField] private Vector4 m_Border;
        [SerializeField] public AutoSizeMode m_AutoSizeMode;



        private RectTransform _RectTransform;

        private AspectRatioFitter _AspectRatioFitter;

        public void ApplyImage(Texture2D _texture)
        {
            _Source = _texture;

            if (m_Type == Type.Image)
            {
                var image = gameObject.GetComponent<Image>();

                if (image != null)
                {
                    if (Application.isPlaying)
                        Destroy(image);
                    else
                        DestroyImmediate(image);
                }

                var rawImage = gameObject.GetComponent<RawImage>();
                if (rawImage == null) rawImage = gameObject.AddComponent<RawImage>();

                rawImage.texture = _Source;
            }
            else if (m_Type == Type.Sprite)
            {

                var sprite = Sprite.Create(
                    _Source,
                    new Rect(0, 0, _Source.width, _Source.height),
                    Vector2.one * 0.5f,
                    100,
                    1,
                    SpriteMeshType.Tight,
                    m_Border, false);


                var rawImage = gameObject.GetComponent<RawImage>();

                if (rawImage != null)
                {
                    if (Application.isPlaying)
                        Destroy(rawImage);
                    else
                        DestroyImmediate(rawImage);
                }

                var image = gameObject.GetComponent<Image>();
                if (image == null) image = gameObject.AddComponent<Image>();
                image.type = m_ImageType;
                image.sprite = sprite;
            }
            UpdateLayout();
        }
        private async void Awake()
        {
            _RectTransform = GetComponent<RectTransform>();
            _AspectRatioFitter = GetComponent<AspectRatioFitter>();

            var texture = await ResourceManager.GetResourceAsync(m_FileName);
            if (texture != null && texture.m_Texture != null)
            {
                ApplyImage(texture.m_Texture);
            }
            else
            {
                gameObject.SetActive(false);
            }
           


        }

        public void UpdateLayout()
        {
            if (_AspectRatioFitter == null) _AspectRatioFitter = GetComponent<AspectRatioFitter>();
            if (_RectTransform == null) _RectTransform = GetComponent<RectTransform>();
            if (_Source == null || _AspectRatioFitter == null) return;
            _AspectRatioFitter.aspectRatio = (float)_Source.width / _Source.height;
            _AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.None;

            switch (m_AutoSizeMode)
            {

                case AutoSizeMode.NativeSize:
                    _RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _Source.width);
                    _RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _Source.height);
                    break;
                case AutoSizeMode.WidthControlHeight:
                    _AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;

                    break;
                case AutoSizeMode.HeightControlWidth:
                    _AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                    break;


            }
        }


        public void SetLayoutHorizontal()
        {
            UpdateLayout();
        }

        public void SetLayoutVertical()
        {
            UpdateLayout();

        }
    }
}

#if UNITY_EDITOR

[CustomEditor(typeof(ResourceImageLoader))]
public class ResourceImageLoaderEditor : Editor
{

    SerializedProperty _FileNameProperty;
    SerializedProperty _TypeProperty;
    SerializedProperty _ImageTypeProperty;
    SerializedProperty _BorderProperty;
    SerializedProperty _AutoSizeModeProperty;
    ResourceImageLoader _Instance;
    Texture2D _Texture;

    private string _ResourceFolder;

    private string _CurrentNameInput;

    private string[] _FilePathsFilter;

    private int _InputNameID;

    private void OnEnable()
    {
        _Instance = target as ResourceImageLoader;
        _FileNameProperty = serializedObject.FindProperty("m_FileName");
        _TypeProperty = serializedObject.FindProperty("m_Type");
        _ImageTypeProperty = serializedObject.FindProperty("m_ImageType");
        _BorderProperty = serializedObject.FindProperty("m_Border");
        _AutoSizeModeProperty = serializedObject.FindProperty("m_AutoSizeMode");

        _ResourceFolder = Path.Combine(Environment.CurrentDirectory, "Resources");

        _CurrentNameInput = string.Empty;
        _InputNameID = GUIUtility.GetControlID(FocusType.Keyboard);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.PropertyField(_FileNameProperty);

        if (EditorGUI.EndChangeCheck())
            _InputNameID = GUIUtility.keyboardControl;


        if (_FilePathsFilter != null && _FilePathsFilter.Length > 0 && GUIUtility.keyboardControl == _InputNameID)
        {
            EditorGUILayout.BeginVertical("box");

            GUI.color = Color.cyan;
            // GUI.contentColor = Color.white;
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


        EditorGUILayout.PropertyField(_TypeProperty);

        //sprite type
        if (_TypeProperty.enumValueIndex == 1)
        {

            EditorGUILayout.PropertyField(_ImageTypeProperty);
            EditorGUILayout.PropertyField(_BorderProperty);
        }

        EditorGUILayout.PropertyField(_AutoSizeModeProperty);

        EditorGUILayout.BeginHorizontal();
        GUI.color = Color.green;
        // if (GUILayout.Button("Browse"))
        // {
        //     if (!Directory.Exists(_ResourceFolder)) Directory.CreateDirectory(_ResourceFolder);
        //     string path = EditorUtility.OpenFilePanelWithFilters("Select Image", _ResourceFolder, new string[] { "Image Files", "png,jpg,jpeg" });
        //     if (!string.IsNullOrEmpty(path))
        //     {
        //         _FileNameProperty.stringValue = Path.GetFileName(path);

        //         var relativeFolder = Path.Combine("ResourcesEditor", "Editor");
        //         var assetfolder = Path.Combine(Application.dataPath, relativeFolder);
        //         if (!Directory.Exists(assetfolder)) Directory.CreateDirectory(assetfolder);
        //         var filePath = Path.Combine(assetfolder, _FileNameProperty.stringValue);
        //         var fileAssetPath = Path.Combine("Assets", relativeFolder, _FileNameProperty.stringValue);

        //         if (File.Exists(filePath))
        //         {
        //             if (!EditorUtility.DisplayDialog("File already exists", $"Resource Name : {_FileNameProperty.stringValue}", "Replace", "Cancel"))
        //             {
        //                 return;
        //             }
        //         }


        //         File.Copy(path, filePath, true);
        //         AssetDatabase.Refresh();
        //         _Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(fileAssetPath);
        //         _Instance.ApplyImage(_Texture);




        //     }
        // }

        if (GUILayout.Button("Load"))
        {

            LoadImage(_FileNameProperty.stringValue);

        }
        EditorGUILayout.EndHorizontal();
        GUI.color = Color.white;

        if (_CurrentNameInput != _FileNameProperty.stringValue || _FileNameProperty.stringValue == string.Empty)
        {
            _CurrentNameInput = _FileNameProperty.stringValue;

            Regex regexPattern = new Regex(_CurrentNameInput, RegexOptions.IgnoreCase);


            _FilePathsFilter = Directory.GetFiles(_ResourceFolder, "*.*", SearchOption.AllDirectories)
                                .Where(file => new string[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(file)) && regexPattern.IsMatch(Path.GetFileName(file)))
                                .Take(10)
                                .ToArray();



        }




        if (GUI.changed)
        {

            EditorUtility.SetDirty(target);
            serializedObject.ApplyModifiedProperties();
            _Instance.UpdateLayout();
        }
    }

    public async void LoadImage(string _filename)
    {
        //delay load image

        await Task.Delay(100);


        var relativeFolder = Path.Combine("ResourcesEditor", "Editor");
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

            //modify texture import settings
            TextureImporter textureImporter = AssetImporter.GetAtPath(fileAssetPath) as TextureImporter;
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.spriteImportMode = SpriteImportMode.Single;
            textureImporter.mipmapEnabled = false;
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.SaveAndReimport();



            AssetDatabase.Refresh();
            _Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(fileAssetPath);

          
        }

        _Instance.ApplyImage(_Texture);
    }

}



#endif
