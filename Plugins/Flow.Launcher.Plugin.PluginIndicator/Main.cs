using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Infrastructure.Plugins;
using Flow.Launcher.Infrastructure.Plugins.Interfaces;
using Flow.Launcher.Infrastructure.Results;

namespace Flow.Launcher.Plugin.PluginIndicator
{
    public static class PluginMetadataDefinition
    {
        public static readonly PluginMetadata Metadata = new()
        {
            ID = "6A122269676E40EB86EB543B945932B9",
            ActionKeywords = ["?"],
            Name = "Plugin Indicator",
            Description = "Provides plugin action keyword suggestions",
            Author = "qianlifeng",
            Version = "1.0.0",
            IcoPath = "Images/Plugin.PluginIndicator.png",

            Plugin = new Main()
        };
    }

    public class Main : IPlugin, IHomeQuery
    {
        internal static PluginInitContext Context { get; private set; }

        public void Init(PluginInitContext context)
        {
            Context = context;
        }

        public List<Result> Query(Query query)
        {
            return QueryResults(query);
        }

        private static List<Result> QueryResults(Query query = null)
        {
            var nonGlobalPlugins = GetNonGlobalPlugins();
            var querySearch = query?.Search ?? string.Empty;

            var results =
                from keyword in nonGlobalPlugins.Keys
                let plugin = nonGlobalPlugins[keyword]
                let keywordSearchResult = Context.API.FuzzySearch(querySearch, keyword)
                let searchResult = keywordSearchResult.IsSearchPrecisionScoreMet() ? keywordSearchResult : Context.API.FuzzySearch(querySearch, plugin.Name)
                let score = searchResult.Score
                where (searchResult.IsSearchPrecisionScoreMet()
                        || string.IsNullOrEmpty(querySearch)) // To list all available action keywords
                    && !plugin.Disabled
                select new Result
                {
                    Title = keyword,
                    SubTitle = Localize.ResultSubtitle(plugin.Name),
                    Score = score,
                    IcoPath = plugin.IcoPath,
                    AutoCompleteText = $"{keyword}{Infrastructure.Results.Query.TermSeparator}",
                    Action = c =>
                    {
                        Context.API.ChangeQuery($"{keyword}{Infrastructure.Results.Query.TermSeparator}");
                        return false;
                    }
                };
            return [.. results];
        }

        private static Dictionary<string, PluginMetadata> GetNonGlobalPlugins()
        {
            var nonGlobalPlugins = new Dictionary<string, PluginMetadata>();
            foreach (var plugin in Context.API.GetAllPlugins())
            {
                foreach (var actionKeyword in plugin.ActionKeywords)
                {
                    // Skip global keywords
                    if (actionKeyword == Infrastructure.Results.Query.GlobalPluginWildcard) continue;

                    // Skip dulpicated keywords
                    if (nonGlobalPlugins.ContainsKey(actionKeyword)) continue;

                    nonGlobalPlugins.Add(actionKeyword, plugin);
                }
            }
            return nonGlobalPlugins;
        }

        public List<Result> HomeQuery()
        {
            return QueryResults();
        }
    }
}
