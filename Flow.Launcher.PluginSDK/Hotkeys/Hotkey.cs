using System.Collections.Immutable;
using System.Windows.Input;

namespace Flow.Launcher.PluginSDK.Hotkeys;

public readonly record struct Hotkey
{
    private const string LongPressPrefix = "[LongPress]";

    /// <summary>
    /// The modifier(s) that need to be held down for the hotkey to be triggered.
    /// </summary>
    /// <remarks>Global hotkeys must contain at least one modifier.</remarks>
    public ModifierKeys Modifiers { get; init; }
    /// <summary>
    /// The main key that needs to be pressed for the hotkey to be triggered.
    /// E.g. K in Ctrl+Alt+K.
    /// </summary>
    /// <remarks>
    /// When set to <see cref="Key.None"/>, pressing the modifier(s) is enough to trigger the hotkey.
    /// </remarks>
    public Key MainKey { get; init; }

    /// <summary>
    /// When true, the hotkey will only trigger when all the keys are held down
    /// for a small amount of time before being released.
    /// <para/>
    /// This allows two different hotkeys to be registered with the same key(s).
    /// </summary>
    /// <remarks>This only has an effect on global hotkeys.</remarks>
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
            if (!Enum.IsDefined(MainKey))
                return false;
            if (MainKey.IsModifierKey())
                return false;

            return true;
        }
    }

    public Hotkey(Key mainKey = Key.None, ModifierKeys modifiers = ModifierKeys.None, bool longPress = false)
    {
        MainKey = mainKey;
        Modifiers = modifiers;
        LongPress = longPress;
    }

    /// <summary>
    /// Tries to create a <see cref="Hotkey"/> instance from a string representation.
    /// </summary>
    /// <returns>True if the hotkey is valid.</returns>
    public static bool TryParse(ReadOnlySpan<char> str, out Hotkey hotkey)
    {
        str = str.Trim();

        bool longPress = str.StartsWith(LongPressPrefix, StringComparison.OrdinalIgnoreCase);
        if (longPress)
            str = str[LongPressPrefix.Length..];

        ModifierKeys modifiers = ModifierKeys.None;
        Key mainKey = Key.None;

        int separatorIndex = str.IndexOf('+');
        while (separatorIndex != -1)
        {
            ReadOnlySpan<char> mod = str[..separatorIndex].Trim();
            ModifierKeys modKey = StringToModifier(mod);
            if (modKey == ModifierKeys.None)
            {
                // Invalid modifier
                hotkey = default;
                return false;
            }

            modifiers |= modKey;
            str = str[(separatorIndex + 1)..];
            separatorIndex = str.IndexOf('+');
        }

        str = str.Trim();
        if (str.Length > 0)
        {
            ModifierKeys modKey = StringToModifier(str);
            if (modKey == ModifierKeys.None && !Enum.TryParse(str, ignoreCase: true, out mainKey))
            {
                // Not a valid modifier nor main key
                hotkey = default;
                return false;
            }

            modifiers |= modKey;
        }

        hotkey = new Hotkey()
        {
            Modifiers = modifiers,
            MainKey = mainKey,
            LongPress = longPress
        };

        return hotkey.IsValid;
    }

    private static ModifierKeys StringToModifier(ReadOnlySpan<char> str)
    {
        if (str.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("LCtrl", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("LeftCtrl", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("RCtrl", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("RightCtrl", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("Control", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("LControl", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("LeftControl", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("RControl", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("RightControl", StringComparison.OrdinalIgnoreCase))
            return ModifierKeys.Control;
        else if (str.Equals("Alt", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("LAlt", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("LeftAlt", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("RAlt", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("RightAlt", StringComparison.OrdinalIgnoreCase))
            return ModifierKeys.Alt;
        else if (str.Equals("Shift", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("LShift", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("LeftShift", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("RShift", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("RightShift", StringComparison.OrdinalIgnoreCase))
            return ModifierKeys.Shift;
        else if (str.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("LWin", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("LeftWin", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("RWin", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("RightWin", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("LWindows", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("LeftWindows", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("RWindows", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("RightWindows", StringComparison.OrdinalIgnoreCase))
            return ModifierKeys.Windows;

        return ModifierKeys.None;
    }

    /// <inheritdoc cref="ToString(bool)"/>
    public override string ToString() => ToString(includeLongPress: true);
    /// <summary>
    /// Converts the hotkey to a string representation.
    /// </summary>
    /// <param name="includeLongPress">Whether to include a LongPress prefix when <see cref="LongPress"/> is true.</param>
    /// <returns>An empty string if the hotkey is not valid.</returns>
    public string ToString(bool includeLongPress)
    {
        if (!IsValid)
            return string.Empty;

        string res = LongPress && includeLongPress ? LongPressPrefix : string.Empty;
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
