using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Flow.Launcher.Infrastructure.Logger;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.UI.Shell;

namespace Flow.Launcher.Plugin.Explorer.Helper
{
    public static class ShellLinkHelper
    {
        // Reference : http://www.pinvoke.net/default.aspx/Interfaces.IShellLinkW
        [ComImport(), Guid("00021401-0000-0000-C000-000000000046")]
        public class ShellLink
        {
        }

        // Retrieve the target path using Shell Link
        public static unsafe string retrieveTargetPath(string path)
        {
            var link = new ShellLink();
            const int STGM_READ = 0;
            ((IPersistFile)link).Load(path, STGM_READ);
            var hwnd = new HWND(IntPtr.Zero);
            // Use SLR_NO_UI to avoid showing any UI during resolution, like Problem with Shortcut dialogs
            // https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishelllinka-resolve
            ((IShellLinkW)link).Resolve(hwnd, (uint)SLR_FLAGS.SLR_NO_UI);

            const int MAX_PATH = 260;
            Span<char> buffer = stackalloc char[MAX_PATH];

            var data = new WIN32_FIND_DATAW();
            var target = string.Empty;
            try
            {
                fixed (char* bufferPtr = buffer)
                {
                    ((IShellLinkW)link).GetPath((PWSTR)bufferPtr, MAX_PATH, &data, (uint)SLGP_FLAGS.SLGP_SHORTPATH);
                    target = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(bufferPtr).ToString();
                }
            }
            catch (COMException e)
            {
                Log.Exception(typeof(ShellLinkHelper).FullName, $"|IShellLinkW|retrieveTargetPath|{path}" +
                    "|Error occurred while getting program arguments", e);
            }

            // To release unmanaged memory
            Marshal.ReleaseComObject(link);

            return target;
        }
    }
}
