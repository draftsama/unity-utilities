using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Example and test script for TextureUtility GPU operations
/// Demonstrates all available texture manipulation features
/// Attach this to a GameObject and assign UI elements to see results
/// </summary>
public class TextureUtilityExample : MonoBehaviour
{
    [Header("Test Texture")]
    [SerializeField] private Texture2D sourceTexture;
    
    [Header("UI Display")]
    [SerializeField] private RawImage originalDisplay;
    [SerializeField] private RawImage resultDisplay;
    [SerializeField] private Text statusText;
    
    [Header("Test Operations")]
    [SerializeField] private bool testFlipX = false;
    [SerializeField] private bool testFlipY = false;
    [SerializeField] private bool testRotate90 = false;
    [SerializeField] private bool testRotate180 = false;
    [SerializeField] private bool testRotate270 = false;
    [SerializeField] private bool testResize = false;
    [SerializeField] private bool testResizeScale = false;
    [SerializeField] private bool testResizeFit = false;
    [SerializeField] private bool testCropCircle = false;
    [SerializeField] private bool testCropRounded = false;
    [SerializeField] private bool testChainOperations = false;
    
    [Header("Resize Parameters")]
    [SerializeField] private int targetWidth = 512;
    [SerializeField] private int targetHeight = 512;
    [SerializeField] private float resizeScale = 0.5f;
    [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;
    
    [Header("Crop Parameters")]
    [SerializeField] private float cornerRadius = 20f;
    
    [Header("Blend Test")]
    [SerializeField] private Texture2D overlayTexture;
    [SerializeField] private bool testBlend = false;
    [SerializeField] private TextureUtility.BlendMode blendMode = TextureUtility.BlendMode.Alpha;
    [SerializeField] private float blendOpacity = 0.5f;
    
    private Texture2D lastResult;
    
    void Start()
    {
        // Display original texture
        if (originalDisplay != null && sourceTexture != null)
        {
            originalDisplay.texture = sourceTexture;
        }
        
        UpdateStatus("Ready. Press Space to run tests.");
    }
    
    void Update()
    {
        // Press Space to run tests
        if (Input.GetKeyDown(KeyCode.Space))
        {
            RunTests();
        }
        
        // Press C to clear result
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearResult();
        }
    }
    
    /// <summary>
    /// Run all enabled tests
    /// </summary>
    public void RunTests()
    {
        if (sourceTexture == null)
        {
            UpdateStatus("Error: No source texture assigned!");
            Debug.LogError("TextureUtilityExample: Please assign a source texture");
            return;
        }
        
        UpdateStatus("Running GPU texture operations...");
        
        Texture2D result = null;
        float startTime = Time.realtimeSinceStartup;
        
        try
        {
            // Test individual operations
            if (testFlipX)
            {
                result = TestFlipX();
            }
            else if (testFlipY)
            {
                result = TestFlipY();
            }
            else if (testRotate90)
            {
                result = TestRotate90();
            }
            else if (testRotate180)
            {
                result = TestRotate180();
            }
            else if (testRotate270)
            {
                result = TestRotate270();
            }
            else if (testResize)
            {
                result = TestResize();
            }
            else if (testResizeScale)
            {
                result = TestResizeScale();
            }
            else if (testResizeFit)
            {
                result = TestResizeFit();
            }
            else if (testCropCircle)
            {
                result = TestCropCircle();
            }
            else if (testCropRounded)
            {
                result = TestCropRounded();
            }
            else if (testBlend)
            {
                result = TestBlend();
            }
            else if (testChainOperations)
            {
                result = TestChainOperations();
            }
            else
            {
                // Default: run a demo chain operation
                result = DemoChainOperation();
            }
            
            // Calculate execution time
            float elapsedTime = (Time.realtimeSinceStartup - startTime) * 1000f; // Convert to ms
            
            // Display result
            if (result != null)
            {
                DisplayResult(result);
                UpdateStatus($"Success! GPU operation completed in {elapsedTime:F2}ms");
                lastResult = result;
            }
            else
            {
                UpdateStatus("Error: Operation failed. Check console for details.");
            }
        }
        catch (System.Exception e)
        {
            UpdateStatus($"Error: {e.Message}");
            Debug.LogError($"TextureUtilityExample error: {e}");
        }
    }
    
