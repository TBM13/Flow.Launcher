using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Flow.Launcher.Infrastructure.Helpers;

public static class FileExplorerHelper
{
    /// <summary>
    /// Gets the path of the file explorer that is currently in the foreground.
    /// <para/>
    /// Returns null if no explorer window is focused or if it is minimized.
    /// </summary>
    public static string? GetForegroundExplorerPath()
    {
        string? locationUrl = GetForegroundExplorerLocationUrl();
        if (string.IsNullOrEmpty(locationUrl))
            return null;

        string path = new Uri(locationUrl).LocalPath;
        if (!Path.EndsInDirectorySeparator(path))
            path += Path.DirectorySeparatorChar;

        return path;
    }

    /// <summary>
    /// Gets the LocationURL of the file explorer that is currently in the foreground.
    /// Returns null if no explorer window is focused or if it is minimized.
    /// </summary>
    private static string? GetForegroundExplorerLocationUrl()
    {
        var shellWindows = new ShellWindows();
        IShellWindows? windows = null;
        try
        {
            windows = (IShellWindows)shellWindows;
            var foregroundWindow = PInvoke.GetForegroundWindow();
            int count = windows.Count;

            for (int i = 0; i < count; i++)
            {
                object? item = windows.Item(i);
                if (item is null)
                    continue;

                IWebBrowser2? browser = null;
                try
                {
                    browser = item as IWebBrowser2;
                    if (browser is null)
                        continue;

                    // Make sure that the window is indeed a file explorer
                    // we don't want the Internet Explorer or the classic control panel
                    string fullName = browser.FullName.ToString();
                    if (!Path.GetFileName(fullName).Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var hwnd = new HWND(browser.HWND);
                    if (hwnd == foregroundWindow && !PInvoke.IsIconic(hwnd))
                    {
                        return browser.LocationURL.ToString();
                    }
                }
                finally
                {
                    if (browser is not null && !ReferenceEquals(browser, item))
                        Marshal.ReleaseComObject(browser);

                    if (Marshal.IsComObject(item))
                        Marshal.ReleaseComObject(item);
                }
            }

            return null;
        }
        finally
        {
            if (windows is not null && !ReferenceEquals(windows, shellWindows))
                Marshal.ReleaseComObject(windows);

            Marshal.ReleaseComObject(shellWindows);
        }
    }
}
