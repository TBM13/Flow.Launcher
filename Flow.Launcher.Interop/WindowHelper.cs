using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Flow.Launcher.Interop.Hardware;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Interop;

/// <summary>
/// Helper class for interacting with open windows.
/// </summary>
public static partial class WindowHelper
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
    /// Returns the next visible, non-minimized window in Z-order after the given window.
    /// </summary>
    public static nint GetNextVisibleWindow(nint hwnd)
    {
        HWND next = PInvoke.GetWindow(new(hwnd), GET_WINDOW_CMD.GW_HWNDNEXT);
        while (!next.IsNull)
        {
            if (PInvoke.IsWindowVisible(next) && !PInvoke.IsIconic(next))
                return next;

            next = PInvoke.GetWindow(next, GET_WINDOW_CMD.GW_HWNDNEXT);
        }

        return 0;
    }

    /// <summary>
    /// Returns a list with all window handles.
    /// </summary>
    /// <exception cref="Win32Exception"></exception>
    public static unsafe List<nint> GetAllWindows()
    {
        List<nint> windows = new(256);
        GCHandle handle = GCHandle.Alloc(windows);
        try
        {
            bool res = PInvoke.EnumWindows(&EnumCallback, (LPARAM)GCHandle.ToIntPtr(handle));

            if (!res)
                throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        finally
        {
            handle.Free();
        }

        return windows;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        static BOOL EnumCallback(HWND hWnd, LPARAM data)
        {
            try
            {
                GCHandle handle = GCHandle.FromIntPtr((nint)data);
                if (handle.Target is List<nint> list)
                    list.Add(hWnd);
            }
            catch
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Retrieves the ID of the thread that created the specified window and, optionally, the ID of the process that created the window.
    /// </summary>
    /// <param name="processId">The ID of the process. It is never zero.</param>
    /// <returns>The ID of the thread that created the window. The ID is never zero.</returns>
    /// <exception cref="Win32Exception"></exception>
    public static uint GetWindowThreadProcessId(nint hwnd, out uint processId)
    {
        uint res = PInvoke.GetWindowThreadProcessId(new(hwnd), out processId);
        if (res == 0 || processId == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        return res;
    }

    /// <summary>
    /// Gets the title of the given window.
    /// </summary>
    /// <exception cref="Win32Exception"></exception>
    public static string GetWindowTitle(nint hwnd)
    {
        HWND hWnd = new(hwnd);

        Span<char> buffer = stackalloc char[512];  // 1024 bytes
        int copied = PInvoke.GetWindowText(hWnd, buffer);

        if (copied >= buffer.Length - 1)
        {
            // The title likely did not fit on the buffer
            int titleLength = PInvoke.GetWindowTextLength(hWnd);
            if (titleLength == 0)
            {
                int lastError = Marshal.GetLastPInvokeError();
                if (lastError != 0)
                    throw new Win32Exception(lastError);

                return string.Empty;
            }

            int bufferSize = titleLength + 1;
            char[] rentedArray = ArrayPool<char>.Shared.Rent(bufferSize);
            buffer = rentedArray.AsSpan(0, bufferSize);

            try
            {
                copied = PInvoke.GetWindowText(hWnd, buffer);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rentedArray);
            }
        }

        if (copied == 0)
        {
            int lastError = Marshal.GetLastPInvokeError();
            if (lastError != 0)
                throw new Win32Exception(lastError);
        }

        return buffer[..copied].ToString();
    }

    /// <summary>
    /// Checks whether the given window is actually visible.
    /// </summary>
    /// <remarks>A minimized window may still be considered visible.</remarks>
    /// <exception cref="Win32Exception"/>
    public static bool IsWindowVisible(nint hwnd)
    {
        HWND hWnd = new(hwnd);

        // Window must have WS_VISIBLE style
        if (!PInvoke.IsWindowVisible(hWnd))
            return false;

        // Must not be cloaked (virtual desktops, minimized UWP apps, etc)
        unsafe
        {
            int cloaked = 0;
            PInvoke.DwmGetWindowAttribute(
                hWnd,
                DWMWINDOWATTRIBUTE.DWMWA_CLOAKED,
                &cloaked,
                sizeof(int)).ThrowOnFailure();

            if (cloaked != 0)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks whether the given window is in actual fullscreen mode.
    /// </summary>
    /// <returns>
    /// True for most fullscreen games but false for most fullscreen apps
    /// (like explorer or a browser when F11 is pressed or a fullscreen video is playing).
    /// </returns>
    public static bool IsWindowFullscreen(nint hwnd)
    {
        HWND foregroundHwnd = new(hwnd);
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

    private const uint LightPopupBorderColor = 0x00E2E2E2;
    private const uint DarkPopupBorderColor = 0x00333333;
    private const int WcaAccentPolicy = 19;
    private const int AccentEnableAcrylicBlurBehind = 4;
    private const int AccentFlagDrawAllBorders = 2;
    private const uint DarkAcrylicTint = 0x99242424;
    private const uint LightAcrylicTint = 0xB3F9F9F9;

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public nint Data;
        public int SizeOfData;
    }

    [LibraryImport("user32.dll")]
    private static partial int SetWindowCompositionAttribute(nint hwnd, ref WindowCompositionAttributeData data);

    /// <summary>
    /// Whether the current OS supports Windows 11 rounded corners and Acrylic popup backdrops.
    /// </summary>
    public static bool SupportsAcrylicPopup { get; } = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

    /// <summary>
    /// Applies Windows 11 rounded corners and Acrylic blur backdrop to a popup window handle.
    /// </summary>
    public static void EnablePopupAcrylic(nint hwnd, bool isDark)
    {
        if (!SupportsAcrylicPopup || hwnd == 0)
            return;

        HWND hWnd = new(hwnd);
        BOOL dark = isDark;
        DWM_WINDOW_CORNER_PREFERENCE corner = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
        uint borderColor = isDark ? DarkPopupBorderColor : LightPopupBorderColor;
        MARGINS margins = new()
        {
            cxLeftWidth = -1,
            cxRightWidth = -1,
            cyTopHeight = -1,
            cyBottomHeight = -1
        };

        unsafe
        {
            _ = PInvoke.DwmExtendFrameIntoClientArea(hWnd, in margins);

            _ = PInvoke.DwmSetWindowAttribute(
                hWnd,
                DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
                &dark,
                (uint)sizeof(BOOL));

            _ = PInvoke.DwmSetWindowAttribute(
                hWnd,
                DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
                &corner,
                (uint)sizeof(DWM_WINDOW_CORNER_PREFERENCE));

            _ = PInvoke.DwmSetWindowAttribute(
                hWnd,
                DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR,
                &borderColor,
                sizeof(uint));
        }

        AccentPolicy accent = new()
        {
            AccentState = AccentEnableAcrylicBlurBehind,
            AccentFlags = AccentFlagDrawAllBorders,
            GradientColor = isDark ? DarkAcrylicTint : LightAcrylicTint
        };

        unsafe
        {
            WindowCompositionAttributeData data = new()
            {
                Attribute = WcaAccentPolicy,
                Data = (nint)(&accent),
                SizeOfData = sizeof(AccentPolicy)
            };
            _ = SetWindowCompositionAttribute(hwnd, ref data);
        }
    }

    /// <summary>
    /// Cloaks the window such that it is not visible to the user
    /// but still gets composed by DWM.
    /// </summary>
    /// <param name="cloak">Whether the cloak attribute should be set or removed.</param>
    public static void DWMSetCloakForWindow(nint hwnd, bool cloak)
    {
        BOOL cloaked = cloak;
        unsafe
        {
            PInvoke.DwmSetWindowAttribute(
                new(hwnd),
                DWMWINDOWATTRIBUTE.DWMWA_CLOAK,
                &cloaked,
                (uint)sizeof(BOOL)).ThrowOnFailure();
        }
    }

    /// <summary>
    /// Hides the window from the Alt+Tab window list.
    /// </summary>
    public static void HideFromAltTab(nint hwnd)
    {
        HWND window = new(hwnd);
        int exStyle = GetWindowStyle(window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        // Add TOOLWINDOW style, remove APPWINDOW style
        uint newExStyle = ((uint)exStyle | (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW)
            & ~(uint)WINDOW_EX_STYLE.WS_EX_APPWINDOW;

        SetWindowStyle(window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)newExStyle);
    }

    /// <summary>
    /// Disables the window toolbar's control box
    /// and the system menu that appears when pressing Alt+Space.
    /// </summary>
    public static void DisableControlBox(nint hwnd)
    {
        HWND window = new(hwnd);

        // Remove SYSMENU style
        int style = GetWindowStyle(window, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~(int)WINDOW_STYLE.WS_SYSMENU;

        SetWindowStyle(window, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);
    }

    /// <exception cref="Win32Exception"></exception>
    private static int GetWindowStyle(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex)
    {
        int style = PInvoke.GetWindowLong(hWnd, nIndex);
        if (style == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        return style;
    }

    /// <exception cref="Win32Exception"></exception>
    private static nint SetWindowStyle(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, int dwNewLong)
    {
        nint result = PInvoke.SetWindowLong(hWnd, nIndex, dwNewLong);
        if (result == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        return result;
    }
}
