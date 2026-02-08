using System;
using System.Runtime.InteropServices;
using Flow.Launcher.Infrastructure.Logger;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using ZLogger;

namespace Flow.Launcher.Infrastructure.Helpers;

public static class ShellLinkHelper
{
    public const string SHELL_LINK_EXTENSION = ".lnk";
    private static readonly ILogger Logger = LogManager.GetLogger(nameof(ShellLinkHelper));

    /// <summary>
    /// Creates a ShellLink COM object, loads the .lnk file, and returns the <see cref="IShellLinkW"/> interface.
    /// <para/>
    /// Caller must release the returned object via <see cref="Marshal.ReleaseComObject"/> when done.
    /// </summary>
    private static IShellLinkW? LoadShellLink(string path)
    {
        var link = new ShellLink();
        try
        {
            ((IPersistFile)link).Load(path, STGM.STGM_READ);
            return (IShellLinkW)link;
        }
        catch (COMException e)
        {
            Logger.ZLogError(e, $"Failed to load shell link from path: {path}");
            if (Marshal.IsComObject(link))
                Marshal.ReleaseComObject(link);

            return null;
        }
    }

    public static unsafe string? RetrieveTargetPath(string path)
    {
        var link = LoadShellLink(path);
        if (link is null)
            return null;

        try
        {
            // SLR_NO_UI: avoid showing any UI during resolution (e.g. "Problem with Shortcut" dialogs)
            // https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishelllinka-resolve
            link.Resolve(HWND.Null, (uint)SLR_FLAGS.SLR_NO_UI);

            Span<char> buffer = stackalloc char[(int)PInvoke.MAX_PATH];
            var data = new WIN32_FIND_DATAW();
            fixed (char* bufferPtr = buffer)
            {
                link.GetPath(bufferPtr, (int)PInvoke.MAX_PATH, &data, 0);
                return MemoryMarshal.CreateReadOnlySpanFromNullTerminated(bufferPtr).ToString();
            }
        }
        catch (COMException e)
        {
            Logger.ZLogError(e, $"Failed to retrieve target path from shell link: {path}");
            return null;
        }
        finally
        {
            if (Marshal.IsComObject(link))
                Marshal.ReleaseComObject(link);
        }
    }

    public static unsafe (string? description, string? args) RetrieveDescriptionAndArgs(string path)
    {
        var link = LoadShellLink(path);
        if (link is null)
            return (null, null);

        try
        {
            Span<char> buffer = stackalloc char[(int)PInvoke.MAX_PATH];
            string? description = null;
            string? args = null;

            try
            {
                fixed (char* bufferPtr = buffer)
                {
                    link.GetDescription(bufferPtr, (int)PInvoke.MAX_PATH);
                    description = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(bufferPtr).ToString();
                }
            }
            catch (COMException e)
            {
                // C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\MiracastView.lnk always causes an exception
                Logger.ZLogError(e, $"Failed to get description from shell link: {path}");
            }

            // Clear buffer to avoid bleeding of data
            buffer.Clear();

            try
            {
                fixed (char* bufferPtr = buffer)
                {
                    link.GetArguments(bufferPtr, (int)PInvoke.MAX_PATH);
                    args = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(bufferPtr).ToString();
                }
            }
            catch (COMException e)
            {
                Logger.ZLogError(e, $"Failed to get args from shell link: {path}");
            }

            return (description, args);
        }
        finally
        {
            if (Marshal.IsComObject(link))
                Marshal.ReleaseComObject(link);
        }
    }
}
