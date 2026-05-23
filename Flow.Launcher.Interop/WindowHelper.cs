using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Flow.Launcher.Infrastructure.Helpers;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Interop;

/// <summary>
/// Helper class for interacting with open windows.
/// </summary>
public static class WindowHelper
{
    public const int WM_ENTERSIZEMOVE = (int)PInvoke.WM_ENTERSIZEMOVE;
    public const int WM_EXITSIZEMOVE = (int)PInvoke.WM_EXITSIZEMOVE;
    public const int WM_NCLBUTTONDBLCLK = (int)PInvoke.WM_NCLBUTTONDBLCLK;
    public const int WM_SYSCOMMAND = (int)PInvoke.WM_SYSCOMMAND;

    public const int SC_MAXIMIZE = (int)PInvoke.SC_MAXIMIZE;
    public const int SC_MINIMIZE = (int)PInvoke.SC_MINIMIZE;

    /// <summary>
    /// Retrieves a handle to the given window.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public static nint GetWindowHandle(Window window, bool ensure = false)
    {
        WindowInteropHelper windowHelper = new(window);
        if (ensure)
            windowHelper.EnsureHandle();

        nint handle = windowHelper.Handle;
        if (handle == 0)
            throw new InvalidOperationException("The window must be shown before getting its handle");

        return handle;
    }

    /// <summary>
    /// Retrieves a handle to the foreground window.
    /// </summary>
    /// <returns>The handle to the foreground window or zero.</returns>
    public static nint GetForegroundWindow()
    {
        return (nint)PInvoke.GetForegroundWindow();
    }

    /// <summary>
    /// Checks whether the foreground window is in actual fullscreen mode.
    /// </summary>
    /// <returns>
    /// True for most fullscreen games but false for most fullscreen apps
    /// (like explorer or a browser when F11 is pressed or a fullscreen video is playing).
    /// </returns>
    public static bool IsForegroundWindowFullscreen()
    {
        HWND foregroundHwnd = PInvoke.GetForegroundWindow();
        if (foregroundHwnd.IsNull)
            return false;

        // Shell and desktop windows should not be considered fullscreen
        if (foregroundHwnd == PInvoke.GetShellWindow() || foregroundHwnd == PInvoke.GetDesktopWindow())
            return false;

        // If the window has the normal status of maximized (zoomed), it's likely not in fullscreen mode.
        // Prevents false positives when the taskbar is set to auto-hide and thus
        // maximized windows cover the entire screen
        if (PInvoke.IsZoomed(foregroundHwnd))
            return false;

        if (!PInvoke.GetWindowRect(foregroundHwnd, out RECT appBounds))
            return false; // The window might have been closed

        MonitorInfo? monitorInfo = MonitorHelper.GetNearestDisplayMonitor(foregroundHwnd);
        if (monitorInfo is null)
            return false;

        bool isFullscreen = appBounds.left <= monitorInfo.Bounds.X
           && appBounds.top <= monitorInfo.Bounds.Y
           && (appBounds.right - appBounds.left) >= monitorInfo.Bounds.Width
           && (appBounds.bottom - appBounds.top) >= monitorInfo.Bounds.Height;

        return isFullscreen;
    }

    /// <summary>
    /// Cloaks the window such that it is not visible to the user
    /// but still gets composed by DWM.
    /// </summary>
    /// <param name="cloak">Whether the cloak attribute should be set or removed.</param>
    public static void DWMSetCloakForWindow(Window window, bool cloak)
    {
        BOOL cloaked = cloak;
        unsafe
        {
            PInvoke.DwmSetWindowAttribute(
                (HWND)GetWindowHandle(window),
                DWMWINDOWATTRIBUTE.DWMWA_CLOAK,
                &cloaked,
                (uint)sizeof(BOOL)).ThrowOnFailure();
        }
    }

    /// <summary>
    /// Hides the window from the Alt+Tab window list.
    /// </summary>
    public static void HideFromAltTab(Window window)
    {
        HWND hwnd = (HWND)GetWindowHandle(window);
        int exStyle = GetWindowStyle(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        // Add TOOLWINDOW style, remove APPWINDOW style
        uint newExStyle = ((uint)exStyle | (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW)
            & ~(uint)WINDOW_EX_STYLE.WS_EX_APPWINDOW;

        SetWindowStyle(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)newExStyle);
    }

    /// <summary>
    /// Disables the window toolbar's control box
    /// and the system menu that appears when pressing Alt+Space.
    /// </summary>
    public static void DisableControlBox(Window window)
    {
        HWND hwnd = (HWND)GetWindowHandle(window);

        // Remove SYSMENU style
        int style = GetWindowStyle(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~(int)WINDOW_STYLE.WS_SYSMENU;

        SetWindowStyle(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);
    }

    private static int GetWindowStyle(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex)
    {
        int style = PInvoke.GetWindowLong(hWnd, nIndex);
        if (style == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        return style;
    }

    private static nint SetWindowStyle(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, int dwNewLong)
    {
        nint result = PInvoke.SetWindowLong(hWnd, nIndex, dwNewLong);
        if (result == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        return result;
    }
}
