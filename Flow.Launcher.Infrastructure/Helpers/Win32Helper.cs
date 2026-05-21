using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Flow.Launcher.Infrastructure.Helpers;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;
using Point = System.Windows.Point;

namespace Flow.Launcher.Infrastructure;

public static partial class Win32Helper
{
    #region Blur Handling

    public static unsafe bool DWMSetCloakForWindow(Window window, bool cloak)
    {
        var cloaked = cloak ? 1 : 0;

        return PInvoke.DwmSetWindowAttribute(
            GetWindowHandle(window),
            DWMWINDOWATTRIBUTE.DWMWA_CLOAK,
            &cloaked,
            (uint)Marshal.SizeOf<int>()).Succeeded;
    }
    #endregion

    #region Window Foreground

    public static unsafe nint GetForegroundWindow()
    {
        return (nint)PInvoke.GetForegroundWindow().Value;
    }

    public static bool SetForegroundWindow(Window window)
    {
        return PInvoke.SetForegroundWindow(GetWindowHandle(window));
    }

    public static bool SetForegroundWindow(nint handle)
    {
        return PInvoke.SetForegroundWindow(new(handle));
    }

    public static bool IsForegroundWindow(Window window)
    {
        return IsForegroundWindow(GetWindowHandle(window));
    }

    internal static bool IsForegroundWindow(HWND handle)
    {
        return handle.Equals(PInvoke.GetForegroundWindow());
    }

    #endregion

    #region Task Switching

