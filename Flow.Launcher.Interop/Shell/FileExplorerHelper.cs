using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Flow.Launcher.Interop.Files;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
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
    private static unsafe string? GetForegroundExplorerLocationUrl()
    {
        // Object managed by GC
        IShellWindows windows = ShellWindows.CreateInstance<IShellWindows>();

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

        windows.get_Count(out int count).ThrowOnFailure();
        for (int i = 0; i < count; i++)
        {
            using ComVariant index = ComVariant.Create(i);

            if (windows.Item(index, out IDispatch item).Failed)
                // The window may have been closed or become unresponsive
                continue;

            if (item is IWebBrowser2 browser)
            {
                BSTR fullName = default;
                BSTR locationUrl = default;
                try
                {
                    // Make sure that the window is indeed a file explorer
                    // we don't want Internet Explorer or the classic control panel
                    if (browser.get_FullName(&fullName).Failed)
                        continue;
                    if (!Path.GetFileName(fullName).Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (browser.get_HWND(out SHANDLE_PTR pHWND).Failed)
                        continue;

                    HWND hwnd = new(pHWND);
                    if (hwnd == targetWindow && !PInvoke.IsIconic(hwnd))
                    {
                        if (browser.get_LocationURL(&locationUrl).Failed)
                            continue;

                        return locationUrl.ToString();
                    }
                }
                finally
                {
                    if (fullName.Value is not null)
                        PInvoke.SysFreeString(fullName);
                    if (locationUrl.Value is not null)
                        PInvoke.SysFreeString(locationUrl);
                }
            }
        }

        return null;
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
    /// Opens the given folder path in explorer.exe.
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="IOException"></exception>
    /// <exception cref="Win32Exception"></exception>
    public static void OpenFolder(string folderPath)
    {
        // Ensure we are launching a directory and not something weird like an executable
        if (!FileHelper.TryGetAttributes(folderPath, out FileAttribs attribs))
            throw new IOException($"Failed to get the attributes of {folderPath}");
        if (!attribs.HasFlag(FileAttribs.Directory))
            throw new ArgumentException($"{folderPath} is not a directory", nameof(folderPath));

        using FreeLibrarySafeHandle handle = PInvoke.ShellExecute(
            HWND.Null, "open", folderPath, null, null, SHOW_WINDOW_CMD.SW_SHOWNORMAL);

        // The returned value is not an actual handle so lets ensure it never gets freed
        nint rawValue = handle.DangerousGetHandle();
        handle.SetHandleAsInvalid();
        // If returned value is <= 32 then it is an error
        if ((long)rawValue <= 32)
            throw new Win32Exception((int)rawValue);
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
