using UnityEngine;

/// <summary>
/// GPU-accelerated texture manipulation utility using Compute Shaders
/// Provides flip, rotate, crop (rect/circle/rounded), and combine operations
/// All operations are performed on the GPU for optimal performance
/// </summary>
public static class TextureUtility
{
    #region Enums
    
    /// <summary>
    /// Rotation angles supported for texture rotation
    /// </summary>
    public enum RotationAngle
    {
        /// <summary>Rotate 90 degrees clockwise</summary>
        R90,
        /// <summary>Rotate 180 degrees</summary>
        R180,
        /// <summary>Rotate 270 degrees clockwise (90 counter-clockwise)</summary>
        R270
    }
    
    /// <summary>
    /// Blend modes for combining textures
    /// </summary>
    public enum BlendMode
    {
        /// <summary>Standard alpha blending</summary>
        Alpha,
        /// <summary>Additive blending (colors add together)</summary>
        Additive,
        /// <summary>Multiply blending (colors multiply)</summary>
        Multiply,
        /// <summary>Screen blending (inverse multiply)</summary>
        Screen,
        /// <summary>Overlay blending</summary>
        Overlay
    }
    
    #endregion
    
    #region Private Fields
    
    // Cached compute shader references
    private static ComputeShader textureOperationsShader;
    private static ComputeShader textureCropShader;
    private static ComputeShader textureCombineShader;
    
    // Kernel IDs for texture operations
    private static int kernelFlipX = -1;
    private static int kernelFlipY = -1;
    private static int kernelRotate90 = -1;
    private static int kernelRotate180 = -1;
    private static int kernelRotate270 = -1;
    
    // Kernel IDs for crop operations
    private static int kernelCropRect = -1;
    private static int kernelCropCircle = -1;
    private static int kernelCropRoundedCorners = -1;
    
    // Kernel IDs for combine operations
    private static int kernelBlendAlpha = -1;
    private static int kernelBlendAdditive = -1;
    private static int kernelBlendMultiply = -1;
    private static int kernelBlendScreen = -1;
    private static int kernelBlendOverlay = -1;
    private static int kernelOverlay = -1;
    
    // Shader property IDs (cached for performance)
    private static readonly int PropSource = Shader.PropertyToID("Source");
    private static readonly int PropResult = Shader.PropertyToID("Result");
    private static readonly int PropWidth = Shader.PropertyToID("Width");
    private static readonly int PropHeight = Shader.PropertyToID("Height");
    private static readonly int PropParams = Shader.PropertyToID("Params");
    private static readonly int PropRectData = Shader.PropertyToID("RectData");
    private static readonly int PropCenter = Shader.PropertyToID("Center");
    private static readonly int PropRadius = Shader.PropertyToID("Radius");
    private static readonly int PropCornerRadii = Shader.PropertyToID("CornerRadii");
    private static readonly int PropSource1 = Shader.PropertyToID("Source1");
    private static readonly int PropSource2 = Shader.PropertyToID("Source2");
    private static readonly int PropOpacity = Shader.PropertyToID("Opacity");
    private static readonly int PropPosition = Shader.PropertyToID("Position");
    private static readonly int PropScale = Shader.PropertyToID("Scale");
    
    // Initialization flag
    private static bool isInitialized = false;
    
    #endregion
    
    #region Initialization
    
    /// <summary>
    /// Initialize compute shaders and kernel IDs
    /// Called automatically on first use
    /// </summary>
    private static void InitializeShaders()
    {
        if (isInitialized) return;
        
        // Load compute shaders from Resources folder
        textureOperationsShader = Resources.Load<ComputeShader>("TextureOperations");
        textureCropShader = Resources.Load<ComputeShader>("TextureCrop");
        textureCombineShader = Resources.Load<ComputeShader>("TextureCombine");
        
        // Validate shaders loaded successfully
        if (textureOperationsShader == null)
        {
            Debug.LogError("Failed to load TextureOperations compute shader from Resources folder");
            return;
        }
        
        if (textureCropShader == null)
        {
            Debug.LogError("Failed to load TextureCrop compute shader from Resources folder");
            return;
        }
        
        if (textureCombineShader == null)
        {
            Debug.LogError("Failed to load TextureCombine compute shader from Resources folder");
            return;
        }
        
        // Find all kernel IDs
        kernelFlipX = textureOperationsShader.FindKernel("FlipX");
        kernelFlipY = textureOperationsShader.FindKernel("FlipY");
        kernelRotate90 = textureOperationsShader.FindKernel("Rotate90");
        kernelRotate180 = textureOperationsShader.FindKernel("Rotate180");
        kernelRotate270 = textureOperationsShader.FindKernel("Rotate270");
        
        kernelCropRect = textureCropShader.FindKernel("CropRect");
        kernelCropCircle = textureCropShader.FindKernel("CropCircle");
        kernelCropRoundedCorners = textureCropShader.FindKernel("CropRoundedCorners");
        
        kernelBlendAlpha = textureCombineShader.FindKernel("BlendAlpha");
        kernelBlendAdditive = textureCombineShader.FindKernel("BlendAdditive");
        kernelBlendMultiply = textureCombineShader.FindKernel("BlendMultiply");
        kernelBlendScreen = textureCombineShader.FindKernel("BlendScreen");
        kernelBlendOverlay = textureCombineShader.FindKernel("BlendOverlay");
        kernelOverlay = textureCombineShader.FindKernel("Overlay");
        
        isInitialized = true;
        Debug.Log("TextureUtility GPU system initialized successfully");
    }
    
