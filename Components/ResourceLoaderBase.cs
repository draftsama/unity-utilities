using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class ResourceLoaderBase : MonoBehaviour
{
    [SerializeField] protected string m_FileName;

    [SerializeField] protected Texture2D _EditorSource;

    [SerializeField] protected int m_TextureTypeValue = 0;
    [SerializeField] protected bool m_AlphaIsTransparency = false;
    [SerializeField] protected bool m_GenerateMipMaps = false;
    [SerializeField] protected TextureWrapMode m_TextureWrapMode = TextureWrapMode.Clamp;
    [SerializeField] protected FilterMode m_FilterMode = FilterMode.Bilinear;
    public virtual void ApplyTexture(Texture2D _texture)
    {

    }
    public void SetEditorSource(Texture2D _texture)
    {
        _EditorSource = _texture;
    }




}


#if UNITY_EDITOR
[CustomEditor(typeof(ResourceLoaderBase), true)]
public class ResourceLoaderBaseEditor : Editor
{

    protected int _InputNameID;
    protected string _ResourceFolder;

    protected string _CurrentNameInput;

    protected string[] _FilePathsFilter;

    protected Texture2D _Texture;

    protected SerializedProperty _TextureTypeValueProperty;
    protected SerializedProperty _AlphaIsTransparency;

    protected SerializedProperty _TextureWrapMode;

    protected SerializedProperty _GenerateMipMaps;

    protected SerializedProperty _FilterMode;
    protected SerializedProperty _FileNameProperty;
    TextureImporterType _TextureType;
    private void OnEnable()
    {
        _ResourceFolder = Path.Combine(Environment.CurrentDirectory, "Resources");
        _CurrentNameInput = string.Empty;
        _InputNameID = GUIUtility.keyboardControl;


        _TextureTypeValueProperty = serializedObject.FindProperty("m_TextureTypeValue");
        _AlphaIsTransparency = serializedObject.FindProperty("m_AlphaIsTransparency");
        _TextureWrapMode = serializedObject.FindProperty("m_TextureWrapMode");
        _GenerateMipMaps = serializedObject.FindProperty("m_GenerateMipMaps");
        _FilterMode = serializedObject.FindProperty("m_FilterMode");

       _FileNameProperty= serializedObject.FindProperty("m_FileName");

        var relativeFolder = Path.Combine("ResourcesEditor", "Editor");
        var fileAssetPath = Path.Combine("Assets", relativeFolder, _FileNameProperty.stringValue);


        if (File.Exists(fileAssetPath))
        {
            _Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(fileAssetPath);
        }

    }

    public override void OnInspectorGUI()
    {

        serializedObject.Update();



        EditorGUI.BeginChangeCheck();

        //display Texture2D
       
        if (_Texture != null)
        {
            GUILayout.Label("Preview", GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var aspectRatio = (float)_Texture.height / (float)_Texture.width;
            GUILayout.Box(_Texture, GUILayout.Width(240), GUILayout.Height(aspectRatio * 240f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
        }


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

       

        _TextureType = (TextureImporterType)_TextureTypeValueProperty.intValue;
        _TextureType = (TextureImporterType)EditorGUILayout.EnumPopup("Texture Type", _TextureType);
        _TextureTypeValueProperty.intValue = (int)_TextureType;

        EditorGUILayout.PropertyField(_AlphaIsTransparency);
        EditorGUILayout.PropertyField(_GenerateMipMaps);
        EditorGUILayout.PropertyField(_TextureWrapMode);
        EditorGUILayout.PropertyField(_FilterMode);

        GUI.color = Color.green;
        if (GUILayout.Button("Load"))
        {
            LoadImage(_FileNameProperty.stringValue);
        }
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
        }
        serializedObject.ApplyModifiedProperties();

        GUI.color = Color.white;

    }

    public async void LoadImage(string _filename)
    {
        //delay load image
        if (string.IsNullOrEmpty(_filename))
            return;
        Debug.Log($"load image: {_filename}");

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

            //modify texture import setting
            var importer = AssetImporter.GetAtPath(fileAssetPath) as TextureImporter;
            importer.textureType = (TextureImporterType)_TextureTypeValueProperty.intValue;
            importer.mipmapEnabled = _GenerateMipMaps.boolValue;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = _AlphaIsTransparency.boolValue;
            importer.wrapMode = (TextureWrapMode)_TextureWrapMode.intValue;
            importer.filterMode = (FilterMode)_FilterMode.intValue;
            importer.isReadable = true;
            importer.SaveAndReimport();



            _Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(fileAssetPath);
        }

        var instance = target as ResourceLoaderBase;
        instance.SetEditorSource(_Texture);
        instance.ApplyTexture(_Texture);
    }

}



#endif
