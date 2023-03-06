using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Modules.Utilities;
using UnityEngine;
using UniRx;
using UnityEngine.UI;
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
        private void Awake()
        {
            _RectTransform = GetComponent<RectTransform>();
            _AspectRatioFitter = GetComponent<AspectRatioFitter>();

            ResourceManager.GetResource(m_FileName, ResourceManager.ResourceResponse.ResourceType.Texture).Subscribe(_ =>
            {

                if (_ != null && _.m_Texture != null)
                {
                    _Source = _.m_Texture;

                    if (m_Type == Type.Image)
                    {
                        var image = gameObject.GetComponent<Image>();
                        if (image != null) Destroy(image);

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
                        if (rawImage != null) Destroy(rawImage);

                        var image = gameObject.GetComponent<Image>();
                        if (image == null) image = gameObject.AddComponent<Image>();
                        image.type = m_ImageType;
                        image.sprite = sprite;
                    }
                    UpdateLayout();
                }
            }).AddTo(this);


        }

        private void UpdateLayout()
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
        private void OnValidate()
        {
            UpdateLayout();
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

    private void OnEnable()
    {
        _FileNameProperty = serializedObject.FindProperty("m_FileName");
        _TypeProperty = serializedObject.FindProperty("m_Type");
        _ImageTypeProperty = serializedObject.FindProperty("m_ImageType");
        _BorderProperty = serializedObject.FindProperty("m_Border");
        _AutoSizeModeProperty = serializedObject.FindProperty("m_AutoSizeMode");

    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_FileNameProperty);
        EditorGUILayout.PropertyField(_TypeProperty);

        //sprite type
        if (_TypeProperty.enumValueIndex == 1)
        {

            EditorGUILayout.PropertyField(_ImageTypeProperty);
            EditorGUILayout.PropertyField(_BorderProperty);
        }

        EditorGUILayout.PropertyField(_AutoSizeModeProperty);


        if (GUI.changed)
        {

            EditorUtility.SetDirty(target);
            serializedObject.ApplyModifiedProperties();
        }
    }

}



#endif