    /// <summary>
    /// Hide windows in the Alt+Tab window list
    /// </summary>
    /// <param name="window">To hide a window</param>
    public static void HideFromAltTab(Window window)
    {
        var hwnd = GetWindowHandle(window);

        var exStyle = GetWindowStyle(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        // Add TOOLWINDOW style, remove APPWINDOW style
        var newExStyle = ((uint)exStyle | (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) & ~(uint)WINDOW_EX_STYLE.WS_EX_APPWINDOW;

        SetWindowStyle(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)newExStyle);
    }

    /// <summary>
    /// Restore window display in the Alt+Tab window list.
    /// </summary>
    /// <param name="window">To restore the displayed window</param>
    public static void ShowInAltTab(Window window)
    {
        var hwnd = GetWindowHandle(window);

        var exStyle = GetWindowStyle(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        // Remove the TOOLWINDOW style and add the APPWINDOW style.
        var newExStyle = ((uint)exStyle & ~(uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) | (uint)WINDOW_EX_STYLE.WS_EX_APPWINDOW;

        SetWindowStyle(GetWindowHandle(window), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)newExStyle);
    }

    /// <summary>
    /// Disable windows toolbar's control box
    /// This will also disable system menu with Alt+Space hotkey
    /// </summary>
    public static void DisableControlBox(Window window)
    {
        var hwnd = GetWindowHandle(window);

        var style = GetWindowStyle(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);

        style &= ~(int)WINDOW_STYLE.WS_SYSMENU;

        SetWindowStyle(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);
    }

    private static int GetWindowStyle(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex)
    {
        var style = PInvoke.GetWindowLong(hWnd, nIndex);
        if (style == 0 && Marshal.GetLastPInvokeError() != 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        return style;
    }

    private static nint SetWindowStyle(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, int dwNewLong)
    {
        PInvoke.SetLastError(WIN32_ERROR.NO_ERROR); // Clear any existing error

        var result = PInvoke.SetWindowLong(hWnd, nIndex, dwNewLong);
        if (result == 0 && Marshal.GetLastPInvokeError() != 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return result;
    }

    #endregion

    #region Window Fullscreen

    private const string WINDOW_CLASS_CONSOLE = "ConsoleWindowClass";
    private const string WINDOW_CLASS_WINTAB = "Flip3D";
    private const string WINDOW_CLASS_PROGMAN = "Progman";
    private const string WINDOW_CLASS_WORKERW = "WorkerW";

    private static HWND _hwnd_shell;
    private static HWND HWND_SHELL =>
        _hwnd_shell != HWND.Null ? _hwnd_shell : _hwnd_shell = PInvoke.GetShellWindow();

    private static HWND _hwnd_desktop;
    private static HWND HWND_DESKTOP =>
        _hwnd_desktop != HWND.Null ? _hwnd_desktop : _hwnd_desktop = PInvoke.GetDesktopWindow();

    public static unsafe bool IsForegroundWindowFullscreen()
    {
        // Get current active window
        var hWnd = PInvoke.GetForegroundWindow();
        if (hWnd.Equals(HWND.Null))
        {
            return false;
        }

        // If current active window is desktop or shell, exit early
        if (hWnd.Equals(HWND_DESKTOP) || hWnd.Equals(HWND_SHELL))
        {
            return false;
        }

        string windowClass;
        const int capacity = 256;
        Span<char> buffer = stackalloc char[capacity];
        int validLength;
        fixed (char* pBuffer = buffer)
        {
            validLength = PInvoke.GetClassName(hWnd, pBuffer, capacity);
        }

        windowClass = buffer[..validLength].ToString();

        // For Win+Tab (Flip3D)
        if (windowClass == WINDOW_CLASS_WINTAB)
        {
            return false;
        }

        PInvoke.GetWindowRect(hWnd, out var appBounds);

        // For console (ConsoleWindowClass), we have to check for negative dimensions
        if (windowClass == WINDOW_CLASS_CONSOLE)
        {
            return appBounds.top < 0 && appBounds.bottom < 0;
        }

        // For desktop (Progman or WorkerW, depends on the system), we have to check
        if (windowClass is WINDOW_CLASS_PROGMAN or WINDOW_CLASS_WORKERW)
        {
            var hWndDesktop = PInvoke.FindWindowEx(hWnd, HWND.Null, "SHELLDLL_DefView", null);
            hWndDesktop = PInvoke.FindWindowEx(hWndDesktop, HWND.Null, "SysListView32", "FolderView");
            if (hWndDesktop != HWND.Null)
            {
                return false;
            }
        }

        MonitorInfo? monitorInfo = MonitorHelper.GetNearestDisplayMonitor(hWnd);
        return (appBounds.bottom - appBounds.top) == monitorInfo?.Bounds.Height &&
               (appBounds.right - appBounds.left) == monitorInfo?.Bounds.Width;
    }

    #endregion

    #region Pixel to DIP

    /// <summary>
    /// Transforms pixels to Device Independent Pixels used by WPF
    /// </summary>
    /// <param name="visual">current window, required to get presentation source</param>
    /// <param name="unitX">horizontal position in pixels</param>
    /// <param name="unitY">vertical position in pixels</param>
    /// <returns>point containing device independent pixels</returns>
    public static Point TransformPixelsToDIP(Visual visual, double unitX, double unitY)
    {
        Matrix matrix;
        var source = PresentationSource.FromVisual(visual);
        if (source is not null)
        {
            matrix = source.CompositionTarget.TransformFromDevice;
        }
        else
        {
            using var src = new HwndSource(new HwndSourceParameters());
            matrix = src.CompositionTarget.TransformFromDevice;
        }

        return new Point((int)(matrix.M11 * unitX), (int)(matrix.M22 * unitY));
    }

    #endregion

    #region WndProc

    public const int WM_ENTERSIZEMOVE = (int)PInvoke.WM_ENTERSIZEMOVE;
    public const int WM_EXITSIZEMOVE = (int)PInvoke.WM_EXITSIZEMOVE;
    public const int WM_NCLBUTTONDBLCLK = (int)PInvoke.WM_NCLBUTTONDBLCLK;
    public const int WM_SYSCOMMAND = (int)PInvoke.WM_SYSCOMMAND;

    public const int SC_MAXIMIZE = (int)PInvoke.SC_MAXIMIZE;
    public const int SC_MINIMIZE = (int)PInvoke.SC_MINIMIZE;

    #endregion

    #region Window Handle

    internal static HWND GetWindowHandle(Window window, bool ensure = false)
    {
        var windowHelper = new WindowInteropHelper(window);
        if (ensure)
        {
            windowHelper.EnsureHandle();
        }
        return new(windowHelper.Handle);
    }

    #endregion

    #region File / Folder Dialog

    public static string SelectFile()
    {
        var dlg = new OpenFileDialog();
        var result = dlg.ShowDialog();
        if (result == true)
            return dlg.FileName;

        return string.Empty;
    }

    #endregion
}
