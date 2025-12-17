namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Represents a query that is sent to a plugin.
    /// </summary>
    public record Query
    {
        /// <summary>
        /// Query can be splited into multiple terms by whitespace
        /// </summary>
        public const string TermSeparator = " ";
        /// <summary>
        /// User can set multiple action keywords seperated by whitespace
        /// </summary>
        public const string ActionKeywordSeparator = TermSeparator;
        /// <summary>
        /// Wildcard action keyword. Plugins using this value will be queried on every search.
        /// </summary>
        public const string GlobalPluginWildcardSign = "*";

        /// <summary>
        /// Original query, exactly how the user has typed into the search box.
        /// We don't recommend using this property directly. You should always use Search property.
        /// </summary>
        public required string OriginalQuery { get; init; }

        /// <summary>
        /// Original query but with trimmed whitespace. Includes action keyword.
        /// It has handled built-in custom query hotkeys and build-in shortcuts.
        /// If you need the exact original query from the search box, use OriginalQuery property instead.
        /// We don't recommend using this property directly. You should always use Search property.
        /// </summary>
        public required string TrimmedQuery { get; init; }

        /// <summary>
        /// Determines whether the query was forced to execute again.
        /// For example, the value will be true when the user presses Ctrl + R.
        /// When this property is true, plugins handling this query should avoid serving cached results.
        /// </summary>
        public bool IsReQuery { get; internal set; } = false;

        /// <summary>
        /// Determines whether the query is a home query.
        /// </summary>
        public bool IsHomeQuery { get; init; } = false;

        /// <summary>
        /// Search part of a query.
        /// This will not include action keyword if exclusive plugin gets it, otherwise it should be same as TrimmedQuery.
        /// Since we allow user to switch a exclusive plugin to generic plugin,
        /// so this property will always give you the "real" query part of the query
        /// </summary>
        public required string Search { get; init; }

        /// <summary>
        /// The action keyword part of this query.
        /// For global plugins this value will be empty.
        /// </summary>
        public required string ActionKeyword { get; init; }

        /// <inheritdoc />
        public override string ToString() => TrimmedQuery;
    }
}
