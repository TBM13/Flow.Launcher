using System.Runtime.InteropServices;
using Flow.Launcher.PluginSDK.API;
using Windows.Win32;

namespace Flow.Launcher.Interop;

/// <summary>
/// Contains methods that interact with the current application.
/// </summary>
public static partial class ApplicationHelper
{

    [LibraryImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true)]
    private static partial int SetPreferredAppMode(int appMode);

    /// <summary>
    /// Calls a hidden Windows API to enable/disable dark mode on things like context menus.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the color scheme is not recognized.</exception>
    // Inspired by https://github.com/ysc3839/win32-darkmode
    public static void SetWin32DarkMode(ColorScheme scheme)
    {
        // Undocumented API from Windows 10 1809
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            Environment.OSVersion.Version.Build >= 17763)
        {
            int appMode = scheme switch
            {
                ColorScheme.System => 1, // AllowDark,
                ColorScheme.Dark => 2, // ForceDark,
                ColorScheme.Light => 3, // ForceLight,
                _ => throw new ArgumentOutOfRangeException(nameof(scheme), scheme, null)
            };

            _ = SetPreferredAppMode(appMode);
        }
    }

    // Inspired by https://github.com/files-community/Files code on STA Thread handling.
    public static Task StartSTATaskAsync(Action action)
    {
        var taskCompletionSource = new TaskCompletionSource();
        Thread thread = new(() =>
        {
            PInvoke.OleInitialize();

            try
            {
                action();
                taskCompletionSource.SetResult();
            }
            catch (Exception ex)
            {
                taskCompletionSource.SetException(ex);
            }
            finally
            {
                PInvoke.OleUninitialize();
            }
        })
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return taskCompletionSource.Task;
    }

    public static Task<T> StartSTATaskAsync<T>(Func<T> func)
    {
        var taskCompletionSource = new TaskCompletionSource<T>();

        Thread thread = new(() =>
        {
            PInvoke.OleInitialize();

            try
            {
                taskCompletionSource.SetResult(func());
            }
            catch (Exception ex)
            {
                taskCompletionSource.SetException(ex);
            }
            finally
            {
                PInvoke.OleUninitialize();
            }
        })
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return taskCompletionSource.Task;
    }
}
