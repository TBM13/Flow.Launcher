using Flow.Launcher.Core.Text;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Plugins;

namespace Flow.Launcher.Core;

public static class QueryBuilder
{
    public static Query Build(string originalQuery, bool isRequery, Dictionary<string, PluginMetadata> nonGlobalPlugins)
    {
        ReadOnlySpan<char> trimmedQuery = originalQuery.Trim();

        // Home query
        if (trimmedQuery.IsEmpty)
        {
            return new Query()
            {
                OriginalQuery = string.Empty,
                Search = string.Empty,
                ActionKeyword = string.Empty,
                IsReQuery = isRequery,
                IsHomeQuery = true,
            };
        }

        // Tokenize query
        Span<Range> tokens = stackalloc Range[TokenizedString.MaxTokens];
        TokenizedString tokenizedQuery = TokenizedString.Tokenize(trimmedQuery, tokens);

        string actionKeyword, search;
        ReadOnlySpan<char> possibleActionKeyword = tokenizedQuery[0];

        var lookup = nonGlobalPlugins.GetAlternateLookup<ReadOnlySpan<char>>();
        if (lookup.TryGetValue(possibleActionKeyword, out PluginMetadata? metadata) && !metadata.Disabled)
        {
            // Query has the action keyword of a non-global plugin
            actionKeyword = possibleActionKeyword.ToString();
            // TODO: Maybe generate search from the tokenizedQuery
            search = tokenizedQuery.TokenCount > 1
                ? trimmedQuery[(actionKeyword.Length + 1)..].TrimStart().ToString()
                : string.Empty;
        }
        // Allow queries with a single-digit action keyword symbol. E.g. '>settings' ('>' is the action keyword)
        else if (possibleActionKeyword.Length >= 2
                && !char.IsLetterOrDigit(possibleActionKeyword[0])
                && lookup.TryGetValue(possibleActionKeyword[..1], out metadata)
                && !metadata.Disabled)
        {
            actionKeyword = possibleActionKeyword[..1].ToString();
            // TODO: Maybe generate search from the tokenizedQuery
            search = trimmedQuery[1..].TrimStart().ToString();
        }
        else
        {
            // No valid action keyword (global query)
            actionKeyword = string.Empty;
            // TODO: Maybe generate search from the tokenizedQuery
            search = trimmedQuery.ToString();
        }

        return new Query()
        {
            OriginalQuery = originalQuery,
            Search = search,
            ActionKeyword = actionKeyword,
            IsReQuery = isRequery,
            IsHomeQuery = false,
        };
    }
}
