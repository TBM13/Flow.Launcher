namespace Flow.Launcher.Infrastructure.Results;

/// <summary>
/// Represents a query that is sent to a plugin.
/// </summary>
public record Query
{
    /// <summary>
    /// The character that separates terms in a query.
    /// </summary>
    public const char TermSeparator = ' ';
    /// <summary>
    /// Plugins whose action keyword is this will be queried on every search.
    /// </summary>
    public const string GlobalPluginWildcard = "*";

    /// <summary>
    /// The original query, exactly what the user typed into the search box.
    /// <para/>
    /// We don't recommend using this property directly. Use <see cref="Search"/> instead.
    /// </summary>
    public required string OriginalQuery { get; init; }

    /// <summary>
    /// Original query but with whitespaces trimmed. Includes the action keyword.
    /// <para/>
    /// If you need the exact original query from the search box, use <see cref="OriginalQuery"/> instead.
    /// <para/>
    /// We don't recommend using this property directly. Use <see cref="Search"/> instead.
    /// </summary>
    public required string TrimmedQuery { get; init; }

    /// <summary>
    /// The search part of a query, without the action keyword.
    /// </summary>
    public required string Search { get; init; }

    /// <summary>
    /// The action keyword part of this query.
    /// For global plugins this value will be empty.
    /// </summary>
    public required string ActionKeyword { get; init; }

    /// <summary>
    /// Determines whether the query was forced to execute again.
    /// For example, the value will be true when the user presses Ctrl + R.
    /// <para/>
    /// When this is <see langword="true"/>, plugins handling this query should avoid serving cached results.
    /// </summary>
    public required bool IsReQuery { get; init; }

    /// <summary>
    /// Determines whether the query is a home query.
    /// </summary>
    public required bool IsHomeQuery { get; init; }
}
