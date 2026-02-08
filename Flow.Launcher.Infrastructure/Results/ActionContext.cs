using System.Windows;
using System.Windows.Input;

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

/// <summary>
/// Contains the press state of certain special keys.
/// </summary>
public readonly record struct SpecialKeyState
{
    /// <summary>
    /// True if the Ctrl key is pressed.
    /// </summary>
    public bool CtrlPressed { get; init; }

    /// <summary>
    /// True if the Shift key is pressed.
    /// </summary>
    public bool ShiftPressed { get; init; }

    /// <summary>
    /// True if the Alt key is pressed.
    /// </summary>
    public bool AltPressed { get; init; }

    /// <summary>
    /// True if the Windows key is pressed.
    /// </summary>
    public bool WinPressed { get; init; }

    /// <summary>
    /// Get this object represented as a <see cref="ModifierKeys"/> flag combination.
    /// </summary>
    /// <returns></returns>
    public ModifierKeys ToModifierKeys()
    {
        return (CtrlPressed ? ModifierKeys.Control : ModifierKeys.None) |
               (ShiftPressed ? ModifierKeys.Shift : ModifierKeys.None) |
               (AltPressed ? ModifierKeys.Alt : ModifierKeys.None) |
               (WinPressed ? ModifierKeys.Windows : ModifierKeys.None);
    }
}
