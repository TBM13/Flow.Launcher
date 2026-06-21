namespace Flow.Launcher.Infrastructure.Image;

public static class ImageHelper
{
    /// <summary>
    /// Checks whether the given path has a known image file extension.
    /// </summary>
    /// <param name="lowercasePath">The path of the file, in lowercase.</param>
    public static bool HasImageExtension(ReadOnlySpan<char> lowercasePath)
    {
        ReadOnlySpan<char> ext = Path.GetExtension(lowercasePath);
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ico", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
    }
}
