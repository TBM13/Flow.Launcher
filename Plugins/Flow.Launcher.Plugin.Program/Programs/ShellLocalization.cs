using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Flow.Launcher.Plugin.Program.Programs
{
    // From PT Run
    /// <summary>
    /// Class to get localized name of shell items like 'My computer'. The localization is based on the 'windows display language'.
    /// Reused code from https://stackoverflow.com/questions/41423491/how-to-get-localized-name-of-known-folder for the method <see cref="GetLocalizedName"/>
    /// </summary>
    public static class ShellLocalization
    {
        /// <summary>
        /// Returns the localized name of a shell item.
        /// </summary>
        /// <param name="path">Path to the shell item (e. g. shortcut 'File Explorer.lnk').</param>
        /// <returns>The localized name as string or <see cref="string.Empty"/>.</returns>
        public static unsafe string GetLocalizedName(string path)
        {
            int retCode = PInvoke.SHCreateItemFromParsingName(path, null, typeof(Windows.Win32.UI.Shell.IShellItem).GUID, out object shellItemObj);
            if (retCode != 0 || shellItemObj is not Windows.Win32.UI.Shell.IShellItem shellItem)
            {
                return string.Empty;
            }

            try
            {
                PWSTR displayName;
                shellItem.GetDisplayName(Windows.Win32.UI.Shell.SIGDN.SIGDN_NORMALDISPLAY, &displayName);
                string filename = displayName.ToString();
                PInvoke.CoTaskMemFree(displayName);
                return filename;
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                Marshal.ReleaseComObject(shellItem);
            }
        }
    }
}
