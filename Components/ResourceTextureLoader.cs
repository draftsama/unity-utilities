using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Modules.Utilities;
using Unity.Mathematics;


#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
#endif
public class ResourceTextureLoader : ResourceLoaderBase
{

    [SerializeField][HideInInspector] private int _CurrentMaterialIndex = 0;
    [SerializeField][HideInInspector] private int _CurrentTexturePropertyIndex = 0;

    async void Start()
    {

        if (string.IsNullOrEmpty(m_FileName)) return;

        var texture = await ResourceManager.GetTextureAsync(m_FileName);
        
        ApplyImage(texture);

    }
    private void OnDisable()
    {
#if UNITY_EDITOR

        if (_EditorSource != null)
        {
            ApplyImage(_EditorSource);
        }
#endif
    }

    public override void ApplyImage(Texture2D _texture)
    {
        var renderers = GetComponentsInChildren<Renderer>();

        if(_texture == null)return;

        _texture.wrapMode = m_TextureWrapMode;
        _texture.alphaIsTransparency = m_AlphaIsTransparency;
        _texture.filterMode = m_FilterMode;
        _texture.Apply();



        var materials = renderers.SelectMany(x => x.sharedMaterials).ToList();

        if (materials.Count > 0)
        {
            var propName = materials[_CurrentMaterialIndex].GetPropertyNames(MaterialPropertyType.Texture)
                .ElementAtOrDefault(_CurrentTexturePropertyIndex);

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

            var propName = materials[index].GetPropertyNames(MaterialPropertyType.Texture);

            if (propName.Length > 0)
            {
                texturePropertyIndex = Mathf.Clamp(texturePropertyIndex, 0, materials[index].GetPropertyNames(MaterialPropertyType.Texture).Length - 1);

                texturePropertyIndex = EditorGUILayout.Popup("Texture Property", texturePropertyIndex, propName.ToArray());
                currentTexturePropertyIndex.intValue = texturePropertyIndex;
            }


            currentMaterialIndex.intValue = index;
            GUI.contentColor = Color.yellow;
            GUI.backgroundColor = Color.gray;
            if (GUILayout.Button("Clear Texture"))
            {
                instance.ApplyImage(null);
            }
            if (GUILayout.Button("Clear All Texture"))
            {
                var propNames = materials[index].GetPropertyNames(MaterialPropertyType.Texture);
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

        if(GUI.changed){
            EditorUtility.SetDirty(target);
        }
        serializedObject.ApplyModifiedProperties();
    }
}

#endif
