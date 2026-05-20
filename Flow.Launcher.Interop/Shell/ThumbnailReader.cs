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
/// Subclass of <see cref="SIIGBF"/>
/// </summary>
[Flags]
public enum ThumbnailOptions
{
    None = 0x00,
    BiggerSizeOk = 0x01,
    InMemoryOnly = 0x02,
    IconOnly = 0x04,
    ThumbnailOnly = 0x08,
    InCacheOnly = 0x10,
}

public class WindowsThumbnailProvider
{
    // Based on https://stackoverflow.com/questions/21751747/extract-thumbnail-for-any-file-in-windows

    private static readonly Guid GUID_IShellItem = typeof(IShellItem).GUID;

    /// <summary>
    /// Obtains a BitmapSource thumbnail for the specified file.
    /// </summary>
    /// <remarks>
    /// If the file is a Windows URL shortcut (".url"), the method attempts to resolve the shortcut's icon and use that for the thumbnail; otherwise it requests a thumbnail for the file path. The native HBITMAP used to create the BitmapSource is always released to avoid native memory leaks.
    /// </remarks>
    /// <param name="fileName">Path to the file (can be a regular file or a ".url" shortcut).</param>
    /// <param name="width">Requested thumbnail width in pixels.</param>
    /// <param name="height">Requested thumbnail height in pixels.</param>
    /// <param name="options">Thumbnail extraction options (flags) controlling fallback and caching behavior.</param>
    /// <returns>A BitmapSource representing the requested thumbnail.</returns>
    public static BitmapSource GetThumbnail(string fileName, int width, int height, ThumbnailOptions options)
    {
        HBITMAP hBitmap;

        var extension = Path.GetExtension(fileName);
        if (string.Equals(extension, InternetShortcutHelper.INTERNET_SHORTCUT_EXTENSION,
            StringComparison.OrdinalIgnoreCase))
        {
            hBitmap = GetHBitmapForUrlFile(fileName, width, height, options);
        }
        else
        {
            hBitmap = GetHBitmap(Path.GetFullPath(fileName), width, height, options);
        }

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

    /// <summary>
    /// Obtains a native HBITMAP for the specified file at the requested size using the Windows Shell image factory.
    /// </summary>
    /// <remarks>
    /// If <paramref name="options"/> is <see cref="ThumbnailOptions.ThumbnailOnly"/> and thumbnail extraction fails
    /// due to extraction errors or a missing path, the method falls back to requesting an icon (<see cref="ThumbnailOptions.IconOnly"/>).
    /// The returned HBITMAP is a raw GDI handle; the caller is responsible for releasing it (e.g., via DeleteObject) to avoid native memory leaks.
    /// </remarks>
    /// <param name="fileName">Path to the file to thumbnail.</param>
    /// <param name="width">Requested thumbnail width in pixels.</param>
    /// <param name="height">Requested thumbnail height in pixels.</param>
    /// <param name="options">Thumbnail request flags that control behavior (e.g., ThumbnailOnly, IconOnly).</param>
    /// <returns>An HBITMAP handle containing the image. Caller must free the handle when finished.</returns>
    /// <exception cref="COMException"></exception>
    /// <exception cref="InvalidOperationException">If the shell item does not expose IShellItemImageFactory or if an unexpected error occurs while obtaining the image.</exception>
    private static unsafe HBITMAP GetHBitmap(string fileName, int width, int height, ThumbnailOptions options)
    {
        PInvoke.SHCreateItemFromParsingName(
            fileName,
            null,
            GUID_IShellItem,
            out object nativeShellItem).ThrowOnFailure();

        if (nativeShellItem is not IShellItemImageFactory imageFactory)
        {
            Marshal.ReleaseComObject(nativeShellItem);
            throw new InvalidOperationException("Failed to get IShellItemImageFactory");
        }

        SIZE size = new SIZE
        {
            cx = width,
            cy = height
        };

        HBITMAP hBitmap = default;
        try
        {
            try
            {
                imageFactory.GetImage(size, (SIIGBF)options, &hBitmap);
            }
            catch (COMException ex) when (options == ThumbnailOptions.ThumbnailOnly &&
                (ex.HResult == (int)HRESULT.WTS_E_EXTRACTIONPENDING || ex.HResult == (int)HRESULT.WTS_E_FAILEDEXTRACTION))
            {
                // Fallback to IconOnly if extraction fails or files cannot be found
                imageFactory.GetImage(size, (SIIGBF)ThumbnailOptions.IconOnly, &hBitmap);
            }
            catch (FileNotFoundException) when (options == ThumbnailOptions.ThumbnailOnly)
            {
                // Fallback to IconOnly if files cannot be found
                imageFactory.GetImage(size, (SIIGBF)ThumbnailOptions.IconOnly, &hBitmap);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to get thumbnail", ex);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(nativeShellItem);
        }

        return hBitmap;
    }

    /// <summary>
    /// Obtains an HBITMAP for a Windows .url shortcut by resolving its IconFile entry and delegating to GetHBitmap.
    /// </summary>
    /// <remarks>
    /// The method parses the .url file as an INI, looks in the "InternetShortcut" section for the "IconFile" entry,
    /// and requests a bitmap for that icon path. If no IconFile is present or any error occurs while reading or
    /// resolving the icon, it falls back to requesting a thumbnail for the .url file itself.
    /// </remarks>
    /// <param name="fileName">Path to the .url shortcut file.</param>
    /// <param name="width">Requested thumbnail width (pixels).</param>
    /// <param name="height">Requested thumbnail height (pixels).</param>
    /// <param name="options">ThumbnailOptions flags controlling extraction behavior.</param>
    /// <returns>An HBITMAP containing the requested image; callers are responsible for freeing the native handle.</returns>
    private static HBITMAP GetHBitmapForUrlFile(string fileName, int width, int height, ThumbnailOptions options)
    {
        try
        {
            InternetShortcutInfo shortcutInfo = InternetShortcutHelper.Parse(fileName);
            if (shortcutInfo.IconFile is not null && File.Exists(shortcutInfo.IconFile))
                return GetHBitmap(Path.GetFullPath(shortcutInfo.IconFile), width, height, options);
        }
        catch
        {
            // fallback to getting the thumbnail directly from the .url file
        }

        return GetHBitmap(Path.GetFullPath(fileName), width, height, options);
    }
}
