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


    public virtual void ApplyImage(Texture2D _texture)
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

    private void OnEnable()
    {
        _ResourceFolder = Path.Combine(Environment.CurrentDirectory, "Resources");
        _CurrentNameInput = string.Empty;
        _InputNameID = GUIUtility.keyboardControl;
    }

    public override void OnInspectorGUI()
    {

        serializedObject.Update();


        var _FileNameProperty = serializedObject.FindProperty("m_FileName");

        EditorGUI.BeginChangeCheck();

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
            _Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(fileAssetPath);
        }

        var instance = target as ResourceLoaderBase;
        instance.SetEditorSource(_Texture);
        instance.ApplyImage(_Texture);
    }

}



#endif
