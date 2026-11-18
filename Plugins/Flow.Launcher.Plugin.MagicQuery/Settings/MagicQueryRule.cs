using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flow.Launcher.Plugin.MagicQuery.Settings;

public partial class MagicQueryRule : ObservableObject
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    [GeneratedRegex(@"\$(?<token>\$|\&|\d+|\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\})")]
    private static partial Regex SubstitutionTokenRegex();

    [ObservableProperty]
    public partial bool Enabled { get; set; } = true;

    [ObservableProperty]
    public partial string WindowTitlePattern { get; set; } = string.Empty;

    [ObservableProperty]
    public partial MatchType WindowTitleMatchType { get; set; } = MatchType.Partial;

    [ObservableProperty]
    public partial bool WindowTitleCaseSensitive { get; set; } = false;

    [ObservableProperty]
    public partial string ProcessPathPattern { get; set; } = string.Empty;

    [ObservableProperty]
    public partial MatchType ProcessPathMatchType { get; set; } = MatchType.Partial;

    [ObservableProperty]
    public partial bool ProcessPathCaseSensitive { get; set; } = false;

    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    /// <summary>
    /// Evaluates this rule against the given window title and process path.
    /// </summary>
    /// <returns>The resolved query string if the rule matches; otherwise, <see langword="null"/>.</returns>
    public string? TryMatch(string? windowTitle, string? processPath)
    {
        if (!Enabled)
            return null;

        bool hasTitlePattern = !string.IsNullOrEmpty(WindowTitlePattern);
        bool hasPathPattern = !string.IsNullOrEmpty(ProcessPathPattern);
        if (!hasTitlePattern && !hasPathPattern)
            return null;

        Match? titleRegexMatch = null;
        if (hasTitlePattern)
        {
            if (windowTitle is null || !TryMatchField(
                windowTitle, WindowTitlePattern, WindowTitleMatchType, WindowTitleCaseSensitive, out titleRegexMatch))
                return null;
        }

        Match? pathRegexMatch = null;
        if (hasPathPattern)
        {
            if (processPath is null || !TryMatchField(
                processPath, ProcessPathPattern, ProcessPathMatchType, ProcessPathCaseSensitive, out pathRegexMatch))
                return null;
        }

        if (titleRegexMatch is not null || pathRegexMatch is not null)
            return ApplyRegexSubstitutions(Query, titleRegexMatch, pathRegexMatch);

        return Query;
    }

    private static bool TryMatchField(
        string input, string pattern, MatchType matchType, bool caseSensitive, out Match? regexMatch)
    {
        regexMatch = null;
        StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        switch (matchType)
        {
            case MatchType.Exact:
                return input.Equals(pattern, comparison);

            case MatchType.Partial:
                return input.Contains(pattern, comparison);

            case MatchType.Regex:
                try
                {
                    RegexOptions options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    Match match = Regex.Match(input, pattern, options, RegexTimeout);
                    if (!match.Success)
                        return false;

                    regexMatch = match;
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }

            default:
                return false;
        }
    }

    private static string ApplyRegexSubstitutions(string queryTemplate, Match? titleMatch, Match? pathMatch)
    {
        if (string.IsNullOrEmpty(queryTemplate))
            return string.Empty;

        return SubstitutionTokenRegex().Replace(queryTemplate, tokenMatch =>
        {
            string token = tokenMatch.Groups["token"].Value;
            if (token == "$")
                return "$";

            if (token == "&")
                return (titleMatch ?? pathMatch)!.Value;

            Group nameGroup = tokenMatch.Groups["name"];
            if (nameGroup.Success)
            {
                string name = nameGroup.Value;
                if (titleMatch?.Groups[name] is { Success: true } tg)
                    return tg.Value;
                if (pathMatch?.Groups[name] is { Success: true } pg)
                    return pg.Value;
                if ((titleMatch is not null && titleMatch.Groups.ContainsKey(name))
                    || (pathMatch is not null && pathMatch.Groups.ContainsKey(name)))
                    return string.Empty;

                return tokenMatch.Value;
            }

            if (int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int groupIndex))
            {
                if (groupIndex == 0)
                    return (titleMatch ?? pathMatch)!.Value;

                if (titleMatch is not null && groupIndex < titleMatch.Groups.Count && titleMatch.Groups[groupIndex].Success)
                    return titleMatch.Groups[groupIndex].Value;
                if (pathMatch is not null && groupIndex < pathMatch.Groups.Count && pathMatch.Groups[groupIndex].Success)
                    return pathMatch.Groups[groupIndex].Value;
                if ((titleMatch is not null && groupIndex < titleMatch.Groups.Count)
                    || (pathMatch is not null && groupIndex < pathMatch.Groups.Count))
                    return string.Empty;
            }

            return tokenMatch.Value;
        });
    }
}
