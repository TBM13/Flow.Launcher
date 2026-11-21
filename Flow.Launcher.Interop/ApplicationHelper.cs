using System.Runtime.InteropServices;

namespace Flow.Launcher.Interop;

public enum AppMode
{
    /// <summary>
    /// Follow whatever theme the system is using.
    /// </summary>
    AllowDark = 1,
    ForceDark = 2,
    ForceLight = 3
}

/// <summary>
/// Contains methods that interact with the current application.
/// </summary>
public static partial class ApplicationHelper
{
    [LibraryImport("uxtheme.dll", EntryPoint = "#135")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool AllowDarkModeForApp([MarshalAs(UnmanagedType.I1)] bool allow);

    [LibraryImport("uxtheme.dll", EntryPoint = "#135")]
    private static partial int SetPreferredAppMode(int appMode);

    [LibraryImport("uxtheme.dll", EntryPoint = "#136")]
    private static partial void FlushMenuThemes();

    /// <summary>
    /// Calls a hidden Windows API to enable/disable dark mode on things like context menus.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the color scheme is not recognized.</exception>
    // Inspired by https://github.com/ysc3839/win32-darkmode
    public static void SetAppMode(AppMode mode)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown app mode.");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        // Undocumented API from Windows 10 1809; SetPreferredAppMode replaced AllowDarkModeForApp in 1903 (18362).
        int build = Environment.OSVersion.Version.Build;
        if (build >= 18362)
        {
            _ = SetPreferredAppMode((int)mode);
            FlushMenuThemes();
        }
        else if (build >= 17763)
        {
            _ = AllowDarkModeForApp(mode != AppMode.ForceLight);
            FlushMenuThemes();
        }
    }
}
