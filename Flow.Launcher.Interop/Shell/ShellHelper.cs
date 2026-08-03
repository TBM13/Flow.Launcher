using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Flow.Launcher.Interop.Shell;

public static class ShellHelper
{
    /// <summary>
    /// Extracts a localized string from a given indirect string reference (e.g. "@shell32.dll,-4117").
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="COMException"></exception>
    public static string LoadIndirectString(string indirectString)
    {
        if (!indirectString.StartsWith('@'))
            throw new ArgumentException(
                "The string is not a valid indirect string reference", nameof(indirectString));

        // When the buffer is too small, SHLoadIndirectString throws a generic
        // 0x80004005 unspecified error (which we cannot differentiate from other errors).
        // Lets try to read the string only once using a relatively big buffer
        Span<char> buffer = stackalloc char[3072]; // 6 KB
        PInvoke.SHLoadIndirectString(indirectString, buffer).ThrowOnFailure();

        return buffer[..buffer.IndexOf('\0')].ToString();
    }

    /// <summary>
    /// Returns the friendly, localized display name of a shell item (e.g. "File Explorer" for explorer.exe).
    /// </summary>
    /// <exception cref="COMException"/>
    public static unsafe string GetDisplayName(string path)
    {
        // Object handled by GC
        PInvoke.SHCreateItemFromParsingName(
            path, null, out IShellItem shellItem).ThrowOnFailure();

        PWSTR displayName = default;
        try
        {
            shellItem.GetDisplayName(SIGDN.SIGDN_NORMALDISPLAY, out displayName)
                .ThrowOnFailure();
            return displayName.ToString();
        }
        finally
        {
            if (displayName.Value is not null)
                PInvoke.CoTaskMemFree(displayName);
        }
    }
}
