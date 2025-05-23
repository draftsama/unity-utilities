
namespace Modules.Utilities
{
    public enum PathType
    {
        StreamAssets,
        Relative,
        Absolute
    }

    public enum ContentSizeMode
    {
        None, NativeSize, WidthControlHeight, HeightControlWidth
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

#if PACKAGE_ADDRESSABLES_INSTALLED
        Addressable,
#endif
        

    }

}