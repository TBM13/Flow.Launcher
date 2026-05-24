using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.PluginSDK.Logging;

namespace Flow.Launcher.Infrastructure.Image;

public class ImageLoader(Logger<ImageLoader> logger)
{
    private readonly Logger<ImageLoader> _logger = logger;
    private readonly ImageCache _imageCache = new();
    private readonly ConcurrentDictionary<string, string> _guidToKey = new();
    private readonly string[] _imageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".ico"];

    public ImageSource Image => _imageCache[Constant.ImageIcon, false]!;
    public ImageSource MissingImage => _imageCache[Constant.MissingImgIcon, false]!;
    public ImageSource LoadingImage => _imageCache[Constant.LoadingImgIcon, false]!;
    public const int SmallIconSize = 64;
    public const int FullIconSize = 256;
    public const int FullImageSize = 320;

    private record ImageResult(ImageSource ImageSource, ImageType ImageType);
    private enum ImageType
    {
        File,
        Folder,
        Data,
        ImageFile,
        FullImageFile,
        Error,
        Cache
    }

    public async Task InitializeAsync()
    {
        await Task.Run(() =>
        {
            foreach (var icon in new[] { Constant.DefaultIcon, Constant.ImageIcon, Constant.MissingImgIcon, Constant.LoadingImgIcon })
            {
                ImageSource img = new BitmapImage(new Uri(icon));
                img.Freeze();
                _imageCache[icon, false] = img;
            }
        });
    }

    public bool TryGetValue(string path, bool loadFullImage, [NotNullWhen(true)] out ImageSource? image)
    {
        return _imageCache.TryGetValue(path, loadFullImage, out image);
    }

    private static BitmapSource GetThumbnail(string path,
        ThumbnailOptions option = ThumbnailOptions.ThumbnailOnly, int size = SmallIconSize)
    {
        return WindowsThumbnailProvider.GetThumbnail(
            path,
            size,
            size,
            option);
    }

    private static BitmapImage LoadFullImage(string path)
    {
        path = Path.GetFullPath(path);
        Uri uri = new Uri(path);

        int decodedWidth = 0, decodedHeight = 0;
        // Peek at the image dimensions without fully loading it
        BitmapFrame frame = BitmapFrame.Create(uri, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        if (frame.PixelWidth > FullImageSize || frame.PixelHeight > FullImageSize)
        {
            if (frame.PixelWidth > frame.PixelHeight)
                // Image is landscape, constraining the width is enough
                // (since the aspect ratio is maintained)
                decodedWidth = FullImageSize;
            else
                // Image is portrait, constraining the height is enough
                decodedHeight = FullImageSize;
        }

        BitmapImage image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = uri;
        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        image.EndInit();

        if (decodedWidth > 0)
            image.DecodePixelWidth = decodedWidth;
        if (decodedHeight > 0)
            image.DecodePixelHeight = decodedHeight;

        image.Freeze();
        return image;
    }

    private ImageResult GetThumbnailResult(string path, bool loadFullImage = false)
    {
        ImageSource image;
        ImageType type = ImageType.Error;

        if (Directory.Exists(path))
        {
            /* Directories can also have thumbnails instead of shell icons.
             * Generating thumbnails for a bunch of folder results while scrolling
             * could have a big impact on performance and Flow.Launcher responsibility.
             * - Solution: just load the icon
             */
            type = ImageType.Folder;
            image = GetThumbnail(path, ThumbnailOptions.IconOnly);
        }
        else if (File.Exists(path))
        {
            var extension = Path.GetExtension(path).ToLower();
            if (_imageExtensions.Contains(extension))
            {
                type = ImageType.ImageFile;
                if (loadFullImage)
                {
                    try
                    {
                        image = LoadFullImage(path);
                        type = ImageType.FullImageFile;
                    }
                    catch (NotSupportedException ex)
                    {
                        image = Image;
                        type = ImageType.Error;
                        _logger.LogError(ex, $"Failed to load image file from {path}");
                    }
                }
                else
                {
                    /* Although the documentation for GetImage on MSDN indicates that
                     * if a thumbnail is available it will return one, this has proved to not
                     * be the case in many situations while testing.
                     * - Solution: explicitly pass the ThumbnailOnly flag
                     */
                    image = GetThumbnail(path, ThumbnailOptions.ThumbnailOnly);
                }
            }
            else
            {
                type = ImageType.File;
                image = GetThumbnail(path, ThumbnailOptions.None, loadFullImage ? FullIconSize : SmallIconSize);
            }
        }
        else
        {
            image = MissingImage;
        }

        if (type != ImageType.Error)
        {
            image.Freeze();
        }

        return new ImageResult(image, type);
    }

    private async ValueTask<ImageResult> LoadInternalAsync(string path, bool loadFullImage = false)
    {
        ImageResult imageResult;

        try
        {
            imageResult = await Task.Run(() => GetThumbnailResult(path, loadFullImage));
        }
        catch (Exception e)
        {
            try
            {
                // Get thumbnail may fail for certain images on the first try, retry again has proven to work
                imageResult = GetThumbnailResult(path, loadFullImage);
            }
            catch (Exception e2)
            {
                _logger.LogError(e2, $"Failed to get thumbnail for {path} on first try");
                _logger.LogError(e2, $"Failed to get thumbnail for {path} on second try");

                ImageSource image = MissingImage;
                _imageCache[path, false] = image;
                imageResult = new ImageResult(image, ImageType.Error);
            }
        }

        return imageResult;
    }

    public async ValueTask<ImageSource> LoadAsync(string path, bool loadFullImage = false, bool cacheImage = true)
    {
        // If the path is relative combine it with FlowLauncher's directory,
        // since the working directory may be different
        if (!Path.IsPathFullyQualified(path))
        {
            path = Path.Combine(Constant.ProgramDirectory, path);
        }
        path = path.ToLowerInvariant();

        // Use cached image if available
        if (_imageCache.TryGetValue(path, loadFullImage, out ImageSource? cachedImage))
            return cachedImage;

        var imageResult = await LoadInternalAsync(path, loadFullImage);

        var img = imageResult.ImageSource;
        if (imageResult.ImageType != ImageType.Error && imageResult.ImageType != ImageType.Cache)
        {
            // we need to get image hash
            string? hash = ImageHashGenerator.GetHashFromImage(img);
            if (hash is not null)
            {
                if (_guidToKey.TryGetValue(hash, out string? key))
                {
                    // image already exists
                    img = _imageCache[key, loadFullImage] ?? img;
                }
                else if (cacheImage)
                {
                    // save guid key
                    _guidToKey[hash] = path;
                }
            }

            if (cacheImage)
            {
                // update cache
                _imageCache[path, loadFullImage] = img;
            }
        }

        return img;
    }
}
