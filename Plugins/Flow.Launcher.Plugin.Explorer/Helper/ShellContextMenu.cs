using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Flow.Launcher.Plugin.Explorer.Helper
{
    //  Code from https://www.codeproject.com/Articles/22012/Explorer-Shell-Context-Menu:
    /// <summary>
    /// "Stand-alone" shell context menu
    /// 
    /// It isn't really debugged but is mostly working.
    /// Create an instance and call ShowContextMenu with a list of FileInfo for the files.
    /// Limitation is that it only handles files in the same directory but it can be fixed
    /// by changing the way files are translated into PIDLs.
    /// 
    /// Based on FileBrowser in C# from CodeProject
    /// http://www.codeproject.com/useritems/FileBrowser.asp
    /// 
    /// Hooking class taken from MSDN Magazine Cutting Edge column
    /// http://msdn.microsoft.com/msdnmag/issues/02/10/CuttingEdge/
    /// 
    /// Andreas Johansson
    /// afjohansson@hotmail.com
    /// http://afjohansson.spaces.live.com
    /// </summary>
    /// <example>
    ///    ShellContextMenu scm = new ShellContextMenu();
    ///    FileInfo[] files = new FileInfo[1];
    ///    files[0] = new FileInfo(@"c:\windows\notepad.exe");
    ///    scm.ShowContextMenu(this.Handle, files, Cursor.Position);
    /// </example>
    public class ShellContextMenu : NativeWindow
    {
        private IContextMenu? _oContextMenu;
        private IShellFolder? _oDesktopFolder;
        private IShellFolder? _oParentFolder;
        private IntPtr[]? _arrPIDLs;
        private string? _strParentFolder;

        public ShellContextMenu()
        {
            CreateHandle(new CreateParams());
        }

        ~ShellContextMenu()
        {
            ReleaseAll();
        }

        #region GetContextMenuInterfaces()

        /// <summary>Gets the interfaces to the context menu</summary>
        /// <param name="oParentFolder">Parent folder</param>
        /// <param name="arrPIDLs">PIDLs</param>
        /// <returns>true if it got the interfaces, otherwise false</returns>
        private bool GetContextMenuInterfaces(IShellFolder oParentFolder, IntPtr[] arrPIDLs, out IntPtr ctxMenuPtr)
        {
            int nResult = oParentFolder.GetUIObjectOf(
                IntPtr.Zero,
                (uint)arrPIDLs.Length,
                arrPIDLs,
                ref IID_IContextMenu,
                IntPtr.Zero,
                out ctxMenuPtr);

            if (S_OK == nResult)
            {
                _oContextMenu = (IContextMenu)Marshal.GetTypedObjectForIUnknown(ctxMenuPtr, typeof(IContextMenu));

                return true;
            }
            else
            {
                ctxMenuPtr = IntPtr.Zero;
                _oContextMenu = null;
                return false;
            }
        }

        #endregion

        #region InvokeCommand

        private static void InvokeCommand(IContextMenu oContextMenu, uint nCmd, string strFolder, Point pointInvoke)
        {
            CMINVOKECOMMANDINFOEX invoke = new CMINVOKECOMMANDINFOEX
            {
                cbSize = cbInvokeCommand,
                lpVerb = (IntPtr)(nCmd - CMD_FIRST),
                lpDirectory = strFolder,
                lpVerbW = (IntPtr)(nCmd - CMD_FIRST),
                lpDirectoryW = strFolder,
                fMask = CMIC.UNICODE | CMIC.PTINVOKE |
                               ((Control.ModifierKeys & Keys.Control) != 0 ? CMIC.CONTROL_DOWN : 0) |
                               ((Control.ModifierKeys & Keys.Shift) != 0 ? CMIC.SHIFT_DOWN : 0),
                ptInvoke = new Point(pointInvoke.X, pointInvoke.Y),
                nShow = SW.SHOWNORMAL
            };

            oContextMenu.InvokeCommand(ref invoke);
        }

        #endregion

        #region ReleaseAll()

        /// <summary>
        /// Release all allocated interfaces, PIDLs 
        /// </summary>
        private void ReleaseAll()
        {
            if (null != _oContextMenu)
            {
                Marshal.ReleaseComObject(_oContextMenu);
                _oContextMenu = null;
            }
            if (null != _oDesktopFolder)
            {
                Marshal.ReleaseComObject(_oDesktopFolder);
                _oDesktopFolder = null;
            }
            if (null != _oParentFolder)
            {
                Marshal.ReleaseComObject(_oParentFolder);
                _oParentFolder = null;
            }
            if (null != _arrPIDLs)
            {
                FreePIDLs(_arrPIDLs);
                _arrPIDLs = null;
            }
        }

        #endregion

        #region GetDesktopFolder()

        /// <summary>
        /// Gets the desktop folder
        /// </summary>
        /// <returns>IShellFolder for desktop folder</returns>
        private IShellFolder GetDesktopFolder()
        {
            IntPtr pUnkownDesktopFolder = IntPtr.Zero;

            if (null == _oDesktopFolder)
            {
                // Get desktop IShellFolder
                int nResult = SHGetDesktopFolder(out pUnkownDesktopFolder);
                if (S_OK != nResult)
                {
                    throw new Exception("Failed to get the desktop shell folder");
                }
                _oDesktopFolder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(pUnkownDesktopFolder, typeof(IShellFolder));
            }

            return _oDesktopFolder;
        }

        #endregion

        #region GetParentFolder()

        /// <summary>
        /// Gets the parent folder
        /// </summary>
        /// <param name="folderName">Folder path</param>
        /// <returns>IShellFolder for the folder (relative from the desktop)</returns>
        private IShellFolder? GetParentFolder(string folderName)
        {
            if (null == _oParentFolder)
            {
                IShellFolder oDesktopFolder = GetDesktopFolder();
                if (null == oDesktopFolder)
                {
                    return null;
                }

                // Get the PIDL for the folder file is in
                IntPtr pPIDL = IntPtr.Zero;
                uint pchEaten = 0;
                SFGAO pdwAttributes = 0;
                int nResult = oDesktopFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, folderName, ref pchEaten, out pPIDL, ref pdwAttributes);
                if (S_OK != nResult)
                {
                    return null;
                }

                IntPtr pStrRet = Marshal.AllocCoTaskMem(MAX_PATH * 2 + 4);
                Marshal.WriteInt32(pStrRet, 0, 0);
                nResult = _oDesktopFolder.GetDisplayNameOf(pPIDL, SHGNO.FORPARSING, pStrRet);
                StringBuilder strFolder = new StringBuilder(MAX_PATH);
                StrRetToBuf(pStrRet, pPIDL, strFolder, MAX_PATH);
                Marshal.FreeCoTaskMem(pStrRet);
                pStrRet = IntPtr.Zero;
                _strParentFolder = strFolder.ToString();

                // Get the IShellFolder for folder
                IntPtr pUnknownParentFolder = IntPtr.Zero;
                nResult = oDesktopFolder.BindToObject(pPIDL, IntPtr.Zero, ref IID_IShellFolder, out pUnknownParentFolder);
                // Free the PIDL first
                Marshal.FreeCoTaskMem(pPIDL);
                if (S_OK != nResult)
                {
                    return null;
                }
                _oParentFolder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(pUnknownParentFolder, typeof(IShellFolder));
            }

            return _oParentFolder;
        }

        #endregion

        #region GetPIDLs()

        /// <summary>
        /// Get the PIDLs
        /// </summary>
        /// <param name="arrFI">Array of FileInfo</param>
        /// <returns>Array of PIDLs</returns>
        protected IntPtr[]? GetPIDLs(FileInfo[] arrFI)
        {
            if (null == arrFI || 0 == arrFI.Length)
            {
                return null;
            }

            IShellFolder? oParentFolder = GetParentFolder(arrFI[0].DirectoryName);
            if (null == oParentFolder)
            {
                return null;
            }

            IntPtr[] arrPIDLs = new IntPtr[arrFI.Length];
            int n = 0;
            foreach (FileInfo fi in arrFI)
            {
                // Get the file relative to folder
                uint pchEaten = 0;
                SFGAO pdwAttributes = 0;
                IntPtr pPIDL = IntPtr.Zero;
                int nResult = oParentFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, fi.Name, ref pchEaten, out pPIDL, ref pdwAttributes);
                if (S_OK != nResult)
                {
                    FreePIDLs(arrPIDLs);
                    return null;
                }
                arrPIDLs[n] = pPIDL;
                n++;
            }

            return arrPIDLs;
        }

        /// <summary>
        /// Get the PIDLs
        /// </summary>
        /// <param name="arrFI">Array of DirectoryInfo</param>
        /// <returns>Array of PIDLs</returns>
        protected IntPtr[]? GetPIDLs(DirectoryInfo[] arrFI)
        {
            if (null == arrFI || 0 == arrFI.Length)
            {
                return null;
            }

            IShellFolder? oParentFolder = GetParentFolder(arrFI[0].Parent!.FullName);
            if (null == oParentFolder)
            {
                return null;
            }

            IntPtr[] arrPIDLs = new IntPtr[arrFI.Length];
            int n = 0;
            foreach (DirectoryInfo fi in arrFI)
            {
                // Get the file relative to folder
                uint pchEaten = 0;
                SFGAO pdwAttributes = 0;
                IntPtr pPIDL = IntPtr.Zero;
                int nResult = oParentFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, fi.Name, ref pchEaten, out pPIDL, ref pdwAttributes);
                if (S_OK != nResult)
                {
                    FreePIDLs(arrPIDLs);
                    return null;
                }
                arrPIDLs[n] = pPIDL;
                n++;
            }

            return arrPIDLs;
        }

        #endregion

        #region FreePIDLs()

        /// <summary>
        /// Free the PIDLs
        /// </summary>
        /// <param name="arrPIDLs">Array of PIDLs (IntPtr)</param>
        protected static void FreePIDLs(IntPtr[] arrPIDLs)
        {
            if (null != arrPIDLs)
            {
                for (int n = 0; n < arrPIDLs.Length; n++)
                {
                    if (arrPIDLs[n] != IntPtr.Zero)
                    {
                        Marshal.FreeCoTaskMem(arrPIDLs[n]);
                        arrPIDLs[n] = IntPtr.Zero;
                    }
                }
            }
        }

        #endregion

        #region ShowContextMenu()

        /// <summary>
        /// Shows the context menu
        /// </summary>
        /// <param name="files">FileInfos (should all be in same directory)</param>
        /// <param name="pointScreen">Where to show the menu</param>
        public void ShowContextMenu(FileInfo[] files, Point pointScreen)
        {
            // Release all resources first.
            ReleaseAll();
            _arrPIDLs = GetPIDLs(files);
            ShowContextMenu(pointScreen);
        }

        /// <summary>
        /// Shows the context menu
        /// </summary>
        /// <param name="dirs">DirectoryInfos (should all be in same directory)</param>
        /// <param name="pointScreen">Where to show the menu</param>
        public void ShowContextMenu(DirectoryInfo[] dirs, Point pointScreen)
        {
            // Release all resources first.
            ReleaseAll();
            _arrPIDLs = GetPIDLs(dirs);
            ShowContextMenu(pointScreen);
        }

        /// <summary>
        /// Shows the context menu
        /// </summary>
        /// <param name="arrFI">FileInfos (should all be in same directory)</param>
        /// <param name="pointScreen">Where to show the menu</param>
        private void ShowContextMenu(Point pointScreen)
        {
            IntPtr pMenu = IntPtr.Zero,
                iContextMenuPtr = IntPtr.Zero;

            try
            {
                if (null == _arrPIDLs)
                {
                    ReleaseAll();
                    return;
                }

                if (false == GetContextMenuInterfaces(_oParentFolder, _arrPIDLs, out iContextMenuPtr))
                {
                    ReleaseAll();
                    return;
                }

                pMenu = CreatePopupMenu();

                int nResult = _oContextMenu.QueryContextMenu(
                    pMenu,
                    0,
                    CMD_FIRST,
                    CMD_LAST,
                    CMF.EXPLORE |
                    CMF.NORMAL |
                    ((Control.ModifierKeys & Keys.Shift) != 0 ? CMF.EXTENDEDVERBS : 0));


                uint nSelected = TrackPopupMenuEx(
                    pMenu,
                    TPM.RETURNCMD,
                    pointScreen.X,
                    pointScreen.Y,
                    Handle,
                    IntPtr.Zero);

                DestroyMenu(pMenu);
                pMenu = IntPtr.Zero;

                if (nSelected != 0)
                {
                    InvokeCommand(_oContextMenu, nSelected, _strParentFolder, pointScreen);
                }
            }
            catch
            {
                throw;
            }
            finally
            {
                //hook.Uninstall();
                if (pMenu != IntPtr.Zero)
                {
                    DestroyMenu(pMenu);
                }

                if (iContextMenuPtr != IntPtr.Zero)
                    Marshal.Release(iContextMenuPtr);

                ReleaseAll();
            }
        }

        #endregion

        #region Variables and Constants

        private const int MAX_PATH = 260;
        private const uint CMD_FIRST = 1;
        private const uint CMD_LAST = 30000;

        private const int S_OK = 0;

        private static readonly int cbInvokeCommand = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>();

        #endregion

        #region DLL Import

        // Retrieves the IShellFolder interface for the desktop folder, which is the root of the Shell's namespace.
        [DllImport("shell32.dll")]
        private static extern int SHGetDesktopFolder(out IntPtr ppshf);

        // Takes a STRRET structure returned by IShellFolder::GetDisplayNameOf, converts it to a string, and places the result in a buffer. 
        [DllImport("shlwapi.dll", EntryPoint = "StrRetToBuf", ExactSpelling = false, CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int StrRetToBuf(IntPtr pstr, IntPtr pidl, StringBuilder pszBuf, int cchBuf);

        // The TrackPopupMenuEx function displays a shortcut menu at the specified location and tracks the selection of items on the shortcut menu. The shortcut menu can appear anywhere on the screen.
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern uint TrackPopupMenuEx(IntPtr hmenu, TPM flags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        // The CreatePopupMenu function creates a drop-down menu, submenu, or shortcut menu. The menu is initially empty. You can insert or append menu items by using the InsertMenuItem function. You can also use the InsertMenu function to insert menu items and the AppendMenu function to append menu items.
        [DllImport("user32", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreatePopupMenu();

        // The DestroyMenu function destroys the specified menu and frees any memory that the menu occupies.
        [DllImport("user32", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DestroyMenu(IntPtr hMenu);
        #endregion

        #region Shell GUIDs

        private static Guid IID_IShellFolder = new("{000214E6-0000-0000-C000-000000000046}");
        private static Guid IID_IContextMenu = new("{000214e4-0000-0000-c000-000000000046}");

        #endregion

        #region Structs

        // Contains extended information about a shortcut menu command
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CMINVOKECOMMANDINFOEX
        {
            public int cbSize;
            public CMIC fMask;
            public IntPtr hwnd;
            public IntPtr lpVerb;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpParameters;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpDirectory;
            public SW nShow;
            public int dwHotKey;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpTitle;
            public IntPtr lpVerbW;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpParametersW;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpDirectoryW;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpTitleW;
            public Point ptInvoke;
        }

        #endregion

        #region Enums

        // Defines the values used with the IShellFolder::GetDisplayNameOf and IShellFolder::SetNameOf 
        // methods to specify the type of file or folder names used by those methods
        [Flags]
        private enum SHGNO
        {
            NORMAL = 0x0000,
            INFOLDER = 0x0001,
            FOREDITING = 0x1000,
            FORADDRESSBAR = 0x4000,
            FORPARSING = 0x8000
        }

        // The attributes that the caller is requesting, when calling IShellFolder::GetAttributesOf
        [Flags]
        private enum SFGAO : uint
        {
            BROWSABLE = 0x8000000,
            CANCOPY = 1,
            CANDELETE = 0x20,
            CANLINK = 4,
            CANMONIKER = 0x400000,
            CANMOVE = 2,
            CANRENAME = 0x10,
            CAPABILITYMASK = 0x177,
            COMPRESSED = 0x4000000,
            CONTENTSMASK = 0x80000000,
            DISPLAYATTRMASK = 0xfc000,
            DROPTARGET = 0x100,
            ENCRYPTED = 0x2000,
            FILESYSANCESTOR = 0x10000000,
            FILESYSTEM = 0x40000000,
            FOLDER = 0x20000000,
            GHOSTED = 0x8000,
            HASPROPSHEET = 0x40,
            HASSTORAGE = 0x400000,
            HASSUBFOLDER = 0x80000000,
            HIDDEN = 0x80000,
            ISSLOW = 0x4000,
            LINK = 0x10000,
            NEWCONTENT = 0x200000,
            NONENUMERATED = 0x100000,
            READONLY = 0x40000,
            REMOVABLE = 0x2000000,
            SHARE = 0x20000,
            STORAGE = 8,
            STORAGEANCESTOR = 0x800000,
            STORAGECAPMASK = 0x70c50008,
            STREAM = 0x400000,
            VALIDATE = 0x1000000
        }

        // Determines the type of items included in an enumeration. 
        // These values are used with the IShellFolder::EnumObjects method
        [Flags]
        private enum SHCONTF
        {
            FOLDERS = 0x0020,
            NONFOLDERS = 0x0040,
            INCLUDEHIDDEN = 0x0080,
            INIT_ON_FIRST_NEXT = 0x0100,
            NETPRINTERSRCH = 0x0200,
            SHAREABLE = 0x0400,
            STORAGE = 0x0800,
        }

        // Specifies how the shortcut menu can be changed when calling IContextMenu::QueryContextMenu
        [Flags]
        private enum CMF : uint
        {
            NORMAL = 0x00000000,
            DEFAULTONLY = 0x00000001,
            VERBSONLY = 0x00000002,
            EXPLORE = 0x00000004,
            NOVERBS = 0x00000008,
            CANRENAME = 0x00000010,
            NODEFAULT = 0x00000020,
            INCLUDESTATIC = 0x00000040,
            EXTENDEDVERBS = 0x00000100,
            RESERVED = 0xffff0000
        }

        // Flags specifying the information to return when calling IContextMenu::GetCommandString
        [Flags]
        private enum GCS : uint
        {
            VERBA = 0,
            HELPTEXTA = 1,
            VALIDATEA = 2,
            VERBW = 4,
            HELPTEXTW = 5,
            VALIDATEW = 6
        }

        // Specifies how TrackPopupMenuEx positions the shortcut menu horizontally
        [Flags]
        private enum TPM : uint
        {
            LEFTBUTTON = 0x0000,
            RIGHTBUTTON = 0x0002,
            LEFTALIGN = 0x0000,
            CENTERALIGN = 0x0004,
            RIGHTALIGN = 0x0008,
            TOPALIGN = 0x0000,
            VCENTERALIGN = 0x0010,
            BOTTOMALIGN = 0x0020,
            HORIZONTAL = 0x0000,
            VERTICAL = 0x0040,
            NONOTIFY = 0x0080,
            RETURNCMD = 0x0100,
            RECURSE = 0x0001,
            HORPOSANIMATION = 0x0400,
            HORNEGANIMATION = 0x0800,
            VERPOSANIMATION = 0x1000,
            VERNEGANIMATION = 0x2000,
            NOANIMATION = 0x4000,
            LAYOUTRTL = 0x8000
        }

        // Flags used with the CMINVOKECOMMANDINFOEX structure
        [Flags]
        private enum CMIC : uint
        {
            HOTKEY = 0x00000020,
            ICON = 0x00000010,
            FLAG_NO_UI = 0x00000400,
            UNICODE = 0x00004000,
            NO_CONSOLE = 0x00008000,
            ASYNCOK = 0x00100000,
            NOZONECHECKS = 0x00800000,
            SHIFT_DOWN = 0x10000000,
            CONTROL_DOWN = 0x40000000,
            FLAG_LOG_USAGE = 0x04000000,
            PTINVOKE = 0x20000000
        }

        // Specifies how the window is to be shown
        [Flags]
        private enum SW
        {
            HIDE = 0,
            SHOWNORMAL = 1,
            NORMAL = 1,
            SHOWMINIMIZED = 2,
            SHOWMAXIMIZED = 3,
            MAXIMIZE = 3,
            SHOWNOACTIVATE = 4,
            SHOW = 5,
            MINIMIZE = 6,
            SHOWMINNOACTIVE = 7,
            SHOWNA = 8,
            RESTORE = 9,
            SHOWDEFAULT = 10,
        }

        #endregion

        #region IShellFolder

        [ComImport, Guid("000214E6-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellFolder
        {
            // Translates a file object's or folder's display name into an item identifier list.
            // Return value: error code, if any
            [PreserveSig]
            int ParseDisplayName(
                IntPtr hwnd,
                IntPtr pbc,
                [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
                ref uint pchEaten,
                out IntPtr ppidl,
                ref SFGAO pdwAttributes);

            // Allows a client to determine the contents of a folder by creating an item
            // identifier enumeration object and returning its IEnumIDList interface.
            // Return value: error code, if any
            [PreserveSig]
            int EnumObjects(
                IntPtr hwnd,
                SHCONTF grfFlags,
                out IntPtr enumIDList);

            // Retrieves an IShellFolder object for a subfolder.
            // Return value: error code, if any
            [PreserveSig]
            int BindToObject(
                IntPtr pidl,
                IntPtr pbc,
                ref Guid riid,
                out IntPtr ppv);

            // Requests a pointer to an object's storage interface. 
            // Return value: error code, if any
            [PreserveSig]
            int BindToStorage(
                IntPtr pidl,
                IntPtr pbc,
                ref Guid riid,
                out IntPtr ppv);

            // Determines the relative order of two file objects or folders, given their
            // item identifier lists. Return value: If this method is successful, the
            // CODE field of the HRESULT contains one of the following values (the code
            // can be retrived using the helper function GetHResultCode): Negative A
            // negative return value indicates that the first item should precede
            // the second (pidl1 < pidl2). 

            // Positive A positive return value indicates that the first item should
            // follow the second (pidl1 > pidl2).  Zero A return value of zero
            // indicates that the two items are the same (pidl1 = pidl2). 
            [PreserveSig]
            int CompareIDs(
                IntPtr lParam,
                IntPtr pidl1,
                IntPtr pidl2);

            // Requests an object that can be used to obtain information from or interact
            // with a folder object.
            // Return value: error code, if any
            [PreserveSig]
            int CreateViewObject(
                IntPtr hwndOwner,
                Guid riid,
                out IntPtr ppv);

            // Retrieves the attributes of one or more file objects or subfolders. 
            // Return value: error code, if any
            [PreserveSig]
            int GetAttributesOf(
                uint cidl,
                [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
                ref SFGAO rgfInOut);

            // Retrieves an OLE interface that can be used to carry out actions on the
            // specified file objects or folders.
            // Return value: error code, if any
            [PreserveSig]
            int GetUIObjectOf(
                IntPtr hwndOwner,
                uint cidl,
                [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
                ref Guid riid,
                IntPtr rgfReserved,
                out IntPtr ppv);

            // Retrieves the display name for the specified file object or subfolder. 
            // Return value: error code, if any
            [PreserveSig()]
            int GetDisplayNameOf(
                IntPtr pidl,
                SHGNO uFlags,
                IntPtr lpName);

            // Sets the display name of a file object or subfolder, changing the item
            // identifier in the process.
            // Return value: error code, if any
            [PreserveSig]
            int SetNameOf(
                IntPtr hwnd,
                IntPtr pidl,
                [MarshalAs(UnmanagedType.LPWStr)] string pszName,
                SHGNO uFlags,
                out IntPtr ppidlOut);
        }

        #endregion

        #region IContextMenu

        [ComImport, Guid("000214e4-0000-0000-c000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IContextMenu
        {
            // Adds commands to a shortcut menu
            [PreserveSig()]
            int QueryContextMenu(
                IntPtr hmenu,
                uint iMenu,
                uint idCmdFirst,
                uint idCmdLast,
                CMF uFlags);

            // Carries out the command associated with a shortcut menu item
            [PreserveSig()]
            int InvokeCommand(
                ref CMINVOKECOMMANDINFOEX info);

            // Retrieves information about a shortcut menu command, 
            // including the help string and the language-independent, 
            // or canonical, name for the command
            [PreserveSig()]
            int GetCommandString(
                uint idcmd,
                GCS uflags,
                uint reserved,
                [MarshalAs(UnmanagedType.LPArray)] byte[] commandstring,
                int cch);
        }
        #endregion
    }
}
