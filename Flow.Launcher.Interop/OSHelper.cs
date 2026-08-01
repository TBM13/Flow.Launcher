using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Shutdown;

namespace Flow.Launcher.Interop;

public static class OSHelper
{
    /// <summary>
    /// Planned shutdown caused by an application (us)
    /// </summary>
    private const SHUTDOWN_REASON REASON =
        SHUTDOWN_REASON.SHTDN_REASON_MAJOR_APPLICATION
        | SHUTDOWN_REASON.SHTDN_REASON_MINOR_OTHER
        | SHUTDOWN_REASON.SHTDN_REASON_FLAG_PLANNED;

    /// <summary>
    /// Enables the shutdown privilege for the current process.
    /// </summary>
    /// <exception cref="Win32Exception"></exception>
    private static unsafe void EnableShutdownPrivilege()
    {
        HANDLE tokenHandle = default;
        try
        {
            // Get the current process token
            if (!PInvoke.OpenProcessToken(
                PInvoke.GetCurrentProcess(),
                TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | TOKEN_ACCESS_MASK.TOKEN_QUERY,
                &tokenHandle))
                throw new Win32Exception(Marshal.GetLastPInvokeError());

            // Enable the shutdown privilege
            if (!PInvoke.LookupPrivilegeValue(null, PInvoke.SE_SHUTDOWN_NAME, out LUID luid))
                throw new Win32Exception(Marshal.GetLastPInvokeError());

            TOKEN_PRIVILEGES privileges = new()
            {
                PrivilegeCount = 1,
                Privileges = new()
                {
                    e0 = new LUID_AND_ATTRIBUTES
                    {
                        Luid = luid,
                        Attributes = TOKEN_PRIVILEGES_ATTRIBUTES.SE_PRIVILEGE_ENABLED
                    }
                }
            };

            Marshal.SetLastPInvokeError(0);
            if (!PInvoke.AdjustTokenPrivileges(tokenHandle, false, &privileges, 0))
                throw new Win32Exception(Marshal.GetLastPInvokeError());

            // AdjustTokenPrivileges might return true even if the privilege was not enabled
            int error = Marshal.GetLastPInvokeError();
            if (error != 0)
                throw new Win32Exception(error);
        }
        finally
        {
            if (!tokenHandle.IsNull)
                PInvoke.CloseHandle(tokenHandle);
        }
    }

    /// <summary>
    /// Shuts down the computer, performing a full (not hybrid) shutdown.
    /// </summary>
    /// <exception cref="Win32Exception"></exception>
    public static void Shutdown()
    {
        EnableShutdownPrivilege();
        if (!PInvoke.ExitWindowsEx(
            EXIT_WINDOWS_FLAGS.EWX_SHUTDOWN | EXIT_WINDOWS_FLAGS.EWX_POWEROFF,
            REASON))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    /// <summary>
    /// Restarts the computer.
    /// </summary>
    /// <param name="advancedBootOptions">If true, the computer will restart into advanced boot options.</param>
    /// <exception cref="Win32Exception"></exception>
    public static void Restart(bool advancedBootOptions = false)
    {
        EnableShutdownPrivilege();

        EXIT_WINDOWS_FLAGS flags = EXIT_WINDOWS_FLAGS.EWX_REBOOT;
        if (advancedBootOptions)
            flags |= EXIT_WINDOWS_FLAGS.EWX_BOOTOPTIONS;

        if (!PInvoke.ExitWindowsEx(flags, REASON))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    /// <summary>
    /// Suspends the computer, either to sleep or hibernate mode.
    /// </summary>
    /// <exception cref="Win32Exception"></exception>
    public static void Suspend(bool hibernate = false)
    {
        EnableShutdownPrivilege();
        if (!PInvoke.SetSuspendState(hibernate, false, false))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    /// <summary>
    /// Logs off the current user.
    /// </summary>
    /// <exception cref="Win32Exception"></exception>
    public static void LogOff()
    {
        // Doesn't require the shutdown privilege
        if (!PInvoke.ExitWindowsEx(EXIT_WINDOWS_FLAGS.EWX_LOGOFF, REASON))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    /// <summary>
    /// Locks the workstation, requiring the user to log in again to unlock it.
    /// </summary>
    /// <exception cref="Win32Exception"></exception>
    public static void Lock()
    {
        if (!PInvoke.LockWorkStation())
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
}
