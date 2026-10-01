using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using iNKORE.UI.WPF.Modern.Native;
using Windows.Win32.Graphics.Dwm;

namespace iNKORE.UI.WPF.Modern.Helpers.Styles;

public static class BackdropHelper
{
    /// <summary>
    /// Tries to inform the operating system that this window uses dark mode.
    /// </summary>
    /// <param name="window">Window to apply effect.</param>
    public static void ApplyDarkMode(this Window window)
    {
        if (window == null)
            return;

        try
        {
            var windowHandle = new WindowInteropHelper(window).EnsureHandle();

            if (windowHandle == IntPtr.Zero) return;

            ApplyDarkMode(windowHandle);
        }
        catch { }
    }

    /// <summary>
    /// Tries to inform the operating system that this <c>hWnd</c> uses dark mode.
    /// </summary>
    /// <param name="handle">Pointer to the window handle.</param>
    public static void ApplyDarkMode(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;

        var pvAttribute = (int)DWMAPI.PvAttribute.Enable;
        var dwAttribute = DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE;

        DWMAPI.DwmSetWindowAttribute(handle, dwAttribute,
            ref pvAttribute,
            Marshal.SizeOf(typeof(int)));
    }

    /// <summary>
    /// Tries to clear the dark theme usage information.
    /// </summary>
    /// <param name="window">Window to remove effect.</param>
    public static void RemoveDarkMode(this Window window)
    {
        if (window == null)
            return;

        try
        {
            var windowHandle = new WindowInteropHelper(window).EnsureHandle();

            if (windowHandle == IntPtr.Zero) return;

            RemoveDarkMode(windowHandle);
        }
        catch { }
    }

    /// <summary>
    /// Tries to clear the dark theme usage information.
    /// </summary>
    /// <param name="handle">Pointer to the window handle.</param>
    public static void RemoveDarkMode(IntPtr handle)
    {
        if (handle == IntPtr.Zero) { return; }

        var pvAttribute = (int)DWMAPI.PvAttribute.Disable;
        var dwAttribute = DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE;

        DWMAPI.DwmSetWindowAttribute(handle, dwAttribute,
            ref pvAttribute,
            Marshal.SizeOf(typeof(int)));
    }

    /// <summary>
    /// Tries to remove default TitleBar from <c>hWnd</c>.
    /// </summary>
    /// <param name="window">Window to remove effect.</param>
    public static void RemoveTitleBar(this Window window)
    {
        var windowHandle = new WindowInteropHelper(window).EnsureHandle();

        if (windowHandle == IntPtr.Zero) return;

        RemoveTitleBar(windowHandle);
    }

    /// <summary>
    /// Tries to remove default TitleBar from <c>hWnd</c>.
    /// </summary>
    /// <param name="handle">Pointer to the window handle.</param>
    /// <returns><see langowrd="false"/> is problem occurs.</returns>
    private static bool RemoveTitleBar(IntPtr handle)
    {
        // Hide default TitleBar
        // https://stackoverflow.com/questions/743906/how-to-hide-close-button-in-wpf-window
        try
        {
            User32.SetWindowLong(handle, -16, User32.GetWindowLong(handle, -16) & ~0x80000);

            return true;
        }
        catch (Exception e)
        {
#if DEBUG
            Console.WriteLine(e);
#endif
            return false;
        }
    }
}
