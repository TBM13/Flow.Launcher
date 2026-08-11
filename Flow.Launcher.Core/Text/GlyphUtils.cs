namespace Flow.Launcher.Core.Text;

public static class GlyphUtils
{
    /// <summary>
    /// Determines whether the specified input is a single glyph character in the Unicode Private Use Area (PUA).
    /// </summary>
    public static bool IsGlyph(ReadOnlySpan<char> input)
    {
        return input.Length == 1
            && input[0] >= '\uE000'
            && input[0] <= '\uF8FF';
    }
}