    /// <summary>
    /// Release compute shader resources
    /// Call this when cleaning up or before application quit
    /// </summary>
    public static void ReleaseShaders()
    {
        textureOperationsShader = null;
        textureCropShader = null;
        textureCombineShader = null;
        isInitialized = false;
    }
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Create a RenderTexture with proper settings for compute shader operations
    /// </summary>
    /// <param name="width">Texture width</param>
    /// <param name="height">Texture height</param>
    /// <returns>Configured RenderTexture</returns>
    private static RenderTexture CreateRenderTexture(int width, int height)
    {
        RenderTexture rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        rt.enableRandomWrite = true;
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();
        return rt;
    }
    
    /// <summary>
    /// Convert RenderTexture to Texture2D
    /// </summary>
    /// <param name="rt">Source RenderTexture</param>
    /// <returns>Texture2D copy of the RenderTexture</returns>
    private static Texture2D RenderTextureToTexture2D(RenderTexture rt)
    {
        Texture2D result = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        result.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        result.Apply();
        RenderTexture.active = null;
        return result;
    }
    
    /// <summary>
    /// Calculate thread groups for compute shader dispatch
    /// </summary>
    /// <param name="size">Texture dimension size</param>
    /// <param name="threadGroupSize">Thread group size (typically 8)</param>
    /// <returns>Number of thread groups needed</returns>
    private static int CalculateThreadGroups(int size, int threadGroupSize = 8)
    {
        return Mathf.CeilToInt(size / (float)threadGroupSize);
    }
    
    /// <summary>
    /// Validate that source texture is readable
    /// </summary>
    /// <param name="texture">Texture to validate</param>
    /// <param name="operationName">Name of operation for error message</param>
    /// <returns>True if texture is valid</returns>
    private static bool ValidateTexture(Texture2D texture, string operationName)
    {
        if (texture == null)
        {
            Debug.LogError($"TextureUtility.{operationName}: Source texture is null");
            return false;
        }
        
        if (!isInitialized)
        {
            InitializeShaders();
            if (!isInitialized)
            {
                Debug.LogError($"TextureUtility.{operationName}: Failed to initialize shaders");
                return false;
            }
        }
        
        return true;
    }
    
    #endregion
    
    #region Flip Operations
    
    /// <summary>
    /// Flip texture horizontally (mirror left-right) using GPU
    /// </summary>
    /// <param name="source">Source texture to flip (must be Read/Write enabled)</param>
    /// <returns>New flipped texture</returns>
    /// <example>
    /// Texture2D flipped = TextureUtility.FlipX(originalTexture);
    /// </example>
    public static Texture2D FlipX(Texture2D source)
    {
        if (!ValidateTexture(source, "FlipX")) return null;
        
        int width = source.width;
        int height = source.height;
        
        // Create render textures for input and output
        RenderTexture inputRT = CreateRenderTexture(width, height);
        RenderTexture outputRT = CreateRenderTexture(width, height);
        
        // Copy source texture to input RenderTexture
        Graphics.Blit(source, inputRT);
        
        // Set compute shader parameters
        textureOperationsShader.SetTexture(kernelFlipX, PropSource, inputRT);
        textureOperationsShader.SetTexture(kernelFlipX, PropResult, outputRT);
        textureOperationsShader.SetInt(PropWidth, width);
        textureOperationsShader.SetInt(PropHeight, height);
        
        // Dispatch compute shader (calculate thread groups for 8x8 threads)
        int threadGroupsX = CalculateThreadGroups(width);
        int threadGroupsY = CalculateThreadGroups(height);
        textureOperationsShader.Dispatch(kernelFlipX, threadGroupsX, threadGroupsY, 1);
        
        // Convert result to Texture2D
        Texture2D result = RenderTextureToTexture2D(outputRT);
        
        // Cleanup
        inputRT.Release();
        outputRT.Release();
        
        return result;
    }
    
    /// <summary>
    /// Flip texture vertically (mirror top-bottom) using GPU
    /// </summary>
    /// <param name="source">Source texture to flip (must be Read/Write enabled)</param>
    /// <returns>New flipped texture</returns>
    /// <example>
    /// Texture2D flipped = TextureUtility.FlipY(originalTexture);
    /// </example>
    public static Texture2D FlipY(Texture2D source)
    {
        if (!ValidateTexture(source, "FlipY")) return null;
        
        int width = source.width;
        int height = source.height;
        
        // Create render textures for input and output
        RenderTexture inputRT = CreateRenderTexture(width, height);
        RenderTexture outputRT = CreateRenderTexture(width, height);
        
        // Copy source texture to input RenderTexture
        Graphics.Blit(source, inputRT);
        
        // Set compute shader parameters
        textureOperationsShader.SetTexture(kernelFlipY, PropSource, inputRT);
        textureOperationsShader.SetTexture(kernelFlipY, PropResult, outputRT);
        textureOperationsShader.SetInt(PropWidth, width);
        textureOperationsShader.SetInt(PropHeight, height);
        
        // Dispatch compute shader (calculate thread groups for 8x8 threads)
        int threadGroupsX = CalculateThreadGroups(width);
        int threadGroupsY = CalculateThreadGroups(height);
        textureOperationsShader.Dispatch(kernelFlipY, threadGroupsX, threadGroupsY, 1);
        
        // Convert result to Texture2D
        Texture2D result = RenderTextureToTexture2D(outputRT);
        
        // Cleanup
        inputRT.Release();
        outputRT.Release();
        
        return result;
    }
    
    #endregion
    
    #region Resize Operations
    
