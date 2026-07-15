namespace Flow.Launcher.PluginSDK.Plugins.Interfaces;

/// <summary>
/// Implement this interface if you need to manually save stuff.
/// </summary>
public interface ISavable
{
    /// <summary>
    /// Called by Flow Launcher when appropiate.
    /// </summary>
    bool TrySave();
}
