using System.Collections;
using UnityEngine;
using Modules.Utilities;
using System.Linq;
using UnityEngine.UI;

public class ResourceTextureLoader : MonoBehaviour
{

    [SerializeField] public string m_FileName;
    [SerializeField] public OutputType m_OutputType = OutputType.None;

    [SerializeField] public Texture2D m_EditorSource;

    [SerializeField] public int m_TextureTypeValue = 0;
    [SerializeField] public bool m_AlphaIsTransparency = false;
    [SerializeField] public bool m_GenerateMipMaps = false;
    [SerializeField] public TextureWrapMode m_TextureWrapMode = TextureWrapMode.Clamp;
    [SerializeField] public FilterMode m_FilterMode = FilterMode.Bilinear;
    [SerializeField] public int m_MaxTextureSize = 2048;
    [SerializeField] public int m_TextureCompression = 0; // 0 = Uncompressed
    [SerializeField] public int m_NPOTScale = 0; // 0 = None (keep original size)
    [SerializeField][HideInInspector] public int _CurrentMaterialIndex = 0;
    [SerializeField][HideInInspector] public int _CurrentTexturePropertyIndex = 0;
    [SerializeField] public ContentSizeMode m_ContentSizeMode = ContentSizeMode.None;


    [SerializeField] public RawImage m_RawImage;
    [SerializeField] public Image m_Image;
    [SerializeField] public AspectRatioFitter m_AspectRatioFitter;
    [SerializeField] public SpriteRenderer m_SpriteRenderer;

    private RectTransform _RectTransform;



    public void SetEditorSource(Texture2D _texture)
    {
        m_EditorSource = _texture;
    }

    async void Start()
    {

        if (string.IsNullOrEmpty(m_FileName)) return;
        _RectTransform = GetComponent<RectTransform>();
        var texture = await ResourceManager.GetTextureAsync(m_FileName);

        ApplyTexture(texture);

    }
    private void OnDisable()
    {
#if UNITY_EDITOR

        if (m_EditorSource != null)
        {
            ApplyTexture(m_EditorSource);
        }
#endif
    }
    public void ApplyTextureToMesh(Texture2D _texture)
    {
       var renderers = GetComponentsInChildren<Renderer>();

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


    public void ApplyTexture(Texture2D _texture)
    {
        if (_texture == null) return;

        _texture.wrapMode = m_TextureWrapMode;
        _texture.filterMode = m_FilterMode;
        _texture.Apply();



        switch (m_OutputType)
        {
            case OutputType.Image:
                if (m_Image == null)
                {
                    m_Image = gameObject.AddComponent<Image>();
                }
                m_Image.sprite = Sprite.Create(_texture, new Rect(0, 0, _texture.width, _texture.height), Vector2.zero);
                ApplyCanvasSize(_texture);
                break;
            case OutputType.RawImage:
                if (m_RawImage == null)
                {
                    m_RawImage = gameObject.AddComponent<RawImage>();
                }
                m_RawImage.texture = _texture;
                ApplyCanvasSize(_texture);
                break;
            case OutputType.SpriteRenderer:
                if (m_SpriteRenderer == null)
                {
                    m_SpriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
                m_SpriteRenderer.sprite = Sprite.Create(_texture, new Rect(0, 0, _texture.width, _texture.height), Vector2.zero);
                break;
            case OutputType.Material:
                ApplyTextureToMesh(_texture);
                break;
        }



    }

    void ApplyCanvasSize(Texture2D _texture)
    {

        if (_RectTransform == null)
        {
            _RectTransform = GetComponent<RectTransform>();
        }

        if (m_AspectRatioFitter == null)
            return;

        var ratio = (float)_texture.width / (float)_texture.height;
        switch (m_ContentSizeMode)
        {

            case ContentSizeMode.NativeSize:
                m_AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.None;

                _RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _texture.width);
                _RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _texture.height);
                break;
            case ContentSizeMode.WidthControlHeight:

                m_AspectRatioFitter.aspectRatio = ratio;
                m_AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;

                break;
            case ContentSizeMode.HeightControlWidth:
                m_AspectRatioFitter.aspectRatio = ratio;
                m_AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                break;
            case ContentSizeMode.None:

                m_AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.None;
                break;

        }
    }


}