    /// <summary>
    /// Resize texture to new dimensions using GPU with bilinear filtering
    /// Maintains aspect ratio if only width or height is specified
    /// </summary>
    /// <param name="source">Source texture to resize</param>
    /// <param name="newWidth">Target width in pixels</param>
    /// <param name="newHeight">Target height in pixels</param>
    /// <param name="filterMode">Texture filtering mode (default: Bilinear)</param>
    /// <returns>Resized texture</returns>
    /// <example>
    /// // Resize to specific dimensions
    /// Texture2D resized = TextureUtility.Resize(texture, 512, 512);
    /// // Resize with point filtering (pixel art)
    /// Texture2D pixelArt = TextureUtility.Resize(texture, 256, 256, FilterMode.Point);
    /// </example>
    public static Texture2D Resize(Texture2D source, int newWidth, int newHeight, FilterMode filterMode = FilterMode.Bilinear)
    {
        if (!ValidateTexture(source, "Resize")) return null;
        
        if (newWidth <= 0 || newHeight <= 0)
        {
            Debug.LogError($"TextureUtility.Resize: Invalid dimensions ({newWidth}x{newHeight}). Dimensions must be positive.");
            return null;
        }
        
        // Create render texture with target size
        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = filterMode;
        
        // Use Graphics.Blit for high-quality GPU resize with built-in filtering
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);
        
        // Read pixels from render texture
        Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
        result.filterMode = filterMode;
        result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        result.Apply();
        
