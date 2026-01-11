using System;
using System.Collections.Generic;
using Flow.Launcher.Infrastructure.Plugins;

namespace Flow.Launcher.Infrastructure;

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

    private Query() { }

    internal static Query Build(string originalQuery, bool isRequery, Dictionary<string, PluginMetadata> nonGlobalPlugins)
    {
        string trimmedQuery = originalQuery.Trim();

        // home query
        if (trimmedQuery.Length == 0)
        {
            return new Query()
            {
                OriginalQuery = string.Empty,
                TrimmedQuery = string.Empty,
                Search = string.Empty,
                ActionKeyword = string.Empty,
                IsReQuery = isRequery,
                IsHomeQuery = true,
            };
        }

        string[] terms = trimmedQuery.Split(TermSeparator, StringSplitOptions.RemoveEmptyEntries);
        // Since TermSeparator is a whitespace, terms should never have a length of 0 here

        string actionKeyword, search;
        string possibleActionKeyword = terms[0];

        if (nonGlobalPlugins.TryGetValue(possibleActionKeyword, out var pluginMetadata) && !pluginMetadata.Disabled)
        {
            // use non global plugin for query
            actionKeyword = possibleActionKeyword;
            search = terms.Length > 1 ? trimmedQuery[(actionKeyword.Length + 1)..].TrimStart() : string.Empty;
        }
        // Allow queries with a single-digit actionKeyword (that isn't a number nor letter), and no spaces.
        // For example: '>settings' ('>' is the action keyword)
        else if (possibleActionKeyword.Length >= 2
                && !char.IsLetterOrDigit(possibleActionKeyword[0])
                && nonGlobalPlugins.TryGetValue(possibleActionKeyword[0..1], out var pluginMetadata2)
                && !pluginMetadata2.Disabled)
        {
            actionKeyword = possibleActionKeyword[0..1];
            search = trimmedQuery[1..].TrimStart();
        }
        else
        {
            // non action keyword
            actionKeyword = string.Empty;
            search = trimmedQuery;
        }

        return new Query()
        {
            OriginalQuery = originalQuery,
            TrimmedQuery = trimmedQuery,
            Search = search,
            ActionKeyword = actionKeyword,
            IsReQuery = isRequery,
            IsHomeQuery = false,
        };
    }
}
