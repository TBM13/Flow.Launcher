using System.Drawing.Text;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Results;
using Flow.Launcher.Infrastructure.WPF;
using Flow.Launcher.PluginSDK.Logging;

namespace Flow.Launcher.ViewModel
{
    public partial class ResultViewModel : ObservableObject
    {
        // TODO: Check if there is any better alternative
        private static readonly Logger<ResultViewModel> _logger = Ioc.Default.GetRequiredService<Logger<ResultViewModel>>();
        private static readonly ImageLoader _imageLoader = Ioc.Default.GetRequiredService<ImageLoader>();
        private static readonly PrivateFontCollection _fontCollection = new();
        private static readonly Dictionary<string, string> _fonts = [];

        public ResultViewModel(Result result, ISettingsAPI settings)
        {
            Settings = settings;
            Result = result;

            if (Result.Glyph is { FontFamily: not null } glyph)
            {
                // Checks if it's a system installed font, which does not require path to be provided.
                if (glyph.FontFamily.EndsWith(".ttf") || glyph.FontFamily.EndsWith(".otf"))
                {
                    var fontFamilyPath = glyph.FontFamily;

                    if (_fonts.TryGetValue(fontFamilyPath, out var value))
                    {
                        Glyph = glyph with
                        {
                            FontFamily = value
                        };
                    }
                    else
                    {
                        _fontCollection.AddFontFile(fontFamilyPath);
                        _fonts[fontFamilyPath] = $"{Path.GetDirectoryName(fontFamilyPath)}/#{_fontCollection.Families[^1].Name}";
                        Glyph = glyph with
                        {
                            FontFamily = _fonts[fontFamilyPath]
                        };
                    }
                }
                else
                {
                    Glyph = glyph;
                }
            }
        }

        public ISettingsAPI Settings { get; }

        /// <summary>
        /// Gets the center point of this result's UI element in screen coordinates.
        /// Returns null if the element is not loaded or not connected to a visual tree.
        /// </summary>
        [ObservableProperty]
        public partial Func<Point?>? GetScreenCenterPoint { get; set; }

        public Visibility ShowIcon
        {
            get
            {
                if (GlyphAvailable)
                    return Visibility.Collapsed;

                return Visibility.Visible;
            }
        }

        public Visibility ShowPreviewImage
        {
            get
            {
                if (PreviewImageAvailable)
                    return Visibility.Visible;

                // Fall back to icon
                return ShowIcon;
            }
        }

        public Visibility ShowGlyph
        {
            get
            {
                if (GlyphAvailable)
                    return Visibility.Visible;

                return Visibility.Collapsed;
            }
        }

        private bool GlyphAvailable => Glyph is not null;

        private bool PreviewImageAvailable
            => !string.IsNullOrEmpty(Result.Preview.PreviewImagePath) || Result.Preview.PreviewDelegate != null;

        public string ShowTitleToolTip => string.IsNullOrEmpty(Result.TitleToolTip)
            ? Result.Title
            : Result.TitleToolTip;

        public string ShowSubTitleToolTip => string.IsNullOrEmpty(Result.SubTitleToolTip)
            ? Result.SubTitle
            : Result.SubTitleToolTip;

        private volatile bool _imageLoaded;
        private volatile bool _previewImageLoaded;

        private ImageSource _image = _imageLoader.LoadingIcon;
        private ImageSource _previewImage = _imageLoader.LoadingIcon;

        public ImageSource Image
        {
            get
            {
                if (!_imageLoaded)
                {
                    _imageLoaded = true;
                    _ = LoadImageAsync();
                }

                return _image;
            }
            private set => SetProperty(ref _image, value);
        }

        public ImageSource PreviewImage
        {
            get
            {
                if (!_previewImageLoaded)
                {
                    _previewImageLoaded = true;
                    _ = LoadPreviewImageAsync();
                }

                return _previewImage;
            }
            private set => SetProperty(ref _previewImage, value);
        }

        public string PreviewDescription => Result.Preview.Description ?? Result.SubTitle;

        public GlyphInfo? Glyph { get; init; }

        private async Task<ImageSource> LoadImageInternalAsync(string? imagePath, Result.IconDelegate? icon, bool loadFullImage)
        {
            if (string.IsNullOrEmpty(imagePath) && icon != null)
            {
                try
                {
                    return icon();
                }
                catch (Exception e)
                {
                    _logger.LogError(e,
                        $"IcoPath is empty and exception when calling IconDelegate for result <{Result.Title}> of plugin <{Result.PluginID}>");
                }
            }

            imagePath ??= string.Empty;
            return await App.API.LoadImageAsync(imagePath, loadFullImage).ConfigureAwait(false);
        }

        private async Task LoadImageAsync()
        {
            var imagePath = Result.IcoPath;
            var iconDelegate = Result.Icon;

            Image = await LoadImageInternalAsync(imagePath, iconDelegate, false);
        }

        private async Task LoadPreviewImageAsync()
        {
            var imagePath = Result.Preview.PreviewImagePath ?? Result.IcoPath;
            var iconDelegate = Result.Preview.PreviewDelegate ?? Result.Icon;

            PreviewImage = await LoadImageInternalAsync(imagePath, iconDelegate, true);
        }

        public void LoadPreviewImage()
        {
            if (!_previewImageLoaded && ShowPreviewImage == Visibility.Visible)
            {
                _previewImageLoaded = true;
                _ = LoadPreviewImageAsync();
            }
        }

        public Result Result { get; }

        public override bool Equals(object? obj)
        {
            return obj is ResultViewModel r && Result.Equals(r.Result);
        }

        public override int GetHashCode()
        {
            return Result.GetHashCode();
        }

        public override string ToString()
        {
            return Result.ToString();
        }
    }
}