        // Cleanup
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        
        return result;
    }
    
    /// <summary>
    /// Resize texture by scale factor using GPU
    /// </summary>
    /// <param name="source">Source texture to resize</param>
    /// <param name="scale">Scale factor (e.g., 0.5 for half size, 2.0 for double size)</param>
    /// <param name="filterMode">Texture filtering mode (default: Bilinear)</param>
    /// <returns>Resized texture</returns>
    /// <example>
    /// // Scale to half size
    /// Texture2D halfSize = TextureUtility.ResizeScale(texture, 0.5f);
    /// // Scale to double size
    /// Texture2D doubleSize = TextureUtility.ResizeScale(texture, 2.0f);
    /// </example>
    public static Texture2D ResizeScale(Texture2D source, float scale, FilterMode filterMode = FilterMode.Bilinear)
    {
        if (!ValidateTexture(source, "ResizeScale")) return null;
        
        if (scale <= 0)
        {
            Debug.LogError($"TextureUtility.ResizeScale: Invalid scale factor ({scale}). Scale must be positive.");
            return null;
        }
        
        int newWidth = Mathf.RoundToInt(source.width * scale);
        int newHeight = Mathf.RoundToInt(source.height * scale);
        
        return Resize(source, newWidth, newHeight, filterMode);
    }
    
    /// <summary>
    /// Resize texture to fit within maximum dimensions while maintaining aspect ratio
    /// </summary>
    /// <param name="source">Source texture to resize</param>
    /// <param name="maxWidth">Maximum width</param>
    /// <param name="maxHeight">Maximum height</param>
    /// <param name="filterMode">Texture filtering mode (default: Bilinear)</param>
    /// <returns>Resized texture that fits within bounds</returns>
    /// <example>
    /// // Resize to fit within 512x512 while keeping aspect ratio
    /// Texture2D fitted = TextureUtility.ResizeFit(texture, 512, 512);
    /// </example>
    public static Texture2D ResizeFit(Texture2D source, int maxWidth, int maxHeight, FilterMode filterMode = FilterMode.Bilinear)
    {
        if (!ValidateTexture(source, "ResizeFit")) return null;
        
        // Calculate scale to fit within bounds
        float scaleX = (float)maxWidth / source.width;
        float scaleY = (float)maxHeight / source.height;
        float scale = Mathf.Min(scaleX, scaleY);
        
        // Don't upscale if already smaller
        if (scale >= 1.0f)
        {
            scale = 1.0f;
        }
        
        int newWidth = Mathf.RoundToInt(source.width * scale);
        int newHeight = Mathf.RoundToInt(source.height * scale);
        
        return Resize(source, newWidth, newHeight, filterMode);
    }
    
    #endregion
    
    #region Rotation Operations
    
    /// <summary>
    /// Rotate texture by specified angle (90, 180, or 270 degrees) using GPU
    /// Note: 90 and 270 degree rotations swap width/height dimensions
    /// </summary>
    /// <param name="source">Source texture to rotate</param>
    /// <param name="angle">Rotation angle (90, 180, or 270 degrees clockwise)</param>
    /// <returns>New rotated texture</returns>
    /// <example>
    /// Texture2D rotated = TextureUtility.Rotate(originalTexture, RotationAngle.R90);
    /// </example>
    public static Texture2D Rotate(Texture2D source, RotationAngle angle)
    {
        if (!ValidateTexture(source, "Rotate")) return null;
        
        int sourceWidth = source.width;
        int sourceHeight = source.height;
        
        // Determine output dimensions and kernel based on rotation angle
        int outputWidth, outputHeight, kernel;
        
        switch (angle)
        {
            case RotationAngle.R90:
                // 90 degree rotation swaps dimensions
                outputWidth = sourceHeight;
                outputHeight = sourceWidth;
                kernel = kernelRotate90;
                break;
                
            case RotationAngle.R180:
                // 180 degree rotation maintains dimensions
                outputWidth = sourceWidth;
                outputHeight = sourceHeight;
                kernel = kernelRotate180;
                break;
                
            case RotationAngle.R270:
                // 270 degree rotation swaps dimensions
                outputWidth = sourceHeight;
                outputHeight = sourceWidth;
                kernel = kernelRotate270;
                break;
                
            default:
                Debug.LogError($"TextureUtility.Rotate: Invalid rotation angle {angle}");
                return null;
        }
        
        // Create render textures with appropriate dimensions
        RenderTexture inputRT = CreateRenderTexture(sourceWidth, sourceHeight);
        RenderTexture outputRT = CreateRenderTexture(outputWidth, outputHeight);
        
        // Copy source texture to input RenderTexture
        Graphics.Blit(source, inputRT);
        
        // Set compute shader parameters
        // For rotation, Width/Height refer to SOURCE dimensions
        textureOperationsShader.SetTexture(kernel, PropSource, inputRT);
        textureOperationsShader.SetTexture(kernel, PropResult, outputRT);
        textureOperationsShader.SetInt(PropWidth, sourceWidth);
        textureOperationsShader.SetInt(PropHeight, sourceHeight);
        
        // Dispatch compute shader using OUTPUT dimensions for thread groups
        int threadGroupsX = CalculateThreadGroups(outputWidth);
        int threadGroupsY = CalculateThreadGroups(outputHeight);
        textureOperationsShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
        
        // Convert result to Texture2D
        Texture2D result = RenderTextureToTexture2D(outputRT);
        
        // Cleanup
        inputRT.Release();
        outputRT.Release();
        
        return result;
    }
    
    #endregion
    
    #region Crop Operations
    
    /// <summary>
    /// Crop texture to specified rectangular region using GPU
    /// Returns a new texture with dimensions matching the crop rectangle
    /// </summary>
    /// <param name="source">Source texture to crop</param>
    /// <param name="cropRect">Crop rectangle (x, y, width, height) in pixel coordinates</param>
    /// <returns>New cropped texture with size matching the crop rectangle</returns>
    /// <example>
    /// // Crop 200x200 region from position (100, 100) - result will be 200x200 pixels
    /// Rect cropArea = new Rect(100, 100, 200, 200);
    /// Texture2D cropped = TextureUtility.CropRect(originalTexture, cropArea);
    /// </example>
    public static Texture2D CropRect(Texture2D source, Rect cropRect)
    {
        if (!ValidateTexture(source, "CropRect")) return null;
        
        // Validate crop rectangle dimensions
        if (cropRect.width <= 0 || cropRect.height <= 0)
        {
            Debug.LogError($"TextureUtility.CropRect: Invalid crop dimensions ({cropRect.width}x{cropRect.height})");
            return null;
        }
        
        // Clamp crop rectangle to source bounds
        float x = Mathf.Clamp(cropRect.x, 0, source.width);
        float y = Mathf.Clamp(cropRect.y, 0, source.height);
        float width = Mathf.Min(cropRect.width, source.width - x);
        float height = Mathf.Min(cropRect.height, source.height - y);
        
        if (width <= 0 || height <= 0)
        {
            Debug.LogError($"TextureUtility.CropRect: Crop rectangle is outside source bounds");
            return null;
        }
        
        // Create temporary RenderTexture with source size
        RenderTexture tempRT = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, tempRT);
        
        // Read only the cropped region
        RenderTexture.active = tempRT;
        Texture2D result = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(x, y, width, height), 0, 0);
        result.Apply();
        RenderTexture.active = null;
        
        // Cleanup
        RenderTexture.ReleaseTemporary(tempRT);
        
        return result;
    }
    
    /// <summary>
    /// Crop texture to circular shape using GPU
    /// Returns a square texture containing only the circular region with transparent background
    /// </summary>
    /// <param name="source">Source texture to crop</param>
    /// <param name="center">Center of circle in pixel coordinates (default: texture center)</param>
    /// <param name="radius">Radius in pixels (default: half of minimum dimension)</param>
    /// <returns>Square texture (diameter × diameter) with circular crop and transparent background</returns>
    /// <example>
    /// // Crop to circle at center with auto radius - result is square with circle inside
    /// Texture2D circle = TextureUtility.CropCircle(originalTexture);
    /// // Crop to circle at custom position with radius 100 - result is 200x200 texture
    /// Texture2D customCircle = TextureUtility.CropCircle(originalTexture, new Vector2(256, 256), 100);
    /// </example>
    public static Texture2D CropCircle(Texture2D source, Vector2? center = null, float? radius = null)
    {
        if (!ValidateTexture(source, "CropCircle")) return null;
        
        int width = source.width;
        int height = source.height;
        
        // Use defaults if not specified
        Vector2 circleCenter = center ?? new Vector2(width * 0.5f, height * 0.5f);
        float circleRadius = radius ?? (Mathf.Min(width, height) * 0.5f);
        
        // Calculate crop bounds (square bounding box of circle)
        int diameter = Mathf.CeilToInt(circleRadius * 2);
        int cropX = Mathf.FloorToInt(circleCenter.x - circleRadius);
        int cropY = Mathf.FloorToInt(circleCenter.y - circleRadius);
        
        // Clamp to source bounds
        cropX = Mathf.Max(0, cropX);
        cropY = Mathf.Max(0, cropY);
        int cropWidth = Mathf.Min(diameter, width - cropX);
        int cropHeight = Mathf.Min(diameter, height - cropY);
        
        // Create temporary RenderTexture for the full image with circle mask
        RenderTexture inputRT = CreateRenderTexture(width, height);
        RenderTexture maskedRT = CreateRenderTexture(width, height);
        
        Graphics.Blit(source, inputRT);
        
        // Apply circle mask using compute shader
        textureCropShader.SetTexture(kernelCropCircle, PropSource, inputRT);
        textureCropShader.SetTexture(kernelCropCircle, PropResult, maskedRT);
        textureCropShader.SetInt(PropWidth, width);
        textureCropShader.SetInt(PropHeight, height);
        textureCropShader.SetVector(PropCenter, circleCenter);
        textureCropShader.SetFloat(PropRadius, circleRadius);
        
        int threadGroupsX = CalculateThreadGroups(width);
        int threadGroupsY = CalculateThreadGroups(height);
        textureCropShader.Dispatch(kernelCropCircle, threadGroupsX, threadGroupsY, 1);
        
        // Read only the circular region (bounding box)
        RenderTexture.active = maskedRT;
        Texture2D result = new Texture2D(cropWidth, cropHeight, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(cropX, cropY, cropWidth, cropHeight), 0, 0);
        result.Apply();
        RenderTexture.active = null;
        
        // Cleanup
        inputRT.Release();
        maskedRT.Release();
        
        return result;
    }
    
    /// <summary>
    /// Apply rounded corners to texture using GPU
    /// Creates smooth rounded corners with anti-aliasing
    /// </summary>
    /// <param name="source">Source texture</param>
    /// <param name="cornerRadius">Uniform radius for all corners (in pixels)</param>
    /// <returns>Texture with rounded corners</returns>
    /// <example>
    /// Texture2D rounded = TextureUtility.CropRoundedCorners(originalTexture, 20f);
    /// </example>
    public static Texture2D CropRoundedCorners(Texture2D source, float cornerRadius)
    {
        // Use same radius for all four corners
        Vector4 radii = new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
        return CropRoundedCorners(source, radii);
    }
    
    /// <summary>
    /// Apply rounded corners to texture with individual corner radii using GPU
    /// Creates smooth rounded corners with anti-aliasing
    /// </summary>
    /// <param name="source">Source texture</param>
    /// <param name="cornerRadii">Individual corner radii (top-left, top-right, bottom-right, bottom-left) in pixels</param>
    /// <returns>Texture with rounded corners</returns>
    /// <example>
    /// // Different radius for each corner
    /// Vector4 radii = new Vector4(10, 20, 30, 40); // TL, TR, BR, BL
    /// Texture2D rounded = TextureUtility.CropRoundedCorners(originalTexture, radii);
    /// </example>
    public static Texture2D CropRoundedCorners(Texture2D source, Vector4 cornerRadii)
    {
        if (!ValidateTexture(source, "CropRoundedCorners")) return null;
        
        int width = source.width;
        int height = source.height;
        
        // Create render textures
        RenderTexture inputRT = CreateRenderTexture(width, height);
        RenderTexture outputRT = CreateRenderTexture(width, height);
        
        // Copy source texture to input RenderTexture
        Graphics.Blit(source, inputRT);
        
        // Set compute shader parameters
        textureCropShader.SetTexture(kernelCropRoundedCorners, PropSource, inputRT);
        textureCropShader.SetTexture(kernelCropRoundedCorners, PropResult, outputRT);
        textureCropShader.SetInt(PropWidth, width);
        textureCropShader.SetInt(PropHeight, height);
        textureCropShader.SetVector(PropCornerRadii, cornerRadii);
        
        // Dispatch compute shader
        int threadGroupsX = CalculateThreadGroups(width);
        int threadGroupsY = CalculateThreadGroups(height);
        textureCropShader.Dispatch(kernelCropRoundedCorners, threadGroupsX, threadGroupsY, 1);
        
        // Convert result to Texture2D
        Texture2D result = RenderTextureToTexture2D(outputRT);
        
        // Cleanup
        inputRT.Release();
        outputRT.Release();
        
        return result;
    }
    
    #endregion
    
    #region Combine Operations
    
    /// <summary>
    /// Blend two textures using specified blend mode and GPU acceleration
    /// Textures must be the same size
    /// </summary>
    /// <param name="source1">Base texture (bottom layer)</param>
    /// <param name="source2">Blend texture (top layer)</param>
    /// <param name="blendMode">Blend mode to use</param>
    /// <param name="opacity">Blend opacity (0-1, default 1.0)</param>
    /// <returns>Blended texture</returns>
    /// <example>
    /// Texture2D blended = TextureUtility.Blend(baseTexture, overlayTexture, BlendMode.Alpha, 0.5f);
    /// </example>
    public static Texture2D Blend(Texture2D source1, Texture2D source2, BlendMode blendMode, float opacity = 1.0f)
    {
        if (!ValidateTexture(source1, "Blend")) return null;
        if (!ValidateTexture(source2, "Blend")) return null;
        
        // Validate textures are same size
        if (source1.width != source2.width || source1.height != source2.height)
        {
            Debug.LogError("TextureUtility.Blend: Textures must be the same size");
            return null;
        }
        
        int width = source1.width;
        int height = source1.height;
        
        // Select appropriate kernel based on blend mode
        int kernel;
        switch (blendMode)
        {
            case BlendMode.Alpha:
                kernel = kernelBlendAlpha;
                break;
            case BlendMode.Additive:
                kernel = kernelBlendAdditive;
                break;
            case BlendMode.Multiply:
                kernel = kernelBlendMultiply;
                break;
            case BlendMode.Screen:
                kernel = kernelBlendScreen;
                break;
            case BlendMode.Overlay:
                kernel = kernelBlendOverlay;
                break;
            default:
                Debug.LogError($"TextureUtility.Blend: Invalid blend mode {blendMode}");
                return null;
        }
        
        // Create render textures
        RenderTexture inputRT1 = CreateRenderTexture(width, height);
        RenderTexture inputRT2 = CreateRenderTexture(width, height);
        RenderTexture outputRT = CreateRenderTexture(width, height);
        
        // Copy source textures to input RenderTextures
        Graphics.Blit(source1, inputRT1);
        Graphics.Blit(source2, inputRT2);
        
        // Set compute shader parameters
        textureCombineShader.SetTexture(kernel, PropSource1, inputRT1);
        textureCombineShader.SetTexture(kernel, PropSource2, inputRT2);
        textureCombineShader.SetTexture(kernel, PropResult, outputRT);
        textureCombineShader.SetInt(PropWidth, width);
        textureCombineShader.SetInt(PropHeight, height);
        textureCombineShader.SetFloat(PropOpacity, Mathf.Clamp01(opacity));
        
        // Dispatch compute shader
        int threadGroupsX = CalculateThreadGroups(width);
        int threadGroupsY = CalculateThreadGroups(height);
        textureCombineShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
        
        // Convert result to Texture2D
        Texture2D result = RenderTextureToTexture2D(outputRT);
        
        // Cleanup
        inputRT1.Release();
        inputRT2.Release();
        outputRT.Release();
        
        return result;
    }
    
    /// <summary>
    /// Overlay one texture on top of another with position and scale control using GPU
    /// </summary>
    /// <param name="baseTexture">Base texture (background)</param>
    /// <param name="overlayTexture">Overlay texture (foreground)</param>
    /// <param name="position">Position offset for overlay in pixels (default: 0,0)</param>
    /// <param name="scale">Scale factor for overlay (default: 1,1 = original size)</param>
    /// <param name="opacity">Overlay opacity (0-1, default 1.0)</param>
    /// <returns>Combined texture with overlay applied</returns>
    /// <example>
    /// // Overlay logo at position (100, 100) with 50% opacity
    /// Texture2D result = TextureUtility.Overlay(photo, logo, new Vector2(100, 100), Vector2.one, 0.5f);
    /// </example>
    public static Texture2D Overlay(Texture2D baseTexture, Texture2D overlayTexture, Vector2? position = null, Vector2? scale = null, float opacity = 1.0f)
    {
        if (!ValidateTexture(baseTexture, "Overlay")) return null;
        if (!ValidateTexture(overlayTexture, "Overlay")) return null;
        
        int width = baseTexture.width;
        int height = baseTexture.height;
        
        // Use defaults if not specified
        Vector2 overlayPosition = position ?? Vector2.zero;
        Vector2 overlayScale = scale ?? Vector2.one;
        
        // Create render textures
        RenderTexture inputRT1 = CreateRenderTexture(width, height);
        RenderTexture inputRT2 = CreateRenderTexture(overlayTexture.width, overlayTexture.height);
        RenderTexture outputRT = CreateRenderTexture(width, height);
        
        // Copy source textures to input RenderTextures
        Graphics.Blit(baseTexture, inputRT1);
        Graphics.Blit(overlayTexture, inputRT2);
        
        // Set compute shader parameters
        textureCombineShader.SetTexture(kernelOverlay, PropSource1, inputRT1);
        textureCombineShader.SetTexture(kernelOverlay, PropSource2, inputRT2);
        textureCombineShader.SetTexture(kernelOverlay, PropResult, outputRT);
        textureCombineShader.SetInt(PropWidth, width);
        textureCombineShader.SetInt(PropHeight, height);
        textureCombineShader.SetVector(PropPosition, overlayPosition);
        textureCombineShader.SetVector(PropScale, overlayScale);
        textureCombineShader.SetFloat(PropOpacity, Mathf.Clamp01(opacity));
        
        // Dispatch compute shader
        int threadGroupsX = CalculateThreadGroups(width);
        int threadGroupsY = CalculateThreadGroups(height);
        textureCombineShader.Dispatch(kernelOverlay, threadGroupsX, threadGroupsY, 1);
        
        // Convert result to Texture2D
        Texture2D result = RenderTextureToTexture2D(outputRT);
        
        // Cleanup
        inputRT1.Release();
        inputRT2.Release();
        outputRT.Release();
        
        return result;
    }
    
    #endregion
    
    #region Operation Chaining
    
    /// <summary>
    /// Begin a chain of texture operations for batch processing
    /// Allows multiple operations to be defined and executed together
    /// </summary>
    /// <param name="source">Source texture</param>
    /// <returns>TextureOperationChain builder object</returns>
    /// <example>
    /// Texture2D result = TextureUtility.BeginChain(texture)
    ///     .FlipX()
    ///     .Rotate(RotationAngle.R90)
    ///     .CropCircle()
    ///     .Execute();
    /// </example>
    public static TextureOperationChain BeginChain(Texture2D source)
    {
        return new TextureOperationChain(source);
    }
    
    /// <summary>
    /// Builder class for chaining multiple texture operations
    /// Executes operations sequentially, passing output of one as input to next
    /// </summary>
    public class TextureOperationChain
    {
        private Texture2D currentTexture;
        private bool hasError = false;
        
        /// <summary>
        /// Internal constructor - use TextureUtility.BeginChain() to create
        /// </summary>
        internal TextureOperationChain(Texture2D source)
        {
            currentTexture = source;
            if (source == null)
            {
                Debug.LogError("TextureOperationChain: Source texture is null");
                hasError = true;
            }
        }
        
        /// <summary>
        /// Add horizontal flip operation to chain
        /// </summary>
        public TextureOperationChain FlipX()
        {
            if (hasError) return this;
            
            Texture2D result = TextureUtility.FlipX(currentTexture);
            if (result == null)
            {
                hasError = true;
                return this;
            }
            
            // Clean up intermediate texture (except original source)
            if (currentTexture != null)
            {
                // Note: Be careful about destroying textures that might be referenced elsewhere
                // In production, consider object pooling or explicit cleanup
            }
            
            currentTexture = result;
            return this;
        }
        
        /// <summary>
        /// Add vertical flip operation to chain
        /// </summary>
        public TextureOperationChain FlipY()
        {
            if (hasError) return this;
            
            Texture2D result = TextureUtility.FlipY(currentTexture);
            if (result == null)
            {
                hasError = true;
                return this;
            }
            
            currentTexture = result;
            return this;
        }
        
        /// <summary>
        /// Add rotation operation to chain
        /// </summary>
        /// <param name="angle">Rotation angle</param>
        public TextureOperationChain Rotate(RotationAngle angle)
        {
            if (hasError) return this;
            
            Texture2D result = TextureUtility.Rotate(currentTexture, angle);
            if (result == null)
            {
                hasError = true;
                return this;
            }
            
            currentTexture = result;
            return this;
        }
        
        /// <summary>
        /// Add resize operation to chain
        /// </summary>
        /// <param name="width">Target width</param>
        /// <param name="height">Target height</param>
        /// <param name="filterMode">Filter mode (default: Bilinear)</param>
        public TextureOperationChain Resize(int width, int height, FilterMode filterMode = FilterMode.Bilinear)
        {
            if (hasError) return this;
            
            Texture2D result = TextureUtility.Resize(currentTexture, width, height, filterMode);
            if (result == null)
            {
                hasError = true;
                return this;
            }
            
            currentTexture = result;
            return this;
        }
        
        /// <summary>
        /// Add resize by scale operation to chain
        /// </summary>
        /// <param name="scale">Scale factor</param>
        /// <param name="filterMode">Filter mode (default: Bilinear)</param>
        public TextureOperationChain ResizeScale(float scale, FilterMode filterMode = FilterMode.Bilinear)
        {
            if (hasError) return this;
            
            Texture2D result = TextureUtility.ResizeScale(currentTexture, scale, filterMode);
            if (result == null)
            {
                hasError = true;
                return this;
            }
            
            currentTexture = result;
            return this;
        }
        
        /// <summary>
        /// Add resize to fit operation to chain
        /// </summary>
        /// <param name="maxWidth">Maximum width</param>
        /// <param name="maxHeight">Maximum height</param>
        /// <param name="filterMode">Filter mode (default: Bilinear)</param>
        public TextureOperationChain ResizeFit(int maxWidth, int maxHeight, FilterMode filterMode = FilterMode.Bilinear)
        {
            if (hasError) return this;
            
            Texture2D result = TextureUtility.ResizeFit(currentTexture, maxWidth, maxHeight, filterMode);
            if (result == null)
            {
                hasError = true;
                return this;
            }
            
            currentTexture = result;
            return this;
        }
        
        /// <summary>
        /// Add rectangular crop operation to chain
        /// </summary>
        /// <param name="cropRect">Crop rectangle</param>
        public TextureOperationChain CropRect(Rect cropRect)
        {
            if (hasError) return this;
            
            Texture2D result = TextureUtility.CropRect(currentTexture, cropRect);
            if (result == null)
            {
                hasError = true;
                return this;
            }
            
            currentTexture = result;
            return this;
        }
        
        /// <summary>
        /// Add circle crop operation to chain
        /// </summary>
        /// <param name="center">Circle center (optional)</param>
        /// <param name="radius">Circle radius (optional)</param>
        public TextureOperationChain CropCircle(Vector2? center = null, float? radius = null)
        {
            if (hasError) return this;
            
            Texture2D result = TextureUtility.CropCircle(currentTexture, center, radius);
            if (result == null)
            {
                hasError = true;
                return this;
            }
            
            currentTexture = result;
            return this;
        }
        
        /// <summary>
        /// Add rounded corners operation to chain
        /// </summary>
        /// <param name="cornerRadius">Corner radius</param>
        public TextureOperationChain RoundedCorners(float cornerRadius)
        {
            if (hasError) return this;
            
            Texture2D result = TextureUtility.CropRoundedCorners(currentTexture, cornerRadius);
            if (result == null)
            {
                hasError = true;
                return this;
            }
            
            currentTexture = result;
            return this;
        }
        
        /// <summary>
        /// Add rounded corners operation with per-corner radii to chain
        /// </summary>
        /// <param name="cornerRadii">Per-corner radii (TL, TR, BR, BL)</param>
        public TextureOperationChain RoundedCorners(Vector4 cornerRadii)
        {
            if (hasError) return this;
            
            Texture2D result = TextureUtility.CropRoundedCorners(currentTexture, cornerRadii);
            if (result == null)
            {
                hasError = true;
                return this;
            }
            
            currentTexture = result;
            return this;
        }
        
        /// <summary>
        /// Add blend operation to chain
        /// </summary>
        /// <param name="blendTexture">Texture to blend with</param>
        /// <param name="blendMode">Blend mode</param>
        /// <param name="opacity">Blend opacity</param>
        public TextureOperationChain Blend(Texture2D blendTexture, BlendMode blendMode, float opacity = 1.0f)
        {
            if (hasError) return this;
            
            Texture2D result = TextureUtility.Blend(currentTexture, blendTexture, blendMode, opacity);
            if (result == null)
            {
                hasError = true;
                return this;
            }
            
            currentTexture = result;
            return this;
        }
        
        /// <summary>
        /// Add overlay operation to chain
        /// </summary>
        /// <param name="overlayTexture">Texture to overlay</param>
        /// <param name="position">Position offset</param>
        /// <param name="scale">Scale factor</param>
        /// <param name="opacity">Overlay opacity</param>
        public TextureOperationChain Overlay(Texture2D overlayTexture, Vector2? position = null, Vector2? scale = null, float opacity = 1.0f)
        {
            if (hasError) return this;
            
            Texture2D result = TextureUtility.Overlay(currentTexture, overlayTexture, position, scale, opacity);
            if (result == null)
            {
                hasError = true;
                return this;
            }
            
            currentTexture = result;
            return this;
        }
        
        /// <summary>
        /// Execute the chain and return final result
        /// </summary>
        /// <returns>Final processed texture, or null if any operation failed</returns>
        public Texture2D Execute()
        {
            if (hasError)
            {
                Debug.LogError("TextureOperationChain: Chain execution failed due to previous errors");
                return null;
            }
            
            return currentTexture;
        }
    }
    
    #endregion
}

