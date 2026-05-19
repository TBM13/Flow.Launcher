using Windows.Win32;

namespace Flow.Launcher.Infrastructure.Helpers;

/// <summary>
/// Helper class for interacting with Internet Shortcut (.url) files.
/// </summary>
public static class InternetShortcutHelper
{
    public const string INTERNET_SHORTCUT_EXTENSION = ".url";

    /// <summary>
    /// Reads the URL from the given internet shortcut.
    /// <para/>
    /// The URL is not expected to have more than 2048 characters.
    /// </summary>
    public static string? GetUrl(string path)
    {
        Span<char> urlBuffer = stackalloc char[2048];
        uint read = PInvoke.GetPrivateProfileString("InternetShortcut", "URL", string.Empty, urlBuffer, path);
        if (read > 0)
            return urlBuffer[..(int)read].ToString();

        return null;
    }

    /// <summary>
    /// Reads the icon path from the given internet shortcut.
    /// <para/>
    /// The icon path is not expected to have more than <see cref="PInvoke.MAX_PATH"/> characters (260 on Windows).
    /// </summary>
    public static string? GetIconPath(string path)
    {
        Span<char> iconFileBuffer = stackalloc char[(int)PInvoke.MAX_PATH];
        uint read = PInvoke.GetPrivateProfileString("InternetShortcut", "IconFile", string.Empty, iconFileBuffer, path);
        if (read > 0)
            return iconFileBuffer[..(int)read].ToString();

        return null;
    }
}
