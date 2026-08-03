using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Microsoft.Extensions.Logging;

namespace Flow.Launcher.ViewModel;

public partial class ResultViewModel : ObservableObject
{
    private readonly PluginSDK.Logging.Logger<ResultViewModel> _logger;
    private readonly IImageLoader _imageLoader;

    /// <summary>
    /// Gets the center point of this result's UI element in screen coordinates.
    /// Returns null if the element is not loaded or not connected to a visual tree.
    /// </summary>
    [ObservableProperty]
    public partial Func<Point?>? GetScreenCenterPoint { get; set; }

    public Visibility ShowIcon => Glyph is not null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ShowPreviewImage => !string.IsNullOrEmpty(Result.Preview.PreviewImagePath) ? Visibility.Visible : ShowIcon;
    public Visibility ShowGlyph => Glyph is not null ? Visibility.Visible : Visibility.Collapsed;

    public string ShowTitleToolTip => string.IsNullOrEmpty(Result.TitleToolTip)
        ? Result.Title
        : Result.TitleToolTip;

    public string ShowSubTitleToolTip => string.IsNullOrEmpty(Result.SubTitleToolTip)
        ? Result.SubTitle
        : Result.SubTitleToolTip;

    private volatile bool _imageLoaded;
    private volatile bool _previewImageLoaded;
    private ImageSource _image;
    private ImageSource _previewImage;

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
    public Result Result { get; }

    public ResultViewModel(
        ILoggerFactory loggerFactory, IImageLoader imageLoader, Result result)
    {
        _logger = new(loggerFactory);
        _imageLoader = imageLoader;
        Result = result;

        Glyph = Result.Glyph;

        _image = _imageLoader.LoadingIcon;
        _previewImage = _imageLoader.LoadingIcon;
    }

    private async Task<ImageSource> LoadImageInternalAsync(string? imagePath, bool loadFullImage)
    {
        imagePath ??= string.Empty;
        return await _imageLoader.LoadAsync(imagePath, loadFullImage).ConfigureAwait(false);
    }

    private async Task LoadImageAsync()
    {
        var imagePath = Result.IcoPath;
        Image = await LoadImageInternalAsync(imagePath, false);
    }

    private async Task LoadPreviewImageAsync()
    {
        var imagePath = Result.Preview.PreviewImagePath ?? Result.IcoPath;
        PreviewImage = await LoadImageInternalAsync(imagePath, true);
    }

    public void LoadPreviewImage()
    {
        if (!_previewImageLoaded && ShowPreviewImage == Visibility.Visible)
        {
            _previewImageLoaded = true;
            _ = LoadPreviewImageAsync();
        }
    }

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