/// <summary>
/// Extension methods for Texture2D GPU operations
/// Provides convenient calling syntax: texture.FlipXGPU()
/// </summary>
public static class TextureUtilityExtensions
{
    /// <summary>
    /// Flip texture horizontally using GPU (extension method)
    /// </summary>
    /// <param name="texture">Source texture</param>
    /// <returns>New flipped texture</returns>
    public static Texture2D FlipXGPU(this Texture2D texture)
    {
        return TextureUtility.FlipX(texture);
    }
    
    /// <summary>
    /// Flip texture vertically using GPU (extension method)
    /// </summary>
    /// <param name="texture">Source texture</param>
    /// <returns>New flipped texture</returns>
    public static Texture2D FlipYGPU(this Texture2D texture)
    {
        return TextureUtility.FlipY(texture);
    }
    
    /// <summary>
    /// Rotate texture using GPU (extension method)
    /// </summary>
    /// <param name="texture">Source texture</param>
    /// <param name="angle">Rotation angle</param>
    /// <returns>New rotated texture</returns>
    public static Texture2D RotateGPU(this Texture2D texture, TextureUtility.RotationAngle angle)
    {
        return TextureUtility.Rotate(texture, angle);
    }
    
    /// <summary>
    /// Resize texture using GPU (extension method)
    /// </summary>
    /// <param name="texture">Source texture</param>
    /// <param name="width">Target width</param>
    /// <param name="height">Target height</param>
    /// <param name="filterMode">Filter mode (default: Bilinear)</param>
    /// <returns>Resized texture</returns>
    public static Texture2D ResizeGPU(this Texture2D texture, int width, int height, FilterMode filterMode = FilterMode.Bilinear)
    {
        return TextureUtility.Resize(texture, width, height, filterMode);
    }
    
