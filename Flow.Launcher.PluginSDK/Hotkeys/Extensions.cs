using System.Windows.Input;

namespace Flow.Launcher.Infrastructure.Hotkeys;

public static class Extensions
{
    public static bool IsModifierKey(this Key key)
    {
        return key == Key.LeftAlt || key == Key.RightAlt
            || key == Key.LeftCtrl || key == Key.RightCtrl
            || key == Key.LeftShift || key == Key.RightShift
            || key == Key.LWin || key == Key.RWin;
    }

    public static bool ToModifierKey(this Key key, out ModifierKeys modifierKey)
    {
        modifierKey = key switch
        {
            Key.LeftAlt or Key.RightAlt => ModifierKeys.Alt,
            Key.LeftCtrl or Key.RightCtrl => ModifierKeys.Control,
            Key.LeftShift or Key.RightShift => ModifierKeys.Shift,
            Key.LWin or Key.RWin => ModifierKeys.Windows,
            _ => ModifierKeys.None
        };

        return modifierKey != ModifierKeys.None;
    }
}
