using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;

namespace Flow.Launcher.Plugin.PluginIndicator;

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

public class Main : IPlugin
{
    private PluginInitContext _context = null!;

    public void Init(PluginInitContext context)
    {
        _context = context;
    }

    public List<Result>? Query(Query query)
    {
        List<Result> results = [];

        foreach (PluginMetadata plugin in _context.API.GetAllInitializedPlugins(includeFailed: false))
        {
            if (plugin.Disabled)
                continue;

            foreach (string keyword in plugin.ActionKeywords)
            {
                // Skip global keywords
                if (keyword == PluginSDK.Query.GlobalPluginWildcard)
                    continue;

                // If not a home query, filter results with search term
                MatchResult searchResult;
                if (!query.IsHomeQuery && !string.IsNullOrEmpty(query.Search))
                {
                    searchResult = _context.API.StringMatcher.FuzzySearchBest(query.Search, keyword, plugin.Name);
                    if (!searchResult.IsThresholdMet)
                        continue;
                }
                else
                    searchResult = default;

                string autoCompleteText = $"{keyword} ";
                results.Add(new Result
                {
                    Title = keyword,
                    SubTitle = plugin.Name,
                    Score = searchResult.Score,
                    IconOrGlyph = plugin.IcoPath,
                    AutocompleteText = (false, autoCompleteText),
                    Action = _ =>
                    {
                        _context.API.ChangeQuery(autoCompleteText);
                        return false;
                    }
                });
            }
        }

        return results;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