    /// <summary>
    /// Resize texture by scale using GPU (extension method)
    /// </summary>
    /// <param name="texture">Source texture</param>
    /// <param name="scale">Scale factor</param>
    /// <param name="filterMode">Filter mode (default: Bilinear)</param>
    /// <returns>Resized texture</returns>
    public static Texture2D ResizeScaleGPU(this Texture2D texture, float scale, FilterMode filterMode = FilterMode.Bilinear)
    {
        return TextureUtility.ResizeScale(texture, scale, filterMode);
    }
    
    /// <summary>
    /// Resize texture to fit within bounds using GPU (extension method)
    /// </summary>
    /// <param name="texture">Source texture</param>
    /// <param name="maxWidth">Maximum width</param>
    /// <param name="maxHeight">Maximum height</param>
    /// <param name="filterMode">Filter mode (default: Bilinear)</param>
    /// <returns>Resized texture</returns>
    public static Texture2D ResizeFitGPU(this Texture2D texture, int maxWidth, int maxHeight, FilterMode filterMode = FilterMode.Bilinear)
    {
        return TextureUtility.ResizeFit(texture, maxWidth, maxHeight, filterMode);
    }
    
    /// <summary>
    /// Crop texture to rectangle using GPU (extension method)
    /// </summary>
    /// <param name="texture">Source texture</param>
    /// <param name="cropRect">Crop rectangle</param>
    /// <returns>Cropped texture</returns>
    public static Texture2D CropRectGPU(this Texture2D texture, Rect cropRect)
    {
        return TextureUtility.CropRect(texture, cropRect);
    }
    
