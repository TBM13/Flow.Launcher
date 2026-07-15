using System.IO;

namespace Flow.Launcher.Interop.Files;

public static class ImageHelper
{
    /// <summary>
    /// Checks whether the given extension is a known image extension.
    /// </summary>
    /// <param name="ext">The file extension with the leading dot.</param>
    public static bool IsImageExtension(ReadOnlySpan<char> ext)
    {
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ico", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
    }
}
