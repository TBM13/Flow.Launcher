using System.Drawing;
using System.IO;
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Interop.Shell;

// TODO: Handle IContextMenu2 and IContextMenu3
public static class ShellContextMenu
{
    private const uint CMD_FIRST = 1;
    private const uint CMD_LAST = 30000;

    /// <summary>
    /// Shows the Windows Explorer shell context menu for the given files.
    /// </summary>
    /// <param name="files">Files to show context menu for (must be in the same directory)</param>
    /// <param name="screenPoint">Screen coordinates where to show the menu</param>
    /// <param name="showExtendedMenu">Whether to show the extended context menu</param>
    public static void ShowContextMenu(FileInfo[] files, Point screenPoint, bool showExtendedMenu)
    {
        if (files.Length == 0)
            return;

        ShowContextMenu(files[0].DirectoryName, files, static fi => fi.Name, screenPoint, showExtendedMenu);
    }

    /// <summary>
    /// Shows the Windows Explorer shell context menu for the given directories.
    /// </summary>
    /// <param name="directories">Directories to show context menu for (must have the same parent)</param>
    /// <param name="screenPoint">Screen coordinates where to show the menu</param>
    /// <param name="showExtendedMenu">Whether to show the extended context menu</param>
    public static void ShowContextMenu(DirectoryInfo[] directories, Point screenPoint, bool showExtendedMenu)
    {
        if (directories.Length == 0)
            return;

        ShowContextMenu(directories[0].Parent?.FullName, directories, static di => di.Name, screenPoint, showExtendedMenu);
    }

    /// <summary>
    /// Shows the Windows Explorer shell context menu for the given drives.
    /// </summary>
    /// <param name="drives">Drives to show context menu for</param>
    /// <param name="screenPoint">Screen coordinates where to show the menu</param>
    /// <param name="showExtendedMenu">Whether to show the extended context menu</param>
    public static void ShowContextMenu(DriveInfo[] drives, Point screenPoint, bool showExtendedMenu)
    {
        if (drives.Length == 0)
            return;

        string myComputerPath = $"::{PInvoke.CLSID_MyComputer:B}";
        ShowContextMenu(myComputerPath, drives, static drive => drive.Name, screenPoint, showExtendedMenu);
    }

    private static unsafe void ShowContextMenu<T>(
        string? parentPath, T[] items, Func<T, string> getName, Point screenPoint, bool showExtendedMenu)
    {
        if (items.Length == 0)
            return;

        IShellFolder? desktopFolder = null;
        IShellFolder? parentFolder = null;
        IContextMenu? contextMenu = null;
        IntPtr[]? pidls = null;
        string? parentFolderPath = null;
        HMENU menu = default;

        try
        {
            pidls = GetPIDLs(
                ref desktopFolder, ref parentFolder, ref parentFolderPath, parentPath, items, getName);
            if (pidls is null || parentFolder is null ||
                !TryGetContextMenu(parentFolder, pidls, out contextMenu))
                return;

            menu = PInvoke.CreatePopupMenu();

            uint flags = PInvoke.CMF_EXPLORE | PInvoke.CMF_NORMAL;
            if (showExtendedMenu)
                flags |= PInvoke.CMF_EXTENDEDVERBS;

            contextMenu!.QueryContextMenu(menu, 0, CMD_FIRST, CMD_LAST, flags)
                .ThrowOnFailure();

            // Use the foreground window as owner for the popup menu
            HWND ownerWindow = PInvoke.GetForegroundWindow();

            uint selectedCmd = (uint)PInvoke.TrackPopupMenuEx(
                menu,
                (uint)TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD,
                screenPoint.X,
                screenPoint.Y,
                ownerWindow,
                null).Value;

            PInvoke.DestroyMenu(menu);
            menu = HMENU.Null;

            if (selectedCmd != 0)
                InvokeCommand(contextMenu, selectedCmd, parentFolderPath!, screenPoint, ownerWindow);
        }
        finally
        {
            if (menu != HMENU.Null)
                PInvoke.DestroyMenu(menu);

            if (pidls is not null)
                FreePIDLs(pidls, pidls.Length);
        }
    }

    private static unsafe bool TryGetContextMenu(
        IShellFolder parentFolder, IntPtr[] pidls, out IContextMenu? contextMenu)
    {
        contextMenu = null;
        try
        {
            fixed (IntPtr* pPIDLs = pidls)
            {
                Guid iid = typeof(IContextMenu).GUID;
                parentFolder.GetUIObjectOf(
                    HWND.Null,
                    (uint)pidls.Length,
                    (ITEMIDLIST**)pPIDLs,
                    &iid,
                    null,
                    out object result).ThrowOnFailure();

                if (result is IContextMenu resultContextMenu)
                {
                    contextMenu = resultContextMenu;
                    return true;
                }
            }
        }
        catch
        {
            // GetUIObjectOf throws on failure
        }

        return false;
    }

