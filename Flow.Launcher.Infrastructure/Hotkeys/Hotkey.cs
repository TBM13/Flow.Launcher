using System;
using System.Windows.Input;

namespace Flow.Launcher.Infrastructure.Hotkeys;

public readonly record struct Hotkey
{
    /// <summary>
    /// The modifier(s) that need to be held down for the hotkey to be triggered.
    /// <para/>
    /// Set to <see cref="ModifierKeys.None"/> if pressing <see cref="MainKey"/> alone triggers the hotkey.
    /// </summary>
    public required ModifierKeys Modifiers { get; init; }
    /// <summary>
    /// The main key that needs to be pressed for the hotkey to be triggered.
    /// E.g. K in Ctrl+Alt+K.
    /// <para/>
    /// Set to <see cref="Key.None"/> if just pressing the modifier(s) triggers the hotkey.
    /// </summary>
    public required Key MainKey { get; init; }

    /// <summary>
    /// When true, the hotkey will only trigger when all the keys are held down
    /// for a little more than half a second before being released.<br/>
    /// This allows two different hotkeys to be registered with the same key(s).
    /// <para/>
    /// This only has an effect on hotkeys registered in <see cref="GlobalHotkeyManager"/>.
    /// </summary>
    public bool LongPress { get; init; }

    /// <summary>
    /// Whether this hotkey is valid.
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (Modifiers == ModifierKeys.None && MainKey == Key.None)
                return false;
            if (MainKey.IsModifierKey())
                return false;

            return true;
        }
    }

    public static Hotkey FromString(string str)
    {
        bool longPress = str.StartsWith("[LongPress]", StringComparison.OrdinalIgnoreCase);
        if (longPress)
            str = str[11..];

        ModifierKeys modifiers = ModifierKeys.None;
        Key mainKey = Key.None;

        string[] keys = str.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];

            if (key.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("LCtrl", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("LeftCtrl", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("RCtrl", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("RightCtrl", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Control", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("LControl", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("LeftControl", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("RControl", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("RightControl", StringComparison.OrdinalIgnoreCase))
                modifiers |= ModifierKeys.Control;
            else if (key.Equals("Alt", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("LAlt", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("LeftAlt", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("RAlt", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("RightAlt", StringComparison.OrdinalIgnoreCase))
                modifiers |= ModifierKeys.Alt;
            else if (key.Equals("Shift", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("LShift", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("LeftShift", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("RShift", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("RightShift", StringComparison.OrdinalIgnoreCase))
                modifiers |= ModifierKeys.Shift;
            else if (key.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("LWin", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("LeftWin", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("RWin", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("RightWin", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("LWindows", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("LeftWindows", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("RWindows", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("RightWindows", StringComparison.OrdinalIgnoreCase))
                modifiers |= ModifierKeys.Windows;
            else
            {
                if (i != keys.Length - 1)
                    throw new ArgumentException($"Key '{key}' is not a valid modifier key", nameof(str));
                if (!Enum.TryParse(key, true, out mainKey))
                    throw new ArgumentException($"Key '{key}' is not a valid key", nameof(str));
            }
        }

        return new Hotkey
        {
            Modifiers = modifiers,
            MainKey = mainKey,
            LongPress = longPress
        };
    }

    public override string ToString() => ToString(includeLongPress: true);
    public string ToString(bool includeLongPress)
    {
        if (Modifiers == ModifierKeys.None && MainKey == Key.None)
            return string.Empty;

        string res = LongPress && includeLongPress ? "[LongPress]" : string.Empty;
        if (Modifiers != ModifierKeys.None)
            res += Modifiers.ToString().Replace(", ", "+");

        if (MainKey != Key.None)
        {
            if (res.Length > 0)
                res += "+";
            res += MainKey.ToString();
        }

        return res;
    }
}
