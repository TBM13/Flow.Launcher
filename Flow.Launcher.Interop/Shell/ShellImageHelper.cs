using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Flow.Launcher.Infrastructure.Helpers;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;

namespace Flow.Launcher.Infrastructure.Image;

/// <summary>
/// Subenum of <see cref="SIIGBF"/>.
/// </summary>
[Flags]
public enum ShellItemImageFlags
{
    /// <summary>
    /// Default behavior:
    /// <list type="bullet">
    /// <item>Try to get a thumbnail first, fallback to icon if it is not available.</item>
    /// <item>Shrink the bitmap when it is larger than the requested size.</item>
    /// </list>
    /// </summary>
    Default = SIIGBF.SIIGBF_RESIZETOFIT,

    /// <summary>
    /// Do not shrink the bitmap when it is larger than the requested size.
    /// </summary>
    BiggerSizeOk = SIIGBF.SIIGBF_BIGGERSIZEOK,
    /// <summary>
    /// If necessary, stretch the bitmap so that the height and width fit the given size.
    /// </summary>
    ScaleUp = SIIGBF.SIIGBF_SCALEUP,

    /// <summary>
    /// Return only the icon, never the thumbnail.
    /// </summary>
    IconOnly = SIIGBF.SIIGBF_ICONONLY,
    /// <summary>
    /// Return only the thumbnail, never the icon.
    /// </summary>
    /// <remarks>Not all items have thumbnails. The operation will fail in those cases.</remarks>
    ThumbnailOnly = SIIGBF.SIIGBF_THUMBNAILONLY,

    /// <summary>
    /// Get the item's icon only if it is cached in memory.
    /// <para/>
    /// If it is not, fallback to a per-class icon.
    /// </summary>
    /// <remarks>Does not access the disk.</remarks>
    InMemoryOnly = SIIGBF.SIIGBF_MEMORYONLY,

    /// <summary>
    /// Allows access to the disk, but only to retrieve a cached item.
    /// <para/>
    /// If the thumbnail has never been generated before, skips generation
    /// and instead returns a cached per-instance icon or fails.
    /// </summary>
    /// <remarks>Does not extract a thumbnail or icon, which can be expensive.</remarks>
    InCacheOnly = SIIGBF.SIIGBF_INCACHEONLY,
}

public static class ShellImageHelper
{
    private static readonly Guid ImageFactoryGuid = typeof(IShellItemImageFactory).GUID;

    /// <summary>
    /// Obtains the icon or thumbnail for the specified file.
    /// </summary>
    /// <remarks>For Internet Shortcut files (.url), uses its custom icon when available.</remarks>
    /// <param name="fullPath">The absolute path to the file.</param>
    /// <param name="width">Width in physical device pixels.</param>
    /// <param name="height">Height in physical device pixels.</param>
    public static BitmapSource GetThumbnailOrIcon(
        string fullPath, int width, int height, ShellItemImageFlags options)
    {
        ReadOnlySpan<char> extension = Path.GetExtension(fullPath.AsSpan());
        // If the file is an internet shortcut, resolve the icon specified in it and use it if valid
        if (extension.Equals(InternetShortcutHelper.INTERNET_SHORTCUT_EXTENSION,
            StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                InternetShortcutInfo shortcutInfo = InternetShortcutHelper.Parse(fullPath);
                string? shortcutDir = Path.GetDirectoryName(fullPath);

                if (shortcutInfo.IconFile is not null && shortcutDir is not null)
                {
                    // Expand environment variables & resolve relative paths using
                    // the .url file's directory as the base path
                    string shortcutIconPath = Environment.ExpandEnvironmentVariables(shortcutInfo.IconFile);
                    shortcutIconPath = Path.GetFullPath(shortcutIconPath, shortcutDir);
                    if (File.Exists(shortcutIconPath))
                        fullPath = shortcutIconPath;
                }
            }
            catch
            {
                // fallback to getting the thumbnail/icon directly from the .url file
            }
        }

        HBITMAP hBitmap = GetHBitmap(fullPath, width, height, options);
        try
        {
            BitmapSource bitmap = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            bitmap.Freeze();
            return bitmap;
        }
        finally
        {
            PInvoke.DeleteObject(hBitmap);
        }
    }

    /// <returns>An HBITMAP handle containing the image. Caller must free the handle when finished.</returns>
    /// <exception cref="COMException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    private static unsafe HBITMAP GetHBitmap(string path, int width, int height, ShellItemImageFlags options)
    {
        PInvoke.SHCreateItemFromParsingName(
            path,
            null,
            ImageFactoryGuid,
            out object shellItemObj).ThrowOnFailure();

        if (shellItemObj is not IShellItemImageFactory imageFactory)
        {
            Marshal.ReleaseComObject(shellItemObj);
            throw new InvalidOperationException("Failed to get IShellItemImageFactory");
        }

        SIZE size = new SIZE
        {
            cx = width,
            cy = height
        };

        HBITMAP hBitmap = default;
        int remainingAttempts = 3;
        try
        {
            while (true)
            {
                try
                {
                    imageFactory.GetImage(size, (SIIGBF)options, &hBitmap);
                    break;
                }
                catch (COMException ex) when (
                    ex.HResult == (int)HRESULT.E_PENDING && remainingAttempts > 0)
                {
                    // This is a normal exception when the app was recently opened.
                    // Wait a few miliseconds and retry
                    Thread.Sleep(15);
                    remainingAttempts--;
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(shellItemObj);
        }

        return hBitmap;
    }

    /// <summary>
    /// Tries to get the icon index and overlay index of a file/directory.
    /// </summary>
    /// <returns>Null if the indexes could not be retrieved.</returns>
    public static (int iconIndex, int overlayIndex)? GetIconIndex(string path)
    {
        SHGFI_FLAGS flags = SHGFI_FLAGS.SHGFI_OVERLAYINDEX
            // OverlayIndex requires Icon to be passed too
            | SHGFI_FLAGS.SHGFI_ICON;

        // Since we use SHGFI_ICON, we don't need SHGFI_SYSICONINDEX
        // flags |= SHGFI_FLAGS.SHGFI_SYSICONINDEX;

        SHFILEINFOW shfi = default;
        try
        {
            nuint res = PInvoke.SHGetFileInfo(path, default, ref shfi, flags);
            if (res == 0)
                return null;
        }
        finally
        {
            if (!shfi.hIcon.IsNull)
                PInvoke.DestroyIcon(shfi.hIcon);
        }

        int baseIndex = shfi.iIcon & 0x00FFFFFF;
        int overlayIndex = (shfi.iIcon >> 24) & 0x000000FF;
        return (baseIndex, overlayIndex);
    }
}
