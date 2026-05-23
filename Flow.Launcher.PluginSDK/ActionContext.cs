using System.Windows;
using Flow.Launcher.Infrastructure.Hotkeys;

namespace Flow.Launcher.Infrastructure.Results;

/// <summary>
/// Context provided as a parameter when invoking a
/// <see cref="Result.Action"/> or <see cref="Result.AsyncAction"/>
/// </summary>
public record ActionContext
{
    /// <summary>
    /// Contains the keys that were pressed when the result was triggered (excluding the key(s) that triggered it).
    /// </summary>
    public required PressedKeys PressedKeys { get; init; }

    /// <summary>
    /// The screen coordinates of the result's center.
    /// <para/>
    /// Useful to show a popup, like a context menu.
    /// </summary>
    public required Point ResultPosition { get; init; }
}
