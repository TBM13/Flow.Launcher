using Flow.Launcher.PluginSDK.Hotkeys;

namespace Flow.Launcher.PluginSDK.Plugins.Interfaces;

/// <summary>
/// Implemented by plugins that use hotkeys.
/// </summary>
public interface IHotkeysProvider
{
    /// <summary>
    /// Generates all the hotkeys used by this plugin.
    /// </summary>
    /// <remarks>
    /// If a hotkey conflicts with another one, the user will be informed and the hotkey disabled.
    /// </remarks>
    public IEnumerable<HotkeyInfo> RegisterHotkeys();
}
