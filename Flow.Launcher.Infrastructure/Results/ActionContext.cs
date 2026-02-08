using System.Windows;
using Flow.Launcher.Infrastructure.Hotkey;

namespace Flow.Launcher.Infrastructure.Results;

/// <summary>
/// Context provided as a parameter when invoking a
/// <see cref="Result.Action"/> or <see cref="Result.AsyncAction"/>
/// </summary>
public record ActionContext
{
    /// <summary>
    /// Contains the press state of certain special keys.
    /// </summary>
    public required SpecialKeyState SpecialKeyState { get; init; }

    /// <summary>
    /// The screen coordinates of the result's center.
    /// <para/>
    /// Useful to show a popup, like a context menu.
    /// </summary>
    public required Point ResultPosition { get; init; }
}
