using System.Collections.ObjectModel;

namespace Flow.Launcher.Plugin.Program.Settings;

public sealed class Settings
{
    public const char SuffixSeparator = ';';

    private static readonly string[] BuiltinExtensions = ["exe", "lnk", "appref-ms", "url"];

    public DateTime LastIndexTime { get; set; }

    /// <summary>
    /// User-added program directories.
    /// </summary>
    public ObservableCollection<ProgramSource> ProgramSources { get; set; } = [];

    /// <summary>
    /// Individual programs hidden by the user from search results.
    /// </summary>
    public ObservableCollection<ProgramSource> DisabledProgramSources { get; set; } = [];

    public string[] CustomSuffixes { get; set; } = [];

    public bool EnableStartMenuSource { get; set; } = true;
    public bool EnableUWP { get; set; } = true;
    public bool EnableRegistrySource { get; set; } = true;
    public bool EnablePathSource { get; set; } = false;

    public bool EnableDescription { get; set; } = false;
    public bool HideAppsPath { get; set; } = true;
    public bool HideUninstallers { get; set; } = true;

    public HashSet<string> GetAllExtensions()
    {
        HashSet<string> extensions = new(BuiltinExtensions, StringComparer.OrdinalIgnoreCase);

        foreach (string suffix in CustomSuffixes)
        {
            string normalized = suffix.Trim().TrimStart('.');
            if (!string.IsNullOrEmpty(normalized))
                extensions.Add(normalized);
        }

        return extensions;
    }

    public static string[] ParseCustomSuffixes(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        HashSet<string> builtin = new(BuiltinExtensions, StringComparer.OrdinalIgnoreCase);
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> parsed = [];

        foreach (string token in text.Split([SuffixSeparator, ','], StringSplitOptions.RemoveEmptyEntries))
        {
            string normalized = token.Trim().TrimStart('.');
            if (string.IsNullOrEmpty(normalized) || builtin.Contains(normalized))
                continue;

            if (seen.Add(normalized))
                parsed.Add(normalized);
        }

        return [.. parsed];
    }
}
