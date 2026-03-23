# TextureUtility

GPU-accelerated texture manipulation for Unity using Compute Shaders. All operations run entirely on the GPU, making them suitable for runtime use on large textures without stalling the main thread.

## Requirements

- Unity 6 or later
- GPU with Compute Shader support:
  - Windows: DirectX 11 / 12
  - macOS / iOS: Metal
  - Android: Vulkan or OpenGL ES 3.1+
  - Linux: Vulkan / OpenGL
  - WebGL: WebGL 2.0 with compute shader support
- Source `Texture2D` must have **Read/Write enabled** in import settings (except `ToTexture2D`)

## Files

| File | Description |
|---|---|
| `TextureUtility.cs` | Main static utility class and `TextureOperationChain` builder |
| `Resources/TextureOperations.compute` | FlipX, FlipY, Rotate90/180/270 kernels |
| `Resources/TextureCrop.compute` | CropRect, CropCircle, CropRoundedCorners kernels |
| `Resources/TextureCombine.compute` | BlendAlpha/Additive/Multiply/Screen/Overlay, Overlay kernels |

---

## API Reference

### Flip

```csharp
Texture2D TextureUtility.FlipX(Texture2D source)
Texture2D TextureUtility.FlipY(Texture2D source)
```

Mirrors the texture horizontally (left–right) or vertically (top–bottom). Output dimensions are identical to the source.

```csharp
Texture2D mirrored = TextureUtility.FlipX(texture);
Texture2D flipped   = TextureUtility.FlipY(texture);
```

---

### Resize

```csharp
Texture2D TextureUtility.Resize(Texture2D source, int newWidth, int newHeight, FilterMode filterMode = FilterMode.Bilinear)
Texture2D TextureUtility.ResizeScale(Texture2D source, float scale, FilterMode filterMode = FilterMode.Bilinear)
Texture2D TextureUtility.ResizeFit(Texture2D source, int maxWidth, int maxHeight, FilterMode filterMode = FilterMode.Bilinear)
```

| Method | Description |
|---|---|
| `Resize` | Resize to exact pixel dimensions |
| `ResizeScale` | Resize by a scale multiplier (`0.5` = half, `2.0` = double) |
| `ResizeFit` | Fit within maximum bounds while preserving aspect ratio; never upscales |

Supported filter modes: `Bilinear` (default, smooth), `Point` (pixel-perfect), `Trilinear`.

```csharp
Texture2D thumb     = TextureUtility.Resize(texture, 128, 128);
Texture2D half      = TextureUtility.ResizeScale(texture, 0.5f);
Texture2D fitted    = TextureUtility.ResizeFit(texture, 512, 512);
Texture2D pixelArt  = TextureUtility.Resize(texture, 64, 64, FilterMode.Point);
```

---

### Rotate

```csharp
Texture2D TextureUtility.Rotate(Texture2D source, TextureUtility.RotationAngle angle)
```

Rotates clockwise by 90°, 180°, or 270°. R90 and R270 swap width and height; R180 preserves dimensions.

```csharp
Texture2D r90  = TextureUtility.Rotate(texture, TextureUtility.RotationAngle.R90);
Texture2D r180 = TextureUtility.Rotate(texture, TextureUtility.RotationAngle.R180);
Texture2D r270 = TextureUtility.Rotate(texture, TextureUtility.RotationAngle.R270);
```

---

### Crop

#### Rectangular crop

```csharp
Texture2D TextureUtility.CropRect(Texture2D source, Rect cropRect)
```

Returns a new texture whose dimensions match `cropRect`. Coordinates are in pixels; the rect is clamped to source bounds automatically.

```csharp
Texture2D cropped = TextureUtility.CropRect(texture, new Rect(100, 100, 200, 200));
```

#### Circular crop

```csharp
Texture2D TextureUtility.CropCircle(Texture2D source, Vector2? center = null, float? radius = null)
```

Masks the texture to a circle with smooth anti-aliased edges. Returns a square texture (diameter × diameter) with a transparent background. Defaults to the texture centre and half of the minimum dimension.

```csharp
Texture2D avatar       = TextureUtility.CropCircle(texture);
Texture2D customCircle = TextureUtility.CropCircle(texture, new Vector2(256, 256), 100f);
```

#### Rounded corners

```csharp
Texture2D TextureUtility.CropRoundedCorners(Texture2D source, float cornerRadius)
Texture2D TextureUtility.CropRoundedCorners(Texture2D source, Vector4 cornerRadii)
```

Applies a rounded-corner mask using a Signed Distance Field (SDF) with 1-pixel anti-aliasing. The `Vector4` overload sets each corner independently: `(top-left, top-right, bottom-right, bottom-left)`.

```csharp
Texture2D rounded = TextureUtility.CropRoundedCorners(texture, 20f);

// Per-corner radii
Vector4 radii     = new Vector4(0f, 20f, 20f, 0f); // only right corners
Texture2D partial = TextureUtility.CropRoundedCorners(texture, radii);
```

---

### Blend

```csharp
Texture2D TextureUtility.Blend(Texture2D source1, Texture2D source2, TextureUtility.BlendMode blendMode, float opacity = 1.0f)
```

