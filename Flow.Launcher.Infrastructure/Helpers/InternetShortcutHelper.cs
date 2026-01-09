using System;
using Windows.Win32;

namespace Flow.Launcher.Infrastructure.Helpers;

public static class InternetShortcutHelper
{
    public const string INTERNET_SHORTCUT_EXTENSION = ".url";

    public static string? GetUrl(string path)
    {
        Span<char> urlBuffer = stackalloc char[2048];
        uint read = PInvoke.GetPrivateProfileString("InternetShortcut", "URL", string.Empty, urlBuffer, path);
        if (read > 0)
            return urlBuffer[..(int)read].ToString();

        return null;
    }

    public static string? GetIconPath(string path)
    {
        Span<char> iconFileBuffer = stackalloc char[1024];
        uint read = PInvoke.GetPrivateProfileString("InternetShortcut", "IconFile", string.Empty, iconFileBuffer, path);
        if (read > 0)
            return iconFileBuffer[..(int)read].ToString();

        return null;
    }
}
