namespace Flow.Launcher.Plugin.SharedModels;

/// <param name="FileNameWithoutExtension">Theme file name without extension</param>
/// <param name="Name">Theme name</param>
/// <param name="IsDark">Whether the theme supports dark mode</param>
public record ThemeData(string FileNameWithoutExtension, string Name, bool? IsDark = null)
{
    public override string ToString()
    {
        return Name;
    }
}
