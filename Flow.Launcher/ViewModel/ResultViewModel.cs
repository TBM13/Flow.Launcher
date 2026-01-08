using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel
{
    public class ResultViewModel : BaseModel
    {
        private static readonly string ClassName = nameof(ResultViewModel);

        private static readonly PrivateFontCollection FontCollection = new();
        private static readonly Dictionary<string, string> Fonts = [];

        public ResultViewModel(Result result, Settings settings)
        {
            Settings = settings;
            Result = result;

            if (Result.Glyph is { FontFamily: not null } glyph)
            {
                // Checks if it's a system installed font, which does not require path to be provided.
                if (glyph.FontFamily.EndsWith(".ttf") || glyph.FontFamily.EndsWith(".otf"))
                {
                    var fontFamilyPath = glyph.FontFamily;

                    if (Fonts.TryGetValue(fontFamilyPath, out var value))
                    {
                        Glyph = glyph with
                        {
                            FontFamily = value
                        };
                    }
                    else
                    {
                        FontCollection.AddFontFile(fontFamilyPath);
                        Fonts[fontFamilyPath] = $"{Path.GetDirectoryName(fontFamilyPath)}/#{FontCollection.Families[^1].Name}";
                        Glyph = glyph with
                        {
                            FontFamily = Fonts[fontFamilyPath]
                        };
                    }
                }
                else
                {
                    Glyph = glyph;
                }
            }
        }

        public Settings Settings { get; }

        public Visibility ShowIcon
        {
            get
            {
                // If both glyph and image icons are not available, it will then be the default icon
                if (!ImgIconAvailable && !GlyphAvailable)
                    return Visibility.Visible;

                // Although user can choose to use glyph icons, plugins may choose to supply only image icons.
                // In this case we ignore the setting because otherwise icons will not display as intended
                if (Settings.UseGlyphIcons && !GlyphAvailable && ImgIconAvailable)
                    return Visibility.Visible;

                return !Settings.UseGlyphIcons && ImgIconAvailable ? Visibility.Visible : Visibility.Collapsed;
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
                // Although user can choose to not use glyph icons, plugins may choose to supply only glyph icons.
                // In this case we ignore the setting because otherwise icons will not display as intended
                if (!Settings.UseGlyphIcons && !ImgIconAvailable && GlyphAvailable)
                    return Visibility.Visible;

                return Settings.UseGlyphIcons && GlyphAvailable ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private bool GlyphAvailable => Glyph is not null;

        private bool ImgIconAvailable => !string.IsNullOrEmpty(Result.IcoPath) || Result.Icon is not null;

        private bool PreviewImageAvailable => !string.IsNullOrEmpty(Result.Preview.PreviewImagePath) || Result.Preview.PreviewDelegate != null;

        public string ShowTitleToolTip => string.IsNullOrEmpty(Result.TitleToolTip)
            ? Result.Title
            : Result.TitleToolTip;

        public string ShowSubTitleToolTip => string.IsNullOrEmpty(Result.SubTitleToolTip)
            ? Result.SubTitle
            : Result.SubTitleToolTip;

        private volatile bool _imageLoaded;
        private volatile bool _previewImageLoaded;

        private ImageSource _image = ImageLoader.LoadingImage;
        private ImageSource _previewImage = ImageLoader.LoadingImage;

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
            private set => _image = value;
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
            private set => _previewImage = value;
        }

        public string PreviewDescription => Result.Preview.Description ?? Result.SubTitle;

        /// <summary>
        /// Determines if to use the full width of the preview panel
        /// </summary>
        public bool UseBigThumbnail => Result.Preview.IsMedia;

        public GlyphInfo? Glyph { get; set; }

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
                    App.API.LogException(ClassName,
                        $"IcoPath is empty and exception when calling IconDelegate for result <{Result.Title}> of plugin <{Result.PluginID}>",
                        e);
                }
            }

            imagePath ??= string.Empty;
            return await App.API.LoadImageAsync(imagePath, loadFullImage).ConfigureAwait(false);
        }

        private async Task LoadImageAsync()
        {
            var imagePath = Result.IcoPath;
            var iconDelegate = Result.Icon;

            if (imagePath is not null && ImageLoader.TryGetValue(imagePath, false, out var img))
            {
                _image = img;
                return;
            }

            // We need to modify the property not field here to trigger the OnPropertyChanged event
            Image = await LoadImageInternalAsync(imagePath, iconDelegate, false).ConfigureAwait(false);
        }

        private async Task LoadPreviewImageAsync()
        {
            var imagePath = Result.Preview.PreviewImagePath ?? Result.IcoPath;
            var iconDelegate = Result.Preview.PreviewDelegate ?? Result.Icon;

            if (imagePath is not null && ImageLoader.TryGetValue(imagePath, true, out var img))
            {
                _previewImage = img;
                return;
            }

            // We need to modify the property not field here to trigger the OnPropertyChanged event
            PreviewImage = await LoadImageInternalAsync(imagePath, iconDelegate, true).ConfigureAwait(false);
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
