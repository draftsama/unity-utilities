
namespace Modules.Utilities
{
    public enum PathType
    {
        StreamingAssets,
        Relative,
        Absolute,
        ExternalResources
    }

    public enum ContentSizeMode
    {
        None, NativeSize, WidthControlHeight, HeightControlWidth
    }

    public enum OutputType
    {
       None,Image, RawImage, SpriteRenderer, Material
    }

    public enum VideoOutputType
    {
        RawImage,
        Renderer,
    }

    //TODO : Add more video output types as needed
    public enum PlaneVideoControl
    {
        None,
        Self,
        Screen,

    }

    public enum VideoStartMode
    {
        None,
        Prepare,
        FirstFrameReady,
        AutoPlay,
    }

    public enum ResourceStoreType
    {
        ExternalResources,
        Addressable,


    }

}