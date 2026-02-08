using System.Windows.Input;

namespace Flow.Launcher.Infrastructure.Hotkey;

/// <summary>
/// Contains the press state of certain special keys.
/// </summary>
public readonly record struct SpecialKeyState
{
    public required bool CtrlPressed { get; init; }
    public required bool ShiftPressed { get; init; }
    public required bool AltPressed { get; init; }
    public required bool WinPressed { get; init; }

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
