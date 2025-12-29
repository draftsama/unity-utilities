using System.Collections;
using UnityEngine;
using Modules.Utilities;
using System.Linq;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.IO;

public class ResourceTextureLoader : MonoBehaviour
{
    public enum SpritePivot
    {
        Center,
        TopLeft,
        Top,
        TopRight,
        BottomLeft,
        Bottom,
        BottomRight,
        Left,
        Right
    }

    [SerializeField] public string m_FileName;
    [SerializeField] public OutputType m_OutputType = OutputType.None;

    [SerializeField] public Texture2D m_EditorSource;

    [SerializeField] public TextureWrapMode m_TextureWrapMode = TextureWrapMode.Clamp;
    [SerializeField] public FilterMode m_FilterMode = FilterMode.Bilinear;
    [SerializeField][HideInInspector] public int _CurrentMaterialIndex = 0;
    [SerializeField][HideInInspector] public int _CurrentTexturePropertyIndex = 0;
    [SerializeField] public ContentSizeMode m_ContentSizeMode = ContentSizeMode.None;

    [SerializeField] public bool m_SpriteFitScreen = false;

    [SerializeField] public float m_PixelPerUnit = 100;
    [SerializeField] public SpritePivot m_SpritePivot = SpritePivot.Center;

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

        await LoadTexture();
    }



    public async UniTask<Texture2D> LoadTexture()
    {

        Texture2D texture = null;
        if (Application.isPlaying)
        {
            texture = await ResourceManager.GetTextureAsync(m_FileName);

        }
        else
        {
            var path = ResourceManager.GetPathByNameAsync(m_FileName);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"File not found : {m_FileName}");
                return null;
            }
            
            Debug.Log($"Load Texture : {path}");

            // Convert file path to proper URI format
            var fileUri = new System.Uri(path).AbsoluteUri;
            using (var reqTexture = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(fileUri))
            {
                await reqTexture.SendWebRequest();
                
                if (reqTexture.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to load texture: {reqTexture.error}");
                    return null;
                }
                
                texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(reqTexture);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.name = Path.GetFileName(m_FileName);
                texture.Apply();
            }
        }

        ApplyTexture(texture);
        return texture;
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
                m_Image.sprite = Sprite.Create(_texture, new Rect(0, 0, _texture.width, _texture.height), GetPivotVector(m_SpritePivot), m_PixelPerUnit);
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

                if (m_SpriteFitScreen)
                {
                    float cameraHeight = Camera.main.orthographicSize * 2;
                    float idealPixelPerUnit = _texture.height / cameraHeight;
                    m_PixelPerUnit = idealPixelPerUnit;
                }

                m_SpriteRenderer.sprite = Sprite.Create(_texture, new Rect(0, 0, _texture.width, _texture.height), GetPivotVector(m_SpritePivot), m_PixelPerUnit);
                break;
            case OutputType.Material:
                ApplyTextureToMesh(_texture);
                break;
        }



    }

    Vector2 GetPivotVector(SpritePivot pivot)
    {
        return pivot switch
        {
            SpritePivot.Center => new Vector2(0.5f, 0.5f),
            SpritePivot.TopLeft => new Vector2(0, 1),
            SpritePivot.Top => new Vector2(0.5f, 1),
            SpritePivot.TopRight => new Vector2(1, 1),
            SpritePivot.BottomLeft => new Vector2(0, 0),
            SpritePivot.Bottom => new Vector2(0.5f, 0),
            SpritePivot.BottomRight => new Vector2(1, 0),
            SpritePivot.Left => new Vector2(0, 0.5f),
            SpritePivot.Right => new Vector2(1, 0.5f),
            _ => new Vector2(0.5f, 0.5f)
        };
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




