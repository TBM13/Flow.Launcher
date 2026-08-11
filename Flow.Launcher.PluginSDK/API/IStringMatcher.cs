using System.ComponentModel;

namespace Flow.Launcher.PluginSDK.API;

/// <summary>
/// Represents the search precision used to filter search results.
/// </summary>
public enum SearchPrecision
{
    /// <summary>
    /// The highest search precision score.
    /// </summary>
    [Description("Regular")]
    Regular = 50,

    /// <summary>
    /// The medium search precision score.
    /// </summary>
    [Description("Low")]
    Low = 20,

    /// <summary>
    /// The lowest search precision score.
    /// </summary>
    [Description("None")]
    None = 0
}


/// <summary>
/// Represents the result of a fuzzy search.
/// </summary>
/// <param name="IsThresholdMet">
/// Whether the search precision score threshold was met.
/// A result with a score that does not meet the threshold will be filtered out.
/// </param>
public readonly record struct MatchResult(int Score, bool IsThresholdMet);

/// <summary>
/// Performs fuzzy searching against strings.
/// </summary>
public interface IStringMatcher
{
    /// <summary>
    /// Performs a fuzzy search to calculate how similar
    /// <paramref name="query"/> is to <paramref name="candidate"/>.
    /// </summary>
    MatchResult FuzzySearch(ReadOnlySpan<char> query, ReadOnlySpan<char> candidate);
    /// <summary>
    /// Performs a fuzzy search to calculate how similar <paramref name="query"/> is to all the candidates.
    /// </summary>
    /// <returns>The match result with the highest score.</returns>
    MatchResult FuzzySearchBest(ReadOnlySpan<char> query, ReadOnlySpan<char> c1, ReadOnlySpan<char> c2);
    /// <summary>
    /// Performs a fuzzy search to calculate how similar <paramref name="query"/> is to all the candidates.
    /// </summary>
    /// <returns>The match result with the highest score.</returns>
    MatchResult FuzzySearchBest(ReadOnlySpan<char> query,
        ReadOnlySpan<char> c1, ReadOnlySpan<char> c2, ReadOnlySpan<char> c3);
    /// <summary>
    /// Performs a fuzzy search to calculate how similar <paramref name="query"/> is to all the candidates.
    /// </summary>
    /// <returns>The match result with the highest score.</returns>
    MatchResult FuzzySearchBest(ReadOnlySpan<char> query, params string[] candidates);
}
