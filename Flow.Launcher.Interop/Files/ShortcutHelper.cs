using System.IO;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.System.Variant;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Flow.Launcher.Interop.Files;

/// <summary>
/// Helper class for interacting with Shell Link (.lnk) files.
/// </summary>
public static class ShortcutHelper
{
    public const string SHELL_LINK_EXTENSION = ".lnk";

    /// <summary>
    /// Creates a ShellLink COM object, loads the .lnk file, and returns the <see cref="IShellLinkW"/> interface.
    /// </summary>
    /// <exception cref="COMException"></exception>
    private static IShellLinkW LoadShellLink(string path)
    {
        // Object managed by GC
        IShellLinkW link = ShellLink.CreateInstance<IShellLinkW>();
        ((IPersistFile)link).Load(path, STGM.STGM_READ);
        return link;
    }

    /// <exception cref="PathTooLongException"></exception>
    /// <exception cref="COMException"></exception>
    public static unsafe string RetrieveTargetPath(string path)
    {
        IShellLinkW link = LoadShellLink(path);
        // SLR_NO_UI: don't show any UI during resolution (e.g. "Problem with Shortcut" dialogs)
        // SLR_NOTRACK & SLR_NOSEARCH: don't search for the target if it's missing. This can be
        //   super slow, specially for network paths.
        // https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishelllinka-resolve
        link.Resolve(HWND.Null, (uint)(SLR_FLAGS.SLR_NO_UI | SLR_FLAGS.SLR_NOTRACK | SLR_FLAGS.SLR_NOSEARCH));

        char* buffer = stackalloc char[(int)PInvoke.MAX_PATH];
        link.GetPath(buffer, (int)PInvoke.MAX_PATH, null, 0);

        string targetPath = new string(buffer);
        if (targetPath.Length >= PInvoke.MAX_PATH - 1)
            throw new PathTooLongException("Target path is too long");

        return targetPath;
    }

    /// <summary>
    /// Gets the description and command-line arguments associated with a Shell Link (.lnk) file.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="COMException"></exception>
    public static unsafe (string description, string args) RetrieveDescriptionAndArgs(string path)
    {
        IShellLinkW link = LoadShellLink(path);

        char* descriptionBuffer = stackalloc char[(int)PInvoke.INFOTIPSIZE];
        link.GetDescription(descriptionBuffer, (int)PInvoke.INFOTIPSIZE);
        string description = new(descriptionBuffer);

        string args;
        PROPERTYKEY pKey = PInvoke.PKEY_Link_Arguments;
        PROPVARIANT propVar = default;
        try
        {
            ((IPropertyStore)link).GetValue(in pKey, out propVar);
            args = propVar.Anonymous.Anonymous.vt switch
            {
                VARENUM.VT_EMPTY => string.Empty,
                VARENUM.VT_LPWSTR => propVar.Anonymous.Anonymous.Anonymous.pwszVal.ToString(),
                _ => throw new InvalidOperationException(
                    $"Unexpected variant type for ShellLink args: {propVar.Anonymous.Anonymous.vt}")
            };
        }
        finally
        {
            PInvoke.PropVariantClear(ref propVar);
        }

        return (description, args);
    }
}
