using System.IO;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Interop.Shell;

/// <summary>
/// Helper class for interacting with explorer.exe
/// </summary>
public static class FileExplorerHelper
{
    private static readonly string DesktopLocationUrl =
        new Uri(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)).AbsoluteUri;

    /// <summary>
    /// Gets the path of the file explorer window that is currently in the foreground,
    /// or immediately behind our own window if we are focused.
    /// </summary>
    /// <remarks>The desktop itself is considered a file explorer window.</remarks>
    /// <returns>Null if no explorer window is focused or it is minimized.</returns>
    public static string? GetForegroundExplorerPath()
    {
        string? locationUrl = GetForegroundExplorerLocationUrl();
        if (string.IsNullOrEmpty(locationUrl))
            return null;

        if (!Uri.TryCreate(locationUrl, UriKind.Absolute, out Uri? uri))
            return null;

        string path = uri.LocalPath;
        if (!Path.EndsInDirectorySeparator(path))
            path += Path.DirectorySeparatorChar;

        return path;
    }

    /// <summary>
    /// Gets the LocationURL of the file explorer window that is currently in the foreground,
    /// or immediately behind our own window if we are focused.
    /// </summary>
    /// <remarks>The desktop itself is considered a file explorer window.</remarks>
    /// <returns>Null if no explorer window is focused or it is minimized.</returns>
    private static string? GetForegroundExplorerLocationUrl()
    {
        ShellWindows shellWindows = new();
        try
        {
            IShellWindows windows = (IShellWindows)shellWindows;
            HWND foregroundWindow = PInvoke.GetForegroundWindow();
            HWND targetWindow = foregroundWindow;
            HWND shellDesktopWindow = PInvoke.GetShellWindow();

            // If our application is the foreground window, look for the
            // explorer window immediately behind it in Z-order
            PInvoke.GetWindowThreadProcessId(foregroundWindow, out uint foregroundPid);
            if (foregroundPid == (uint)Environment.ProcessId)
            {
                targetWindow = GetNextVisibleWindow(foregroundWindow);
                if (targetWindow.IsNull || targetWindow == shellDesktopWindow)
                    return DesktopLocationUrl;
            }

            // If the desktop itself is the foreground window, return its location
            if (targetWindow == shellDesktopWindow)
                return DesktopLocationUrl;

            int count = windows.Count;
            for (int i = 0; i < count; i++)
            {
                object? item = null;
                try
                {
                    item = windows.Item(i);
                    if (item is IWebBrowser2 browser)
                    {
                        // Make sure that the window is indeed a file explorer
                        // we don't want Internet Explorer or the classic control panel
                        BSTR fullName = browser.FullName;
                        if (!Path.GetFileName(fullName).Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
                            continue;

                        HWND hwnd = new(browser.HWND);
                        if (hwnd == targetWindow && !PInvoke.IsIconic(hwnd))
                            return browser.LocationURL.ToString();
                    }
                }
                catch (COMException)
                {
                    // The window may have been closed or become unresponsive
                    continue;
                }
                finally
                {
                    if (item is not null)
                        Marshal.ReleaseComObject(item);
                }
            }

            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(shellWindows);
        }
    }

    /// <summary>
    /// Returns the next visible, non-minimized window in Z-order after the given window.
    /// </summary>
    private static HWND GetNextVisibleWindow(HWND hwnd)
    {
        HWND next = PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_HWNDNEXT);
        while (!next.IsNull)
        {
            if (PInvoke.IsWindowVisible(next) && !PInvoke.IsIconic(next))
                return next;

            next = PInvoke.GetWindow(next, GET_WINDOW_CMD.GW_HWNDNEXT);
        }

        return HWND.Null;
    }

    /// <summary>
    /// Opens the containing folder of the given file path in explorer.exe and selects the file.
    /// </summary>
    public static unsafe void OpenFolderAndSelectFile(string filePath)
    {
        ITEMIDLIST* pidlFile = null;
        try
        {
            PInvoke.SHParseDisplayName(filePath, null, out pidlFile, 0).ThrowOnFailure();
            PInvoke.SHOpenFolderAndSelectItems(pidlFile, 0, null, 0).ThrowOnFailure();
        }
        finally
        {
            if (pidlFile is not null)
                PInvoke.CoTaskMemFree(pidlFile);
        }
    }
}
