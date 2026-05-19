using System.IO;

namespace Flow.Launcher.Infrastructure.Helpers;

/// <summary>
/// Represents the information contained in an Internet Shortcut (.url) file.
/// </summary>
/// <param name="Url">URL to which the shortcut leads</param>
/// <param name="IconFile">File that contains the icon</param>
public readonly record struct InternetShortcutInfo(string? Url, string? IconFile);

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

            if (url is not null && iconFile is not null)
                break;
        }

        return new InternetShortcutInfo(url, iconFile);
    }
}
