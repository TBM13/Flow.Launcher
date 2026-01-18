using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Plugin.Explorer.Helper
{
    /// <summary>
    /// Shows the Windows Explorer shell context menu for files or folders.
    /// Based on code from https://www.codeproject.com/Articles/22012/Explorer-Shell-Context-Menu
    /// </summary>
    /// <remarks>
    /// Limitation: Only handles files/folders in the same directory.
    /// </remarks>
    public sealed class ShellContextMenu : IDisposable
    {
        // CMIC_MASK constants - C preprocessor defines not exposed by CsWin32
        private const uint CMIC_MASK_UNICODE = 0x00004000;
        private const uint CMIC_MASK_PTINVOKE = 0x20000000;
        private const uint CMIC_MASK_SHIFT_DOWN = 0x10000000;
        private const uint CMIC_MASK_CONTROL_DOWN = 0x40000000;

        // CMF flags for QueryContextMenu - C preprocessor defines not exposed by CsWin32
        private const uint CMF_NORMAL = 0x00000000;
        private const uint CMF_EXPLORE = 0x00000004;
        private const uint CMF_EXTENDEDVERBS = 0x00000100;

        private const int MAX_PATH = 260;
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

        private unsafe void ShowContextMenuCore(Point screenPoint)
        {
            if (_pidls is null || _parentFolder is null)
            {
                ReleaseAll();
                return;
            }

            IntPtr contextMenuPtr = IntPtr.Zero;
            HMENU menu = default;

            try
            {
                if (!TryGetContextMenu(_parentFolder, _pidls, out contextMenuPtr))
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
                    InvokeCommand(_contextMenu, selectedCmd, _parentFolderPath!, screenPoint);
            }
            finally
            {
                if (menu != HMENU.Null)
                    PInvoke.DestroyMenu(menu);

                if (contextMenuPtr != IntPtr.Zero)
                    Marshal.Release(contextMenuPtr);

                ReleaseAll();
            }
        }

        private unsafe bool TryGetContextMenu(IShellFolder parentFolder, IntPtr[] pidls, out IntPtr contextMenuPtr)
        {
            contextMenuPtr = IntPtr.Zero;

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
                        contextMenuPtr = Marshal.GetIUnknownForObject(result);
                        return true;
                    }

                    // If result is not IContextMenu but is a COM object, release it
                    if (result is not null)
                        Marshal.ReleaseComObject(result);
                }
            }
            catch
            {
                // GetUIObjectOf throws on failure
            }

            _contextMenu = null;
            return false;
        }

        private static unsafe void InvokeCommand(IContextMenu contextMenu, uint cmd, string folder, Point point)
        {
            fixed (char* pFolder = folder)
            {
                var cmdOffset = (nuint)(cmd - CMD_FIRST);
                var modifiers = Keyboard.Modifiers;
                CMINVOKECOMMANDINFOEX invoke = new()
                {
                    cbSize = (uint)sizeof(CMINVOKECOMMANDINFOEX),
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

        private unsafe bool TryGetParentFolder(string folderPath)
        {
            if (_parentFolder is not null) return true;

            IShellFolder desktop = GetDesktopFolder();

            fixed (char* pPath = folderPath)
            {
                Guid iid = typeof(IShellFolder).GUID;
                ITEMIDLIST* pidl = null;
                uint attrs = 0;

                desktop.ParseDisplayName(HWND.Null, null, pPath, null, &pidl, ref attrs);
                if (pidl == null) return false;

                try
                {
                    // Get display name for the folder
                    STRRET strRet = default;
                    _desktopFolder!.GetDisplayNameOf(pidl, SHGDNF.SHGDN_FORPARSING, &strRet);
                    Span<char> buffer = stackalloc char[MAX_PATH];
                    PInvoke.StrRetToBuf(ref strRet, null, buffer);
                    _parentFolderPath = buffer.TrimEnd('\0').ToString();

                    // Get IShellFolder for the parent
                    desktop.BindToObject(pidl, null, &iid, out object result);
                    _parentFolder = (IShellFolder)result;
                    return true;
                }
                finally
                {
                    Marshal.FreeCoTaskMem((IntPtr)pidl);
                }
            }
        }

        private IShellFolder GetDesktopFolder()
        {
            if (_desktopFolder is null)
            {
                int hr = PInvoke.SHGetDesktopFolder(out IShellFolder folder);
                if (HRESULT.S_OK != hr)
                    throw new COMException("Failed to get desktop shell folder", hr);
                _desktopFolder = folder;
            }
            return _desktopFolder;
        }

        private void ReleaseAll()
        {
            if (_contextMenu is not null)
            {
                Marshal.ReleaseComObject(_contextMenu);
                _contextMenu = null;
            }
            if (_desktopFolder is not null)
            {
                Marshal.ReleaseComObject(_desktopFolder);
                _desktopFolder = null;
            }
            if (_parentFolder is not null)
            {
                Marshal.ReleaseComObject(_parentFolder);
                _parentFolder = null;
            }
            if (_pidls is not null)
            {
                FreePIDLs(_pidls, _pidls.Length);
                _pidls = null;
            }
            _parentFolderPath = null;
        }

        private static void FreePIDLs(IntPtr[] pidls, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (pidls[i] != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(pidls[i]);
                    pidls[i] = IntPtr.Zero;
                }
            }
        }
    }
}
