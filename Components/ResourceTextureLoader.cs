using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Modules.Utilities;
using System.Linq;


#if UNITY_EDITOR
using UnityEditor;
#endif
public class ResourceTextureLoader : ResourceLoaderBase
{
    public enum ModelAspectRatio
    {
        None, WidthControlHeight, HeightControlWidth
    }

    [SerializeField][HideInInspector] private int _CurrentMaterialIndex = 0;
    [SerializeField][HideInInspector] private int _CurrentTexturePropertyIndex = 0;
    [SerializeField] private ModelAspectRatio m_ModelAspectRatio = ModelAspectRatio.None;

    async void Start()
    {

        if (string.IsNullOrEmpty(m_FileName)) return;

        var texture = await ResourceManager.GetTextureAsync(m_FileName);

        ApplyTexture(texture);

    }
    private void OnDisable()
    {
#if UNITY_EDITOR

        if (_EditorSource != null)
        {
            ApplyTexture(_EditorSource);
        }
#endif
    }

    public override void ApplyTexture(Texture2D _texture)
    {
        var renderers = GetComponentsInChildren<Renderer>();

        if (_texture == null) return;

        _texture.wrapMode = m_TextureWrapMode;
        _texture.filterMode = m_FilterMode;
        _texture.Apply();

       //  Debug.Log($"Apply Texture : {_texture.width}x{_texture.height}");
        if (m_ModelAspectRatio == ModelAspectRatio.WidthControlHeight)
        {
            var aspectRatio = (float)_texture.height / (float)_texture.width;
            var size = transform.localScale;
            size.y = size.x * aspectRatio;
            transform.localScale = size;
        }
        else if (m_ModelAspectRatio == ModelAspectRatio.HeightControlWidth)
        {
            var aspectRatio = (float)_texture.width / (float)_texture.height;
            // Debug.Log($"aspectRatio : {aspectRatio}");
            var size = transform.localScale;
            size.x = size.y * aspectRatio;
            transform.localScale = size;
        }


        var materials = renderers.SelectMany(x => x.sharedMaterials).ToList();

        if (materials.Count > 0)
        {
#if UNITY_2022_1_OR_NEWER

            var propName = materials[_CurrentMaterialIndex].GetPropertyNames(MaterialPropertyType.Texture)
                .ElementAtOrDefault(_CurrentTexturePropertyIndex);
#else
            var propName = materials[_CurrentMaterialIndex].GetTexturePropertyNames()
                .ElementAtOrDefault(_CurrentTexturePropertyIndex);
#endif
         materials[_CurrentMaterialIndex].SetTexture(propName, _texture);
        }

    }


}




#if UNITY_EDITOR
[CustomEditor(typeof(ResourceTextureLoader))]
public class ResourceTextureLoaderEditor : ResourceLoaderBaseEditor
{


    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        serializedObject.Update();

        var instance = target as ResourceTextureLoader;
        var currentMaterialIndex = serializedObject.FindProperty("_CurrentMaterialIndex");
        var currentTexturePropertyIndex = serializedObject.FindProperty("_CurrentTexturePropertyIndex");
        var modelAspectRatio = serializedObject.FindProperty("m_ModelAspectRatio");
        //helper message

        EditorGUILayout.HelpBox("the Material can not share with other object", MessageType.Info);
        EditorGUILayout.PropertyField(modelAspectRatio);

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

        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
        }
        serializedObject.ApplyModifiedProperties();
    }
}

#endif
