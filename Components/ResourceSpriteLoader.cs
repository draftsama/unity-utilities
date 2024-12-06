using System.Collections;
using System.Collections.Generic;
using Modules.Utilities;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif



[RequireComponent(typeof(SpriteRenderer))]
public class ResourceSpriteLoader : ResourceLoaderBase
{

    private SpriteRenderer _spriteRenderer;

    [SerializeField]private float pixelPerUnit = 100;
    async void Start()
    {
        if (string.IsNullOrEmpty(m_FileName)) return;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        var texture = await ResourceManager.GetTextureAsync(m_FileName);
        ApplyTexture(texture);
    }

    public override void ApplyTexture(Texture2D _texture)
    {

        _texture.wrapMode = m_TextureWrapMode;
        _texture.filterMode = m_FilterMode;
        _texture.Apply();


        
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();

     
        var sprite = Sprite.Create(_texture, new Rect(0, 0, _texture.width, _texture.height), new Vector2(0.5f, 0.5f), pixelPerUnit);

        sprite.name = _texture.name;


        _spriteRenderer.sprite = sprite;
    }




}

#if UNITY_EDITOR
[CustomEditor(typeof(ResourceSpriteLoader))]
public class ResourceSpriteLoaderEditor : ResourceLoaderBaseEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
       
       //pixelPerUnit
        serializedObject.Update();

        var instance = target as ResourceSpriteLoader;
        var spriteRenderer = serializedObject.FindProperty("_spriteRenderer");

        EditorGUILayout.PropertyField(serializedObject.FindProperty("pixelPerUnit"));

        serializedObject.ApplyModifiedProperties();

    }
}
#endif
