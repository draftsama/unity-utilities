namespace Modules.Utilities
{
    /// <summary>
    /// Implement on a component inside an on/off-screen view prefab to bind it to its indicator.
    /// Views are pooled, so reset any per-target state in OnUnbind.
    /// </summary>
    public interface IHUDIndicatorViewHandler
    {
        void OnBind(HUDIndicator indicator, HUDIndicatorView view);
        void OnUnbind(HUDIndicator indicator, HUDIndicatorView view);
    }
}