    private static unsafe void InvokeCommand(
        IContextMenu contextMenu, uint cmd, string folder, Point point, HWND ownerWindow)
    {
        if (cmd < CMD_FIRST || cmd > CMD_LAST)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Command ID is out of range");

        fixed (char* pFolder = folder)
        {
            var cmdOffset = (nuint)(cmd - CMD_FIRST);
            var modifiers = Keyboard.Modifiers;
            CMINVOKECOMMANDINFOEX invoke = new()
            {
                cbSize = (uint)sizeof(CMINVOKECOMMANDINFOEX),
                hwnd = ownerWindow,
                lpVerb = new PCSTR((byte*)cmdOffset),
                lpVerbW = new PCWSTR((char*)cmdOffset),
                lpDirectoryW = pFolder,
                fMask = PInvoke.SEE_MASK_UNICODE | PInvoke.CMIC_MASK_PTINVOKE |
                        (modifiers.HasFlag(ModifierKeys.Control) ? PInvoke.CMIC_MASK_CONTROL_DOWN : 0) |
                        (modifiers.HasFlag(ModifierKeys.Shift) ? PInvoke.CMIC_MASK_SHIFT_DOWN : 0),
                ptInvoke = point,
                nShow = (int)SHOW_WINDOW_CMD.SW_SHOWNORMAL
            };

            contextMenu.InvokeCommand((CMINVOKECOMMANDINFO*)&invoke).ThrowOnFailure();
        }
    }

    private static unsafe IntPtr[]? GetPIDLs<T>(
        ref IShellFolder? desktopFolder,
        ref IShellFolder? parentFolder,
        ref string? parentFolderPath,
        string? parentPath,
        T[] items,
        Func<T, string> getName)
    {
        if (string.IsNullOrEmpty(parentPath)) return null;

        if (!TryGetParentFolder(ref desktopFolder, ref parentFolder, ref parentFolderPath, parentPath))
            return null;

        var pidls = new IntPtr[items.Length];
        int allocatedCount = 0;

        try
        {
            for (int i = 0; i < items.Length; i++)
            {
                string name = getName(items[i]);
                fixed (char* pName = name)
                {
                    uint attrs = 0;
                    ITEMIDLIST* pidl = null;
                    parentFolder!.ParseDisplayName(HWND.Null, null, pName, null, &pidl, ref attrs)
                        .ThrowOnFailure();

                    if (pidl == null)
                    {
                        FreePIDLs(pidls, allocatedCount);
                        return null;
                    }
                    pidls[i] = (IntPtr)pidl;
                    allocatedCount++;
                }
            }

            return pidls;
        }
        catch
        {
            FreePIDLs(pidls, allocatedCount);
            throw;
        }
    }

    private static unsafe bool TryGetParentFolder(
        ref IShellFolder? desktopFolder,
        ref IShellFolder? parentFolder,
        ref string? parentFolderPath,
        string folderPath)
    {
        if (parentFolder is not null) return true;

        IShellFolder desktop = GetDesktopFolder(ref desktopFolder);

        fixed (char* pPath = folderPath)
        {
            ITEMIDLIST* pidl = null;
            uint attrs = 0;

            desktop.ParseDisplayName(HWND.Null, null, pPath, null, &pidl, ref attrs)
                .ThrowOnFailure();
            if (pidl == null) return false;

            try
            {
                // Get display name for the folder
                STRRET strRet = default;
                desktopFolder!.GetDisplayNameOf(pidl, SHGDNF.SHGDN_FORPARSING, &strRet)
                    .ThrowOnFailure();

                try
                {
                    Span<char> buffer = stackalloc char[(int)PInvoke.MAX_PATH];
                    PInvoke.StrRetToBuf(ref strRet, null, buffer).ThrowOnFailure();

                    int nullIndex = buffer.IndexOf('\0');
                    if (nullIndex >= 0)
                        buffer = buffer[..nullIndex];
                    if (buffer.Length >= PInvoke.MAX_PATH - 1)
                        throw new PathTooLongException(
                            "The parent folder path exceeds the maximum allowed length");

                    parentFolderPath = buffer.ToString();
                }
                finally
                {
                    // Free STRRET if it contains a CoTaskMemAlloc'd string
                    if (strRet.uType == (uint)STRRET_TYPE.STRRET_WSTR)
                        PInvoke.CoTaskMemFree(strRet.Anonymous.pOleStr.Value);
                }

                // Get IShellFolder for the parent
                desktop.BindToObject(*pidl, null, out IShellFolder shellFolder)
                    .ThrowOnFailure();
                parentFolder = shellFolder;
                return true;
            }
            finally
            {
                PInvoke.CoTaskMemFree(pidl);
            }
        }
    }

    private static IShellFolder GetDesktopFolder(ref IShellFolder? desktopFolder)
    {
        if (desktopFolder is null)
        {
            PInvoke.SHGetDesktopFolder(out IShellFolder folder).ThrowOnFailure();
            desktopFolder = folder;
        }
        return desktopFolder;
    }

    private static unsafe void FreePIDLs(IntPtr[] pidls, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (pidls[i] != IntPtr.Zero)
            {
                PInvoke.CoTaskMemFree(pidls[i].ToPointer());
                pidls[i] = IntPtr.Zero;
            }
        }
    }
}
