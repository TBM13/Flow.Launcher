using System;
using System.Collections.Generic;
using Flow.Launcher.Infrastructure.Plugins;
using Flow.Launcher.Infrastructure.Results;

namespace Flow.Launcher.Core;

public static class QueryBuilder
{
    public static Query Build(string originalQuery, bool isRequery, Dictionary<string, PluginMetadata> nonGlobalPlugins)
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

        string[] terms = trimmedQuery.Split(Query.TermSeparator, StringSplitOptions.RemoveEmptyEntries);
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
