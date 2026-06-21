using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.PluginSDK.Logging;

namespace Flow.Launcher.Infrastructure.Image;

public class ImageLoader
{
    private readonly Logger<ImageLoader> _logger;

    // TODO: Split path cache into two: One for small icons/thumbnails with more capacity
    // and another one for big thumbnails/images with less capacity
    private readonly ImageCache<string> _pathCache = new(400, StringComparer.OrdinalIgnoreCase);
    private readonly ImageCache<(int iconIndex, int overlayIndex)> _iconIndexCache = new(250);
    private readonly ConcurrentDictionary<(string, bool), Lazy<Task<ImageSource>>> _inFlightLoads = new();

    // TODO: Consider DPI and/or make this customizable in settings
    public const int SmallIconSize = 64;
    public const int FullIconSize = 128;
    public const int FullImageSize = 384;
    public ImageSource GenericImageIcon { get; } = null!;
    public ImageSource GenericProgramIcon { get; } = null!;
    public ImageSource LoadingIcon { get; } = null!;

    public ImageLoader(Logger<ImageLoader> logger)
    {
        _logger = logger;

        // Load default icons
        // TODO: Maybe integrate them into the app itself?
        GenericImageIcon = LoadFullBitmap(NormalizePath(Constant.ImageIcon));
        GenericProgramIcon = LoadFullBitmap(NormalizePath(Constant.MissingImgIcon));
        LoadingIcon = LoadFullBitmap(NormalizePath(Constant.LoadingImgIcon));
    }

    /// <summary>
    /// Gets the full (absolute) path for the given path, resolving relative paths against Flow's directory.
    /// </summary>
    private static string NormalizePath(string path)
    {
        path = Environment.ExpandEnvironmentVariables(path);
        path = Path.GetFullPath(path, Constant.ProgramDirectory);
        return path;
    }

    /// <summary>
    /// Loads the image from disk, or returns the cached image if available.
    /// </summary>
    /// <param name="path">The image's path. Can be relative and can contain environment variables.</param>
    /// <param name="loadFullImage">Whether to load the image with its full resolution (may increase memory usage).</param>
    /// <returns>The requested image or a generic error image when something goes wrong.</returns>
    public ValueTask<ImageSource> LoadAsync(
        string path, bool loadFullImage = false)
    {
        path = NormalizePath(path);

        // Return cached image if available
        if (_pathCache.TryGetValue(path, loadFullImage, out ImageSource? cachedImage))
            return new(cachedImage);

        return new(LoadFromDiskAsync(path, loadFullImage));
    }

    private async Task<ImageSource> LoadFromDiskAsync(string normalizedPath, bool loadFullImage)
    {
        var key = (normalizedPath, loadFullImage);
        // Create a lazy task that will load the image
        // (or get the existing task if the image is already being loaded)
        var lazyTask = _inFlightLoads.GetOrAdd(key, k => new(() =>
            // Switch to background thread to avoid blocking the UI while loading the image
            Task.Run(() =>
            {
                try
                {
                    ImageSource img = LoadFromDisk(k.Item1, k.Item2);

                    // Cache image
                    _pathCache[k.Item1, k.Item2] = img;
                    return img;
                }
                finally
                {
                    // Image finished loading
                    _inFlightLoads.TryRemove(k, out _);
                }
            })
        ));

        // Load image or wait for it to load if it is already being loaded
        return await lazyTask.Value.ConfigureAwait(false);
    }

    private ImageSource LoadFromDisk(string normalizedPath, bool loadFullImage = false)
    {
        ImageSource image;
        int size = loadFullImage ? FullIconSize : SmallIconSize;

        // For image files, load the thumbnail or image directly
        if (ImageHelper.HasImageExtension(normalizedPath))
        {
            if (loadFullImage)
            {
                // Load thumbnail instead of the full bitmap because even
                // if we constrain the decoded dimensions, WPF still uses a lot of memory
                // image = LoadFullBitmap(normalizedPath);

                _logger.LogDebug($"Loading full thumbnail of file '{normalizedPath}'");
                try
                {
                    image = ShellImageHelper.GetThumbnailOrIcon(
                        normalizedPath, FullImageSize, FullImageSize, ShellItemImageFlags.ThumbnailOnly);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to get thumbnail of file '{normalizedPath}'");
                    image = GenericImageIcon;
                }
            }
            else
            {
                _logger.LogDebug($"Loading thumbnail/icon of file '{normalizedPath}'");
                try
                {
                    image = ShellImageHelper.GetThumbnailOrIcon(
                        normalizedPath, size, size, ShellItemImageFlags.Default);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to get thumbnail/icon of file '{normalizedPath}'");
                    image = GenericImageIcon;
                }
            }

            return image;
        }

        // For other files, load the icon (no thumbnails)
        var iconIndexes = ShellImageHelper.GetIconIndex(normalizedPath);
        if (iconIndexes is not (int iconIndex, int overlayIndex))
        {
            // The path is likely invalid
            _logger.LogError($"Failed to get icon index of file '{normalizedPath}'");
            return GenericProgramIcon;
        }

        // If the base icon + overlay is already cached, return it
        // This is to avoid having multiple copies of the same icon in memory
        if (_iconIndexCache.TryGetValue((iconIndex, overlayIndex), loadFullImage, out ImageSource? cachedIcon))
            return cachedIcon;

        _logger.LogDebug($"Loading icon of '{normalizedPath}'");
        try
        {
            image = ShellImageHelper.GetThumbnailOrIcon(
                normalizedPath, size, size, ShellItemImageFlags.IconOnly);

            // Add icon to cache
            _iconIndexCache[(iconIndex, overlayIndex), loadFullImage] = image;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to get icon of '{normalizedPath}'");
            image = GenericProgramIcon;
        }

        return image;
    }

    /// <summary>
    /// Loads the given image with its full resolution
    /// (capped by <see cref="FullImageSize"/> to avoid excessive memory usage).
    /// </summary>
    /// <remarks>This function may cause RAM usage spikes.</remarks>
    private ImageSource LoadFullBitmap(string path)
    {
        try
        {
            // Use a stream instead of Uri to avoid WPF internally caching the full image
            // and increasing memory usage
            using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Peek at the image dimensions. Contrain the biggest dimension to FullImageSize
            BitmapDecoder decoder = BitmapDecoder.Create(
                fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            BitmapFrame frame = decoder.Frames[0];
            int decodedWidth = 0, decodedHeight = 0;
            if (frame.PixelWidth > FullImageSize || frame.PixelHeight > FullImageSize)
            {
                if (frame.PixelWidth > frame.PixelHeight)
                    decodedWidth = FullImageSize;
                else
                    decodedHeight = FullImageSize;
            }

            fs.Position = 0;

            BitmapImage image = new();
            image.BeginInit();
            image.StreamSource = fs;
            image.CacheOption = BitmapCacheOption.OnLoad;
            // We don't care about color accuracy so skip color profile for performance
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;

            if (decodedWidth > 0)
                image.DecodePixelWidth = decodedWidth;
            else if (decodedHeight > 0)
                image.DecodePixelHeight = decodedHeight;

            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to load full image '{path}'");
            if (GenericImageIcon is null)
            {
                // GenericImageIcon might be null since this function is called
                // from the constructor
                BitmapSource fallback = BitmapSource.Create(
                    1, 1, 96, 96, PixelFormats.Indexed1, BitmapPalettes.BlackAndWhite, new byte[] { 128 }, 1);

                fallback.Freeze();
                return fallback;
            }

            return GenericImageIcon;
        }
    }
}
