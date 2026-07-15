using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Interop.Files;

/// <summary>
/// Represents the information contained in an Internet Shortcut (.url) file.
/// </summary>
/// <param name="IconFile">File that contains the icon</param>
/// <param name="IconIndex">Index of the icon in the icon file</param>
public readonly struct InternetShortcutInfo
{
    /// <summary>
    /// URL to which the shortcut leads.
    /// </summary>
    public readonly string? Url;

    /// <summary>
    /// The file that contains the custom icon.
    /// </summary>
    public readonly string? IconFile;
    /// <summary>
    /// The index of the icon in the icon file.
    /// </summary>
    public readonly int IconIndex;

    internal InternetShortcutInfo(string? url, string? iconFile, int iconIndex = 0)
    {
        Url = url;
        IconFile = iconFile;
        IconIndex = iconIndex;
    }
}

/// <summary>
/// Helper class for interacting with Internet Shortcut (.url) files.
/// </summary>
/// <inheritdoc cref="File.ReadAllLines(string)" path="/exception" />
public static class InternetShortcutHelper
{
    public const string INTERNET_SHORTCUT_EXTENSION = ".url";

    /// <summary>
    /// Reads and parses the given internet shortcut file.
    /// </summary>
    /// <inheritdoc cref="File.ReadAllLines(string)" path="/exception" />
    public static InternetShortcutInfo Parse(string path)
    {
        string? url = null;
        string? iconFile = null;
        int? iconIndex = null;
        bool inCorrectSection = false;

        foreach (string line in File.ReadAllLines(path))
        {
            if (line.StartsWith('['))
            {
                inCorrectSection = line.Equals("[InternetShortcut]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inCorrectSection)
                continue;

            if (url is null && line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                url = line[4..];
            else if (iconFile is null && line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                iconFile = line[9..];
            else if (iconIndex is null && line.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(line[10..], out int index))
                    iconIndex = index;
            }

            if (url is not null && iconFile is not null && iconIndex.HasValue)
                break;
        }

        return new InternetShortcutInfo(url, iconFile, iconIndex ?? 0);
    }

    /// <summary>
    /// Tries to get the custom icon of an internet shortcut file.
    /// </summary>
    /// <param name="width">Width in physical device pixels.</param>
    /// <param name="height">Height in physical device pixels.</param>
    /// <returns></returns>
    public static BitmapSource? GetCustomIcon(InternetShortcutInfo info, int width, int height)
    {
        if (info.IconFile is null)
            return null;

        Span<HICON> icons = stackalloc HICON[1];
        uint res = PInvoke.PrivateExtractIcons(info.IconFile, info.IconIndex, width, height, icons, default);

        // Check if extraction failed or path was not valid
        HICON hIcon = icons[0];
        if (res == 0 || res == uint.MaxValue || hIcon.IsNull)
            return null;

        try
        {
            BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(
                hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            bitmap.Freeze();
            return bitmap;
        }
        finally
        {
            PInvoke.DestroyIcon(hIcon);
        }
    }
}
