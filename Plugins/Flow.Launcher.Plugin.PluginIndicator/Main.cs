using System.Runtime.CompilerServices;
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

    public ValueTask InitAsync(PluginInitContext context)
    {
        _context = context;
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<Result> QueryAsync(
        Query query,
        [EnumeratorCancellation] CancellationToken token)
    {
        foreach (PluginMetadata plugin in _context.API.GetAllInitializedPlugins(includeFailed: false))
        {
            if (plugin.Disabled)
                continue;

            foreach (string keyword in plugin.ActionKeywords)
            {
                token.ThrowIfCancellationRequested();

                // Skip global keywords
                if (keyword == PluginSDK.Query.GlobalPluginWildcard)
                    continue;

                // If not a home query, filter results with search term
                bool addSelectedCount = false;
                int score = 0;
                if (!query.IsHomeQuery && !string.IsNullOrWhiteSpace(query.Search))
                {
                    MatchResult match = _context.API.StringMatcher.FuzzySearchBest(query.Search, keyword, plugin.Name);
                    if (!match.IsThresholdMet)
                        continue;

                    score = match.Score;
                    addSelectedCount = true;
                }

                string autoCompleteText = $"{keyword} ";
                yield return new Result
                {
                    Title = keyword,
                    SubTitle = plugin.Name,
                    Score = score,
                    AddSelectedCount = addSelectedCount,
                    IconOrGlyph = plugin.IcoPath,
                    AutocompleteText = (false, autoCompleteText),
                    Action = _ =>
                    {
                        _context.API.ChangeQuery(autoCompleteText);
                        return false;
                    }
                };
            }
        }
    }
}
