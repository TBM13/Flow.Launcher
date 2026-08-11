using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;

namespace Flow.Launcher.ViewModel;

public partial class ResultViewModel : ObservableObject
{
    private readonly IImageLoader _imageLoader;

    /// <summary>
    /// Gets the center point of this result's UI element in screen coordinates.
    /// Returns null if the element is not loaded or not connected to a visual tree.
    /// </summary>
    [ObservableProperty]
    public partial Func<Point?>? GetScreenCenterPoint { get; set; }

    public Visibility ShowIcon => Result.Glyph is not null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ShowPreviewImage => !string.IsNullOrEmpty(Result.Preview.PreviewImagePath) ? Visibility.Visible : ShowIcon;
    public Visibility ShowGlyph => Result.Glyph is not null ? Visibility.Visible : Visibility.Collapsed;

    public string ToolTip => string.IsNullOrEmpty(Result.ToolTip)
        ? $"{Result.Title}\n\n{Result.SubTitle}"
        : Result.ToolTip;

    [ObservableProperty]
    public partial ImageSource Image { get; private set; }
    [ObservableProperty]
    public partial ImageSource PreviewImage { get; private set; }

    public string PreviewDescription => Result.Preview.Description ?? Result.SubTitle;

    public Result Result { get; }

    public ResultViewModel(IImageLoader imageLoader, Result result)
    {
        _imageLoader = imageLoader;
        Result = result;

        Image = _imageLoader.LoadingIcon;
        PreviewImage = _imageLoader.LoadingIcon;

        _ = LoadImageAsync();
    }

    public async Task LoadImageAsync()
    {
        string? imagePath = Result.IcoPath;
        if (imagePath is null)
            return;

        Image = await _imageLoader.LoadAsync(imagePath, false);
    }

    public async Task LoadPreviewImageAsync()
    {
        string? imagePath = Result.Preview.PreviewImagePath ?? Result.IcoPath;
        if (imagePath is null)
            return;

        PreviewImage = await _imageLoader.LoadAsync(imagePath, true);
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