    #region Individual Test Methods
    
    private Texture2D TestFlipX()
    {
        Debug.Log("Testing FlipX operation...");
        return TextureUtility.FlipX(sourceTexture);
    }
    
    private Texture2D TestFlipY()
    {
        Debug.Log("Testing FlipY operation...");
        return TextureUtility.FlipY(sourceTexture);
    }
    
    private Texture2D TestRotate90()
    {
        Debug.Log("Testing Rotate 90° operation...");
        return TextureUtility.Rotate(sourceTexture, TextureUtility.RotationAngle.R90);
    }
    
    private Texture2D TestRotate180()
    {
        Debug.Log("Testing Rotate 180° operation...");
        return TextureUtility.Rotate(sourceTexture, TextureUtility.RotationAngle.R180);
    }
    
    private Texture2D TestRotate270()
    {
        Debug.Log("Testing Rotate 270° operation...");
        return TextureUtility.Rotate(sourceTexture, TextureUtility.RotationAngle.R270);
    }
    
    private Texture2D TestResize()
    {
        Debug.Log($"Testing Resize operation ({targetWidth}x{targetHeight}, filter: {filterMode})...");
        return TextureUtility.Resize(sourceTexture, targetWidth, targetHeight, filterMode);
    }
    
    private Texture2D TestResizeScale()
    {
        Debug.Log($"Testing ResizeScale operation (scale: {resizeScale}, filter: {filterMode})...");
        return TextureUtility.ResizeScale(sourceTexture, resizeScale, filterMode);
    }
    
    private Texture2D TestResizeFit()
    {
        Debug.Log($"Testing ResizeFit operation (max: {targetWidth}x{targetHeight}, filter: {filterMode})...");
        return TextureUtility.ResizeFit(sourceTexture, targetWidth, targetHeight, filterMode);
    }
    
    private Texture2D TestCropCircle()
    {
        Debug.Log("Testing CropCircle operation...");
        return TextureUtility.CropCircle(sourceTexture);
    }
    
    private Texture2D TestCropRounded()
    {
        Debug.Log($"Testing CropRoundedCorners operation (radius: {cornerRadius})...");
        return TextureUtility.CropRoundedCorners(sourceTexture, cornerRadius);
    }
    
    private Texture2D TestBlend()
    {
        if (overlayTexture == null)
        {
            Debug.LogError("TestBlend: No overlay texture assigned!");
            return null;
        }
        
        Debug.Log($"Testing Blend operation (mode: {blendMode}, opacity: {blendOpacity})...");
        return TextureUtility.Blend(sourceTexture, overlayTexture, blendMode, blendOpacity);
    }
    
    private Texture2D TestChainOperations()
    {
        Debug.Log("Testing chained operations...");
        return TextureUtility.BeginChain(sourceTexture)
            .FlipX()
            .Rotate(TextureUtility.RotationAngle.R90)
            .CropCircle()
            .Execute();
    }
    
    /// <summary>
    /// Demo showcasing multiple operations in a chain
    /// </summary>
    private Texture2D DemoChainOperation()
    {
        Debug.Log("Running demo: FlipX -> CropCircle -> RoundedCorners");
        
        // Example: Create a flipped, circular avatar with subtle rounding
        return TextureUtility.BeginChain(sourceTexture)
            .FlipX()
            .CropCircle()
            .Execute();
    }
    
    #endregion
    
    #region Helper Methods
    
    private void DisplayResult(Texture2D texture)
    {
        if (resultDisplay != null)
        {
            resultDisplay.texture = texture;
        }
    }
    
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"TextureUtilityExample: {message}");
    }
    
    private void ClearResult()
    {
        if (resultDisplay != null)
        {
            resultDisplay.texture = null;
        }
        
        if (lastResult != null)
        {
            Destroy(lastResult);
            lastResult = null;
        }
        
        UpdateStatus("Result cleared.");
    }
    
    #endregion
    
    void OnDestroy()
    {
        // Clean up created textures
        if (lastResult != null)
        {
            Destroy(lastResult);
        }
        
        // Release compute shader resources
        TextureUtility.ReleaseShaders();
    }
}
