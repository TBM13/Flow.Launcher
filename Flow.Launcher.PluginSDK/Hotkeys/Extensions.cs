using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Flow.Launcher.Infrastructure.Hotkeys;

public static class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsModifierKey(this Key key)
    {
        return key is Key.LeftAlt or Key.RightAlt
            or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