Blends two same-sized textures. `source1` is the base (bottom layer); `source2` is blended on top.

| Blend Mode | Description |
|---|---|
| `Alpha` | Standard alpha compositing |
| `Additive` | Colors add together — result is brighter |
| `Multiply` | Colors multiply — result is darker; black stays black |
| `Screen` | Inverse multiply — result is brighter; white stays white |
| `Overlay` | Multiply on dark areas, Screen on bright areas |

```csharp
Texture2D blended = TextureUtility.Blend(base, overlay, TextureUtility.BlendMode.Alpha, 0.75f);
```

---

### Overlay (position + scale)

```csharp
Texture2D TextureUtility.Overlay(Texture2D baseTexture, Texture2D overlayTexture,
    Vector2? position = null, Vector2? scale = null, float opacity = 1.0f)
```

Composites `overlayTexture` on top of `baseTexture` with pixel-level position offset and scale control. Output uses the base texture's dimensions.

```csharp
// Place a logo at (50, 50) at half size with 80% opacity
Texture2D result = TextureUtility.Overlay(photo, logo,
    position: new Vector2(50, 50),
    scale:    new Vector2(0.5f, 0.5f),
    opacity:  0.8f);
```

---

### Convert to Texture2D

```csharp
Texture2D TextureUtility.ToTexture2D(Texture texture)
```

Converts any `Texture` (including `RenderTexture`) to a `Texture2D` via GPU blit. Does **not** require Read/Write to be enabled on the source.

```csharp
Texture2D snapshot = TextureUtility.ToTexture2D(renderTexture);
Texture2D copy     = TextureUtility.ToTexture2D(material.mainTexture);
```

---

### Operation Chaining

Chain multiple operations fluently. Each step passes its output as the input to the next. If any step returns `null`, the chain short-circuits and `Execute()` returns `null`.

```csharp
Texture2D result = TextureUtility.BeginChain(texture)
    .FlipX()
    .Rotate(TextureUtility.RotationAngle.R90)
    .Resize(256, 256)
    .CropCircle()
    .Execute();

// With blend step
Texture2D composed = TextureUtility.BeginChain(photo)
    .ResizeFit(512, 512)
    .CropRoundedCorners(24f)
    .Overlay(logo, position: new Vector2(10, 10), opacity: 0.9f)
    .Execute();
```

Available chain methods: `FlipX`, `FlipY`, `Rotate`, `Resize`, `ResizeScale`, `ResizeFit`, `CropRect`, `CropCircle`, `RoundedCorners`, `Blend`, `Overlay`, `Execute`.

---

### Extension Methods

Every operation is available as an extension method on `Texture2D` for concise call-site syntax:

| Extension | Calls |
|---|---|
| `texture.FlipXGPU()` | `TextureUtility.FlipX` |
| `texture.FlipYGPU()` | `TextureUtility.FlipY` |
| `texture.RotateGPU(angle)` | `TextureUtility.Rotate` |
| `texture.ResizeGPU(w, h)` | `TextureUtility.Resize` |
| `texture.ResizeScaleGPU(scale)` | `TextureUtility.ResizeScale` |
| `texture.ResizeFitGPU(maxW, maxH)` | `TextureUtility.ResizeFit` |
| `texture.CropRectGPU(rect)` | `TextureUtility.CropRect` |
| `texture.CropCircleGPU()` | `TextureUtility.CropCircle` |
| `texture.CropRoundedGPU(radius)` | `TextureUtility.CropRoundedCorners` |
| `texture.BlendGPU(other, mode)` | `TextureUtility.Blend` |
| `texture.OverlayGPU(other)` | `TextureUtility.Overlay` |
| `texture.BeginChainGPU()` | `TextureUtility.BeginChain` |
| `anyTexture.ToTexture2D()` | `TextureUtility.ToTexture2D` |

```csharp
Texture2D result = texture
    .FlipYGPU()
    .CropRoundedGPU(16f);
```

---

### Resource Management

Shaders are loaded lazily on first use and cached for the lifetime of the application. Call `TextureUtility.ReleaseShaders()` when the utility is no longer needed (e.g., before scene unload or application quit) to release the cached references.

```csharp
void OnDestroy()
{
    TextureUtility.ReleaseShaders();
}
```

---

## Color Space Behaviour

TextureUtility is **color-space-correct on all platforms**. Intermediate compute RTs use `RenderTextureReadWrite.Linear` and the final readback blits through an explicit `sRGB` RT before `ReadPixels`, ensuring identical brightness on Windows (DirectX), macOS (Metal), and Android (Vulkan / OpenGL ES 3.1+).

---

## Performance Notes

- Thread group size is **8×8**, optimised for 2D texture workloads.
- GPU operations are **10–50× faster** than CPU equivalents for textures ≥ 256×256.
- Each static method allocates one or two temporary `RenderTexture` objects and one `Texture2D`; all temporaries are released before returning.
- For tight loops, consider reusing the output `Texture2D` (via `LoadRawTextureData`) rather than calling the utility every frame.