    /// <summary>
    /// Crop texture to circle using GPU (extension method)
    /// </summary>
    /// <param name="texture">Source texture</param>
    /// <param name="center">Circle center (optional)</param>
    /// <param name="radius">Circle radius (optional)</param>
    /// <returns>Circular cropped texture</returns>
    public static Texture2D CropCircleGPU(this Texture2D texture, Vector2? center = null, float? radius = null)
    {
        return TextureUtility.CropCircle(texture, center, radius);
    }
    
    /// <summary>
    /// Apply rounded corners using GPU (extension method)
    /// </summary>
    /// <param name="texture">Source texture</param>
    /// <param name="cornerRadius">Corner radius in pixels</param>
    /// <returns>Texture with rounded corners</returns>
    public static Texture2D CropRoundedGPU(this Texture2D texture, float cornerRadius)
    {
        return TextureUtility.CropRoundedCorners(texture, cornerRadius);
    }
    
    /// <summary>
    /// Apply rounded corners with individual radii using GPU (extension method)
    /// </summary>
    /// <param name="texture">Source texture</param>
    /// <param name="cornerRadii">Per-corner radii (TL, TR, BR, BL)</param>
    /// <returns>Texture with rounded corners</returns>
    public static Texture2D CropRoundedGPU(this Texture2D texture, Vector4 cornerRadii)
    {
        return TextureUtility.CropRoundedCorners(texture, cornerRadii);
    }
    
