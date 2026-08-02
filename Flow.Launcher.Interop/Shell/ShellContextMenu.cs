using System.Drawing;
using System.IO;
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Interop.Shell;

/// <summary>
/// Shows the Windows Explorer shell context menu for files, folders or drives.
/// </summary>
/// <remarks>
/// Limitation: Only handles files/folders in the same directory.
/// </remarks>
// Based on code from https://www.codeproject.com/Articles/22012/Explorer-Shell-Context-Menu
// TODO: Handle IContextMenu2 and IContextMenu3
// TODO: Review code
public sealed class ShellContextMenu : IDisposable
{
    // CMIC_MASK constants - these don't seem to be exposed by CsWin32
    private const uint CMIC_MASK_UNICODE = 0x00004000;
    private const uint CMIC_MASK_PTINVOKE = 0x20000000;
    private const uint CMIC_MASK_SHIFT_DOWN = 0x10000000;
    private const uint CMIC_MASK_CONTROL_DOWN = 0x40000000;

    // CMF flags for QueryContextMenu - these don't seem to be exposed by CsWin32
    private const uint CMF_NORMAL = 0x00000000;
    private const uint CMF_EXPLORE = 0x00000004;
    private const uint CMF_EXTENDEDVERBS = 0x00000100;

    private const uint CMD_FIRST = 1;
    private const uint CMD_LAST = 30000;

    private IContextMenu? _contextMenu;
    private IShellFolder? _desktopFolder;
    private IShellFolder? _parentFolder;
    private IntPtr[]? _pidls;
    private string? _parentFolderPath;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ReleaseAll();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Shows the context menu for the specified files.
    /// </summary>
    /// <param name="files">Files to show context menu for (must be in the same directory)</param>
    /// <param name="screenPoint">Screen coordinates where to show the menu</param>
    public void ShowContextMenu(FileInfo[] files, Point screenPoint)
    {
        if (files is null || files.Length == 0) return;

        ReleaseAll();
        _pidls = GetPIDLs(files[0].DirectoryName, files, static fi => fi.Name);
        ShowContextMenuCore(screenPoint);
    }

    /// <summary>
    /// Shows the context menu for the specified directories.
    /// </summary>
    /// <param name="directories">Directories to show context menu for (must have the same parent)</param>
    /// <param name="screenPoint">Screen coordinates where to show the menu</param>
    public void ShowContextMenu(DirectoryInfo[] directories, Point screenPoint)
    {
        if (directories is null || directories.Length == 0) return;

        ReleaseAll();
        _pidls = GetPIDLs(directories[0].Parent?.FullName, directories, static di => di.Name);
        ShowContextMenuCore(screenPoint);
    }

    /// <summary>
    /// Shows the context menu for the specified drives.
    /// </summary>
    /// <param name="drives">Drives to show context menu for</param>
    /// <param name="screenPoint">Screen coordinates where to show the menu</param>
    public void ShowContextMenu(DriveInfo[] drives, Point screenPoint)
    {
        if (drives is null || drives.Length == 0) return;

        ReleaseAll();
        _pidls = GetDrivePIDLs(drives);
        ShowContextMenuCore(screenPoint);
    }

    private unsafe void ShowContextMenuCore(Point screenPoint)
    {
        if (_pidls is null || _parentFolder is null)
        {
            ReleaseAll();
            return;
        }

        HMENU menu = default;

        try
        {
            if (!TryGetContextMenu(_parentFolder, _pidls))
                return;

            menu = PInvoke.CreatePopupMenu();

            uint flags = CMF_EXPLORE | CMF_NORMAL;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                flags |= CMF_EXTENDEDVERBS;

            _contextMenu!.QueryContextMenu(menu, 0, CMD_FIRST, CMD_LAST, flags);

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
                InvokeCommand(_contextMenu, selectedCmd, _parentFolderPath!, screenPoint, ownerWindow);
        }
        finally
        {
            if (menu != HMENU.Null)
                PInvoke.DestroyMenu(menu);

            ReleaseAll();
        }
    }

    private unsafe bool TryGetContextMenu(IShellFolder parentFolder, IntPtr[] pidls)
    {
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
                    out object result);

