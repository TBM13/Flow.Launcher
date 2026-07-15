using System.Windows.Input;

namespace Flow.Launcher.PluginSDK.Hotkeys;

/// <summary>
/// Represents the keys that are pressed (down) at a specific moment in time.
/// </summary>
public interface IPressedKeys
{
    /// <summary>
    /// The total number of pressed keys (including modifiers).
    /// </summary>
    public int PressedCount { get; }
    /// <summary>
    /// The total number of pressed modifiers.
    /// </summary>
    public int PressedModifiersCount { get; }

    /// <summary>
    /// Whether any Alt key was pressed.
    /// </summary>
    bool AltPressed { get; }
    /// <summary>
    /// Whether any Ctrl key was pressed.
    /// </summary>
    bool CtrlPressed { get; }
    /// <summary>
    /// Whether any Shift key was pressed.
    /// </summary>
    bool ShiftPressed { get; }
    /// <summary>
    /// Whether any Windows key was pressed.
    /// </summary>
    bool WindowsPressed { get; }

    /// <summary>
    /// Returns true if the key was pressed.
    /// </summary>
    bool IsKeyPressed(Key key);
    /// <summary>
    /// Returns true if the given modifiers were pressed.
    /// </summary>
    bool IsModifierPressed(ModifierKeys modifiers);

    /// <summary>
    /// Returns true if the given key was the only pressed key.
    /// </summary>
    bool OnlyKeyPressed(Key key);
    /// <summary>
    /// Returns true if the given modifiers were the only pressed keys.
    /// </summary>
    bool OnlyModifiersPressed(ModifierKeys modifiers);

    /// <summary>
    /// Tries to generate a hotkey from the pressed keys.
    /// </summary>
    /// <remarks>
    /// The generated hotkey might be invalid.
    /// </remarks>
    Hotkey ToHotkey();
}
