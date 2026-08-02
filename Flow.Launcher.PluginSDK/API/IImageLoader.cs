using System.Windows.Media;

namespace Flow.Launcher.PluginSDK.API;

/// <summary>
/// Manages loading images from disk and caching them for later use.
/// </summary>
public interface IImageLoader
{
    ImageSource GenericImageIcon { get; }
    ImageSource GenericProgramIcon { get; }
    ImageSource LoadingIcon { get; }

    /// <summary>
    /// Loads the image from disk, or returns the cached image if available.
    /// </summary>
    /// <param name="path">The image's path. Can be relative and can contain environment variables.</param>
    /// <param name="loadFullImage">Whether to load the image with its full resolution (may increase memory usage).</param>
    /// <returns>The requested image or a generic error image when something goes wrong.</returns>
    ValueTask<ImageSource> LoadAsync(string path, bool loadFullImage = false);
}