                if (result is IContextMenu contextMenu)
                {
                    _contextMenu = contextMenu;
                    return true;
                }
            }
        }
        catch
        {
            // GetUIObjectOf throws on failure
        }

        _contextMenu = null;
        return false;
    }

    private static unsafe void InvokeCommand(
        IContextMenu contextMenu, uint cmd, string folder, Point point, HWND ownerWindow)
    {
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
                fMask = CMIC_MASK_UNICODE | CMIC_MASK_PTINVOKE |
                        (modifiers.HasFlag(ModifierKeys.Control) ? CMIC_MASK_CONTROL_DOWN : 0) |
                        (modifiers.HasFlag(ModifierKeys.Shift) ? CMIC_MASK_SHIFT_DOWN : 0),
                ptInvoke = point,
                nShow = (int)SHOW_WINDOW_CMD.SW_SHOWNORMAL
            };

            contextMenu.InvokeCommand((CMINVOKECOMMANDINFO*)&invoke);
        }
    }

    private unsafe IntPtr[]? GetPIDLs<T>(string? parentPath, T[] items, Func<T, string> getName)
    {
        if (string.IsNullOrEmpty(parentPath)) return null;

        if (!TryGetParentFolder(parentPath))
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
                    _parentFolder!.ParseDisplayName(HWND.Null, null, pName, null, &pidl, ref attrs);

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

    private unsafe IntPtr[]? GetDrivePIDLs(DriveInfo[] drives)
    {
        if (drives.Length == 0) return null;

        // Get the "My Computer" virtual folder using its shell CLSID
        const string myComputerPath = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
        if (!TryGetParentFolder(myComputerPath))
            return null;

        var pidls = new IntPtr[drives.Length];
        int allocatedCount = 0;

        try
        {
            for (int i = 0; i < drives.Length; i++)
            {
                // Use the drive root path (e.g., "C:\") for parsing
                string drivePath = drives[i].Name;
                fixed (char* pPath = drivePath)
                {
                    uint attrs = 0;
                    ITEMIDLIST* pidl = null;
                    _parentFolder!.ParseDisplayName(HWND.Null, null, pPath, null, &pidl, ref attrs);

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

    private unsafe bool TryGetParentFolder(string folderPath)
    {
        if (_parentFolder is not null) return true;

        IShellFolder desktop = GetDesktopFolder();

        fixed (char* pPath = folderPath)
        {
            ITEMIDLIST* pidl = null;
            uint attrs = 0;

            desktop.ParseDisplayName(HWND.Null, null, pPath, null, &pidl, ref attrs);
            if (pidl == null) return false;

            try
            {
                // Get display name for the folder
                STRRET strRet = default;
                _desktopFolder!.GetDisplayNameOf(pidl, SHGDNF.SHGDN_FORPARSING, &strRet);

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

                    _parentFolderPath = buffer.ToString();
                }
                finally
                {
                    // Free STRRET if it contains a CoTaskMemAlloc'd string
                    if (strRet.uType == (uint)STRRET_TYPE.STRRET_WSTR)
                        PInvoke.CoTaskMemFree(strRet.Anonymous.pOleStr.Value);
                }

                // Get IShellFolder for the parent
                desktop.BindToObject(*pidl, null, out IShellFolder shellFolder);
                _parentFolder = shellFolder;
                return true;
            }
            finally
            {
                PInvoke.CoTaskMemFree(pidl);
            }
        }
    }

    private IShellFolder GetDesktopFolder()
    {
        if (_desktopFolder is null)
        {
            PInvoke.SHGetDesktopFolder(out IShellFolder folder).ThrowOnFailure();
            _desktopFolder = folder;
        }
        return _desktopFolder;
    }

    private void ReleaseAll()
    {
        if (_contextMenu is not null)
        {
            _contextMenu = null;
        }
        if (_desktopFolder is not null)
        {
            _desktopFolder = null;
        }
        if (_parentFolder is not null)
        {
            _parentFolder = null;
        }
        if (_pidls is not null)
        {
            FreePIDLs(_pidls, _pidls.Length);
            _pidls = null;
        }
        _parentFolderPath = null;
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
