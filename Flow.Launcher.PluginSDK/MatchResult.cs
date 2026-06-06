using System.ComponentModel;

namespace Flow.Launcher.Infrastructure.Helpers;

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
/// Represents the result of a match operation.
/// </summary>
public readonly record struct MatchResult
{
    /// <summary>
    /// The raw calculated search score without any search precision filtering applied.
    /// </summary>
    public required int RawScore { get; init; }

    /// <summary>
    /// The final score of the match result with search precision filters applied.
    /// </summary>
    public required int Score { get; init; }

    /// <summary>
    /// Determines whether the search precision score threshold was met.
    /// </summary>
    public required bool IsSearchPrecisionScoreMet { get; init; }
}
