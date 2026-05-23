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
public record MatchResult
{
    /// <summary>
    /// Whether the match operation was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The raw calculated search score without any search precision filtering applied.
    /// </summary>
    public required int RawScore { get; init; }

    /// <summary>
    /// The final score of the match result with search precision filters applied.
    /// </summary>
    public int Score => IsSearchPrecisionScoreMet(RawScore) ? RawScore : 0;

    /// <summary>
    /// The search precision score used to filter the search results.
    /// </summary>
    public required SearchPrecision SearchPrecision { get; init; }

    /// <summary>
    /// Determines if the search precision score is met.
    /// </summary>
    /// <returns></returns>
    public bool IsSearchPrecisionScoreMet()
    {
        return IsSearchPrecisionScoreMet(RawScore);
    }

    private bool IsSearchPrecisionScoreMet(int rawScore)
    {
        return rawScore >= (int)SearchPrecision;
    }
}
