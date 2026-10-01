using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace iNKORE.UI.WPF.Modern.Native;

/// <summary>
/// This header is used by multiple technologies.
/// </summary>
internal static class User32
{
    /// <summary>
    /// Retrieves information about the specified window.
    /// The function also retrieves the 32-bit (DWORD) value at the specified offset into the extra window memory.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    /// <summary>
    /// Changes an attribute of the specified window.
    /// The function also sets the 32-bit (long) value at the specified offset into the extra window memory.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("USER32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern bool SetWindowPos(HWND hWnd, HWND hWndInsertAfter, int X, int Y, int cx, int cy, SET_WINDOW_POS_FLAGS uFlags);

    [DllImport("USER32.dll", ExactSpelling = true, EntryPoint = "FindWindowW", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern HWND FindWindow(PCWSTR lpClassName, PCWSTR lpWindowName);

    [DllImport("USER32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int GetSystemMetrics(SYSTEM_METRICS_INDEX nIndex);

    /// <summary>Destroys an icon and frees any memory the icon occupied.</summary>
    /// <param name="hIcon">
    /// <para>Type: <b>HICON</b> A handle to the icon to be destroyed. The icon must not be in use.</para>
    /// <para><see href="https://docs.microsoft.com/windows/win32/api//winuser/nf-winuser-destroyicon#parameters">Read more on docs.microsoft.com</see>.</para>
    /// </param>
    /// <returns>
    /// <para>Type: <b>BOOL</b> If the function succeeds, the return value is nonzero. If the function fails, the return value is zero. To get extended error information, call <a href="/windows/desktop/api/errhandlingapi/nf-errhandlingapi-getlasterror">GetLastError</a>.</para>
    /// </returns>
    /// <remarks>
    /// <para><see href="https://docs.microsoft.com/windows/win32/api//winuser/nf-winuser-destroyicon">Learn more about this API from docs.microsoft.com</see>.</para>
    /// </remarks>
    [DllImport("USER32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern bool DestroyIcon(HICON hIcon);

    /// <summary>Retrieves the window handle to the active window attached to the calling thread's message queue.</summary>
    /// <returns>
    /// <para>Type: <b>HWND</b> The return value is the handle to the active window attached to the calling thread's message queue. Otherwise, the return value is <b>NULL</b>.</para>
    /// </returns>
    /// <remarks>
    /// <para><see href="https://docs.microsoft.com/windows/win32/api//winuser/nf-winuser-getactivewindow">Learn more about this API from docs.microsoft.com</see>.</para>
    /// </remarks>
    [DllImport("USER32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern HWND GetActiveWindow();
}
