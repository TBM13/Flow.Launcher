namespace Flow.Launcher.PluginSDK;

/// <summary>
/// Represents a query that is sent to a plugin.
/// </summary>
public record Query
{
    /// <summary>
    /// Plugins whose action keyword is this will be queried on every search.
    /// </summary>
    public const string GlobalPluginWildcard = "*";

    /// <summary>
    /// The original query, exactly what the user typed into the search box.
    /// </summary>
    public required string OriginalQuery { get; init; }

    /// <summary>
    /// The query without the action keyword, and without any leading or trailing whitespace.
    /// </summary>
    public required string Search { get; init; }

    /// <summary>
    /// The action keyword. This is empty if the query is a home or global query.
    /// </summary>
    public required string ActionKeyword { get; init; }

    /// <summary>
    /// If true, this query is a home query (i.e. the user has not typed anything into the search box).
    /// </summary>
    public required bool IsHomeQuery { get; init; }

    /// <summary>
    /// If true, a re-query has been requested (e.g. the user pressed Ctrl+R).
    /// </summary>
    /// <remarks>When true, plugins should avoid serving cached results.</remarks>
    public required bool IsReQuery { get; init; }
}
