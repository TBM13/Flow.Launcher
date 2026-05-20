using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Flow.Launcher.Interop.Shell;

/// <summary>
/// Helper class for interacting with the Windows taskbar.
/// </summary>
public static class TaskbarHelper
{
    private const string TaskbarClassName = "Shell_TrayWnd";
    private const uint TrayBarFlag = 0x05D1;    // Magic from https://github.com/Oliviaophia/SmartTaskbar

    /// <summary>
    /// Shows the taskbar.
    /// </summary>
    /// <exception cref="Win32Exception"></exception>
    public static unsafe void ShowTaskbar()
    {
        // Find the taskbar window
        HWND taskbarHwnd = PInvoke.FindWindowEx(HWND.Null, HWND.Null, TaskbarClassName);
        if (taskbarHwnd == HWND.Null)
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        HMONITOR mon = PInvoke.MonitorFromWindow(taskbarHwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        if (!PInvoke.PostMessage(taskbarHwnd, TrayBarFlag, new WPARAM(1), new LPARAM((nint)mon.Value)))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    /// <summary>
    /// Hides the taskbar.
    /// </summary>
    /// <exception cref="Win32Exception"></exception>
    public static void HideTaskbar()
    {
        // Find the taskbar window
        HWND taskbarHwnd = PInvoke.FindWindowEx(HWND.Null, HWND.Null, TaskbarClassName);
        if (taskbarHwnd == HWND.Null)
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        if (!PInvoke.PostMessage(taskbarHwnd, TrayBarFlag, default, default))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
}
