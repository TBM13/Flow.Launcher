using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Flow.Launcher.Infrastructure.Hotkey;

public static class GlobalHotkey
{
    public static SpecialKeyState CheckModifiers()
    {
        SpecialKeyState state = new SpecialKeyState()
        {
            ShiftPressed = (PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_SHIFT) & 0x8000) != 0,
            CtrlPressed = (PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_CONTROL) & 0x8000) != 0,
            AltPressed = (PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_MENU) & 0x8000) != 0,
            WinPressed = ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_LWIN) & 0x8000) != 0)
                || ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_RWIN) & 0x8000) != 0)
        };

        return state;
    }
}
