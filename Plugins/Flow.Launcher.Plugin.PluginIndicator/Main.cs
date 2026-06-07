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
#pragma warning disable CS8618
        private PluginInitContext _context;
#pragma warning restore CS8618

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public List<Result>? Query(Query query)
        {
            return QueryResults(query);
        }

        public List<Result>? HomeQuery()
        {
            return QueryResults();
        }

        private List<Result> QueryResults(Query? query = null)
        {
            Dictionary<string, PluginMetadata> nonGlobalPlugins = GetNonGlobalPlugins();
            string querySearch = query?.Search ?? string.Empty;

            IEnumerable<Result> results =
                from keyword in nonGlobalPlugins.Keys
                let plugin = nonGlobalPlugins[keyword]
                let keywordSearchResult = _context.API.FuzzySearch(querySearch, keyword)
                let searchResult = keywordSearchResult.IsSearchPrecisionScoreMet
                    ? keywordSearchResult : _context.API.FuzzySearch(querySearch, plugin.Name)
                let score = searchResult.Score
                where (searchResult.IsSearchPrecisionScoreMet
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
                        _context.API.ChangeQuery($"{keyword}{Infrastructure.Results.Query.TermSeparator}");
                        return false;
                    }
                };

            return [.. results];
        }

        private Dictionary<string, PluginMetadata> GetNonGlobalPlugins()
        {
            Dictionary<string, PluginMetadata> nonGlobalPlugins = [];
            foreach (PluginMetadata plugin in _context.API.GetAllPlugins())
            {
                foreach (string actionKeyword in plugin.ActionKeywords)
                {
                    // Skip global keywords
                    if (actionKeyword == Infrastructure.Results.Query.GlobalPluginWildcard)
                        continue;

                    // Skip duplicated keywords
                    if (nonGlobalPlugins.ContainsKey(actionKeyword))
                        continue;

                    nonGlobalPlugins.Add(actionKeyword, plugin);
                }
            }

            return nonGlobalPlugins;
        }
    }
}
