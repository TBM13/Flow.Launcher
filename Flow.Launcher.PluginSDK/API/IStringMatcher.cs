namespace Flow.Launcher.PluginSDK.API;

/// <summary>
/// Performs fuzzy searching against strings.
/// </summary>
public interface IStringMatcher
{
    /// <summary>
    /// Fuzzy search the string with the query string.
    /// </summary>
    MatchResult FuzzyMatch(ReadOnlySpan<char> query, ReadOnlySpan<char> s);
}
