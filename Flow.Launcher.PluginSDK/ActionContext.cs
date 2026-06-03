using System.Windows;
using Flow.Launcher.PluginSDK.Hotkeys;

namespace Flow.Launcher.Infrastructure.Results;

/// <summary>
/// Context provided when invoking a <see cref="Result"/>'s action or hotkey.
/// </summary>
public record ActionContext
{
    /// <summary>
    /// Contains the keys that were pressed when the result was triggered (excluding the key(s) that triggered it).
    /// </summary>
    // TODO: Remove this, let plugins register their hotkeys
    public required IPressedKeys PressedKeys { get; init; }

    /// <summary>
    /// The screen coordinates of the result's center.
    /// <para/>
    /// Useful to show a popup, like a context menu.
    /// </summary>
    public required Point ResultPosition { get; init; }
}