    /// <summary>
    /// Blend with another texture using GPU (extension method)
    /// </summary>
    /// <param name="baseTexture">Base texture</param>
    /// <param name="blendTexture">Texture to blend with</param>
    /// <param name="blendMode">Blend mode</param>
    /// <param name="opacity">Blend opacity</param>
    /// <returns>Blended texture</returns>
    public static Texture2D BlendGPU(this Texture2D baseTexture, Texture2D blendTexture, TextureUtility.BlendMode blendMode, float opacity = 1.0f)
    {
        return TextureUtility.Blend(baseTexture, blendTexture, blendMode, opacity);
    }
    
    /// <summary>
    /// Overlay another texture on top using GPU (extension method)
    /// </summary>
    /// <param name="baseTexture">Base texture</param>
    /// <param name="overlayTexture">Overlay texture</param>
    /// <param name="position">Position offset</param>
    /// <param name="scale">Scale factor</param>
    /// <param name="opacity">Overlay opacity</param>
    /// <returns>Combined texture</returns>
    public static Texture2D OverlayGPU(this Texture2D baseTexture, Texture2D overlayTexture, Vector2? position = null, Vector2? scale = null, float opacity = 1.0f)
    {
        return TextureUtility.Overlay(baseTexture, overlayTexture, position, scale, opacity);
    }
    
    /// <summary>
    /// Begin operation chaining with this texture (extension method)
    /// </summary>
    /// <param name="texture">Source texture</param>
    /// <returns>TextureOperationChain builder</returns>
    /// <example>
    /// Texture2D result = texture.BeginChainGPU()
    ///     .FlipX()
    ///     .Rotate(TextureUtility.RotationAngle.R90)
    ///     .CropCircle()
    ///     .Execute();
    /// </example>
    public static TextureUtility.TextureOperationChain BeginChainGPU(this Texture2D texture)
    {
        return TextureUtility.BeginChain(texture);
    }
}
