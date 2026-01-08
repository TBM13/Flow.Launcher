using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;

namespace Flow.Launcher.Infrastructure.Image
{
    public static class ImageLoader
    {
        private static readonly string ClassName = nameof(ImageLoader);

        private static readonly ImageCache ImageCache = new();
        private static Lock storageLock { get; } = new();
        private static BinaryStorage<List<(string, bool)>> _storage = null!;
        private static readonly ConcurrentDictionary<string, string> GuidToKey = new();
        private static ImageHashGenerator _hashGenerator = null!;
        private static readonly bool EnableImageHash = true;
        public static ImageSource Image => ImageCache[Constant.ImageIcon, false]!;
        public static ImageSource MissingImage => ImageCache[Constant.MissingImgIcon, false]!;
        public static ImageSource LoadingImage => ImageCache[Constant.LoadingImgIcon, false]!;
        public const int SmallIconSize = 64;
        public const int FullIconSize = 256;
        public const int FullImageSize = 320;

        private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".ico"];

        public static async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
                _storage = new BinaryStorage<List<(string, bool)>>("Image");
                _hashGenerator = new ImageHashGenerator();

                // Even though we no longer do image preloading and thus don't need _storage,
                // for some reason MemoryPackSerializer exceptions appear when this is removed
                LoadStorageToConcurrentDictionary();

                foreach (var icon in new[] { Constant.DefaultIcon, Constant.ImageIcon, Constant.MissingImgIcon, Constant.LoadingImgIcon })
                {
                    ImageSource img = new BitmapImage(new Uri(icon));
                    img.Freeze();
                    ImageCache[icon, false] = img;
                }
            });
        }

        private static List<(string, bool)> LoadStorageToConcurrentDictionary()
        {
            lock (storageLock)
            {
                return _storage.TryLoad([]);
            }
        }

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

        private static async ValueTask<ImageResult> LoadInternalAsync(string path, bool loadFullImage = false)
        {
            ImageResult imageResult;

            try
            {
                // extra scope for use of same variable name
                {
                    if (ImageCache.TryGetValue(path, loadFullImage, out var imageSource))
                    {
                        if (imageSource is null)
                            return new ImageResult(MissingImage, ImageType.Error);

                        return new ImageResult(imageSource, ImageType.Cache);
                    }
                }

                if (path.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException();

                    /*var imageSource = new BitmapImage(new Uri(path));
                    imageSource.Freeze();
                    return new ImageResult(imageSource, ImageType.Data);*/
                }

                imageResult = await Task.Run(() => GetThumbnailResult(ref path, loadFullImage));
            }
            catch (Exception e)
            {
                try
                {
                    // Get thumbnail may fail for certain images on the first try, retry again has proven to work
                    imageResult = GetThumbnailResult(ref path, loadFullImage);
                }
                catch (Exception e2)
                {
                    Log.Exception(ClassName, $"Failed to get thumbnail for {path} on first try", e);
                    Log.Exception(ClassName, $"Failed to get thumbnail for {path} on second try", e2);

                    ImageSource image = MissingImage;
                    ImageCache[path, false] = image;
                    imageResult = new ImageResult(image, ImageType.Error);
                }
            }

            return imageResult;
        }

        private static ImageResult GetThumbnailResult(ref string path, bool loadFullImage = false)
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
                if (ImageExtensions.Contains(extension))
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
                            Log.Exception(ClassName, $"Failed to load image file from path {path}: {ex.Message}", ex);
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
                path = Constant.MissingImgIcon;
            }

            if (type != ImageType.Error)
            {
                image.Freeze();
            }

            return new ImageResult(image, type);
        }

        private static BitmapSource GetThumbnail(string path, ThumbnailOptions option = ThumbnailOptions.ThumbnailOnly,
            int size = SmallIconSize)
        {
            return WindowsThumbnailProvider.GetThumbnail(
                path,
                size,
                size,
                option);
        }

        public static bool CacheContainImage(string path, bool loadFullImage = false)
        {
            return ImageCache.ContainsKey(path, loadFullImage);
        }

        public static bool TryGetValue(string path, bool loadFullImage, [NotNullWhen(true)] out ImageSource? image)
        {
            return ImageCache.TryGetValue(path, loadFullImage, out image);
        }

        public static async ValueTask<ImageSource> LoadAsync(string path, bool loadFullImage = false, bool cacheImage = true)
        {
            path = path.ToLowerInvariant();
            var imageResult = await LoadInternalAsync(path, loadFullImage);

            var img = imageResult.ImageSource;
            if (imageResult.ImageType != ImageType.Error && imageResult.ImageType != ImageType.Cache)
            {
                // we need to get image hash
                string? hash = EnableImageHash ? _hashGenerator.GetHashFromImage(img) : null;
                if (hash is not null)
                {
                    if (GuidToKey.TryGetValue(hash, out string? key))
                    {
                        // image already exists
                        img = ImageCache[key, loadFullImage] ?? img;
                    }
                    else if (cacheImage)
                    {
                        // save guid key
                        GuidToKey[hash] = path;
                    }
                }

                if (cacheImage)
                {
                    // update cache
                    ImageCache[path, loadFullImage] = img;
                }
            }

            return img;
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
            Trace.WriteLine(path);
            return image;
        }
    }
}
