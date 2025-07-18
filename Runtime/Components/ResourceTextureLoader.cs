using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Modules.Utilities;
using System.Linq;
using UnityEngine.UI;



#if UNITY_EDITOR
using UnityEditor;
#endif
public class ResourceTextureLoader : ResourceLoaderBase
{


    [SerializeField][HideInInspector] private int _CurrentMaterialIndex = 0;
    [SerializeField][HideInInspector] private int _CurrentTexturePropertyIndex = 0;
    [SerializeField] private ContentSizeMode m_ContentSizeMode = ContentSizeMode.None;


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
    public void ApplyTextureToMesh(Renderer[] _renderers, Texture2D _texture)
    {

        //  Debug.Log($"Apply Texture : {_texture.width}x{_texture.height}");
        if (m_ContentSizeMode == ContentSizeMode.WidthControlHeight)
        {
            var aspectRatio = (float)_texture.height / (float)_texture.width;
            var size = transform.localScale;
            size.y = size.x * aspectRatio;
            transform.localScale = size;
        }
        else if (m_ContentSizeMode == ContentSizeMode.HeightControlWidth)
        {
            var aspectRatio = (float)_texture.width / (float)_texture.height;
            // Debug.Log($"aspectRatio : {aspectRatio}");
            var size = transform.localScale;
            size.x = size.y * aspectRatio;
            transform.localScale = size;
        }


        var materials = _renderers.SelectMany(x => x.sharedMaterials).ToList();

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

    public void ApplyTextureToCanvas(RawImage _rawImage, Image _image, Texture2D _texture)
    {
        if (_rawImage != null)
        {
            _rawImage.texture = _texture;



        }
        if (_image != null)
        {
            _image.sprite = Sprite.Create(_texture, new Rect(0, 0, _texture.width, _texture.height), Vector2.zero);

        }

        var rectTransform = GetComponent<RectTransform>();
        var aspectRatioFitter = GetComponent<AspectRatioFitter>();

        var ratio = (float)_texture.width / (float)_texture.height;
        switch (m_ContentSizeMode)
        {

            case ContentSizeMode.NativeSize:
                if (aspectRatioFitter != null)
                    Destroy(aspectRatioFitter);
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _texture.width);
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _texture.height);
                break;
            case ContentSizeMode.WidthControlHeight:
                if (aspectRatioFitter == null)
                    aspectRatioFitter = gameObject.AddComponent<AspectRatioFitter>();
                aspectRatioFitter.aspectRatio = ratio;
                aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;

                break;
            case ContentSizeMode.HeightControlWidth:
                if (aspectRatioFitter == null)
                    aspectRatioFitter = gameObject.AddComponent<AspectRatioFitter>();
                aspectRatioFitter.aspectRatio = ratio;
                aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                break;
            case ContentSizeMode.None:
                if (aspectRatioFitter != null)
                    Destroy(aspectRatioFitter);
                aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.None;
                break;

        }
    }


    public override void ApplyTexture(Texture2D _texture)
    {
        if (_texture == null) return;

        _texture.wrapMode = m_TextureWrapMode;
        _texture.filterMode = m_FilterMode;
        _texture.Apply();


        var renderers = GetComponentsInChildren<Renderer>();
        var image = GetComponent<Image>();
        var rawImage = GetComponent<RawImage>();




        if (renderers.Length > 0)
        {
            ApplyTextureToMesh(renderers, _texture);

            return;
        }

        if (rawImage != null || image != null)
        {
            ApplyTextureToCanvas(rawImage, image, _texture);
            return;
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

        var aspectRatio = serializedObject.FindProperty("m_ContentSizeMode");
        //helper message

        EditorGUILayout.PropertyField(aspectRatio);


        if (_ResourceContentType == ResourceContentType.Mesh)
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

        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
        }
        serializedObject.ApplyModifiedProperties();
    }
}

#endif
