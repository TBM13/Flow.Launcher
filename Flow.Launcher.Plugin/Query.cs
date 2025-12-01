using System.Text.Json.Serialization;

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
        /// The search string split into a string array.
        /// Does not include the <see cref="ActionKeyword"/>.
        /// </summary>
        public required string[] SearchTerms { get; init; }

        /// <summary>
        /// The action keyword part of this query.
        /// For global plugins this value will be empty.
        /// </summary>
        public required string ActionKeyword { get; init; }

        /// <summary>
        /// Splits <see cref="SearchTerms"/> by spaces and returns the first item.
        /// </summary>
        /// <remarks>
        /// returns an empty string when <see cref="SearchTerms"/> does not have enough items.
        /// </remarks>
        [JsonIgnore]
        public string FirstSearch => SplitSearch(0);

        /// <summary>
        /// strings from second search (including) to last search
        /// </summary>
        [JsonIgnore]
        public string SecondToEndSearch => SearchTerms.Length > 1 ? (field ??= string.Join(' ', SearchTerms[1..])) : "";

        /// <summary>
        /// Splits <see cref="SearchTerms"/> by spaces and returns the second item.
        /// </summary>
        /// <remarks>
        /// returns an empty string when <see cref="SearchTerms"/> does not have enough items.
        /// </remarks>
        [JsonIgnore]
        public string SecondSearch => SplitSearch(1);

        /// <summary>
        /// Splits <see cref="SearchTerms"/> by spaces and returns the third item.
        /// </summary>
        /// <remarks>
        /// returns an empty string when <see cref="SearchTerms"/> does not have enough items.
        /// </remarks>
        [JsonIgnore]
        public string ThirdSearch => SplitSearch(2);

        private string SplitSearch(int index)
        {
            return index < SearchTerms.Length ? SearchTerms[index] : string.Empty;
        }

        /// <inheritdoc />
        public override string ToString() => TrimmedQuery;
    }
}
