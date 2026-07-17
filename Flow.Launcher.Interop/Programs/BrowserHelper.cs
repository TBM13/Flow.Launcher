using System.IO;
using Microsoft.Win32;

namespace Flow.Launcher.Interop.Programs;

/// <summary>
/// Helper class for interacting with the default browser.
/// </summary>
public static class BrowserHelper
{
    /// <summary>
    /// Gets the path of the default browser's executable.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public static string GetDefaultBrowserPath()
    {
        using RegistryKey? regDefaultLatest = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoiceLatest\ProgId", false);
        string? browserProgramId = (string?)regDefaultLatest?.GetValue("ProgId");

        // Try with older registry key
        if (browserProgramId is null)
        {
            using RegistryKey? regDefault = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice", false);
            browserProgramId = (string?)regDefault?.GetValue("ProgId")
                ?? throw new InvalidOperationException("Couldn't find default browser program ID");
        }

        using RegistryKey? regKey = Registry.ClassesRoot.OpenSubKey(browserProgramId + @"\shell\open\command", false);
        string path = (string?)regKey?.GetValue(null) ?? throw new InvalidOperationException(
            $"Couldn't find the path of the default browser ({browserProgramId})");

        int extensionIndex = path.LastIndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (extensionIndex == -1)
            throw new InvalidOperationException($"Default browser is not an executable: {path}.");

        path = path[..(extensionIndex + 4)].TrimStart('\"');
        if (!File.Exists(path))
            throw new FileNotFoundException("Default browser does not exist", path);

        return path;
    }

    /// <exception cref="ArgumentException"></exception>
    private static Uri StringToUri(string url)
    {
        bool success = Uri.TryCreate(url, UriKind.Absolute, out Uri? uri);
        if (!success)
        {
            // Retry by adding "https://" at the start
            Uri.TryCreate("https://" + url, UriKind.Absolute, out uri);
        }

        if (uri is null)
            throw new ArgumentException($"Invalid URL: {url}", nameof(url));

        return uri;
    }

    /// <inheritdoc cref="StringToUri(string)" path="/exception"/>
    /// <inheritdoc cref="OpenInNewTab(Uri, string?)(Uri, string?)"/>
    public static void OpenInNewTab(string uri, string? browserPath = null)
        => OpenInNewTab(StringToUri(uri), browserPath);
    /// <summary> 
    /// Opens the URI in a new tab of the default browser.
    /// </summary>
    /// <inheritdoc cref="OpenUri(Uri, string[], string?)" path="/remarks"/>
    /// <inheritdoc cref="OpenUri(Uri, string[], string?)" path="/exception"/>
    public static void OpenInNewTab(Uri uri, string? browserPath = null)
        => OpenUri(uri, [uri.AbsoluteUri], browserPath);

    /// <inheritdoc cref="StringToUri(string)" path="/exception"/>
    /// <inheritdoc cref="OpenInNewWindow(Uri, string?)"/>
    public static void OpenInNewWindow(string uri, string? browserPath = null)
        => OpenInNewWindow(StringToUri(uri), browserPath);
    /// <summary> 
    /// Opens the URI in a new window of the default browser.
    /// </summary>
    /// <inheritdoc cref="OpenUri(Uri, string[], string?)" path="/remarks"/>
    /// <inheritdoc cref="OpenUri(Uri, string[], string?)" path="/exception"/>
    public static void OpenInNewWindow(Uri uri, string? browserPath = null)
        => OpenUri(uri, ["--new-window", uri.AbsoluteUri], browserPath);

    /// <summary> 
    /// Opens the specified browser (or the default one if null) with the given arguments.
    /// </summary>
    /// <remarks>
    /// If the default browser can't be obtained and the URI is an actual URL (HTTP/HTTPS),
    /// fallbacks to letting Windows handle it.
    /// </remarks>
    /// <inheritdoc cref="GetDefaultBrowserPath" path="/exception"/>
    private static void OpenUri(Uri uri, string[] args, string? browserPath)
    {
        try
        {
            browserPath ??= GetDefaultBrowserPath();
        }
        catch
        {
            // Fallback to letting Windows handle the URI, but only if it is an URL
            // (we don't want to end up running an executable or anything weird)
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                throw;

            ProcessHelper.StartProcess(uri.AbsoluteUri, useShellExecute: true);
            return;
        }

        string workingDir = Path.GetDirectoryName(browserPath) ?? string.Empty;
        ProcessHelper.StartProcess(browserPath, workingDirectory: workingDir, arguments: args);
    }
}
