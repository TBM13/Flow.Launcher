using Flow.Launcher.Plugin.WindowsSettings.Settings;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;

namespace Flow.Launcher.Plugin.WindowsSettings;

public static class PluginMetadataDefinition
{
    public static readonly PluginMetadata Metadata = new()
    {
        ID = "5b1ac9a65f5a4717980d1b2a743b9855",
        ActionKeywords = ["*", "cfg"],
        Name = "Windows Settings",
        Description = "List and search Windows 11 settings.",
        Author = "TBM13",
        Version = "1.0.0",
        IcoPath = "Images/Plugin.WindowsSettings.png",

        Plugin = new Main()
    };
}

public sealed class Main : IPlugin
{
    private PluginInitContext _context = null!;
    private IStringMatcher _matcher = null!;

    /// <summary>
    /// Key is a page's path (e.g. "System/Display/").
    /// Value is all the settings that should be shown on that path.
    /// </summary>
    private readonly Dictionary<string, List<SettingEntry>> _allSettings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The settings shown for an empty or home query.
    /// </summary>
    private List<Result> _rootSettings = [];

    public void Init(PluginInitContext ctx)
    {
        _context = ctx;
        _matcher = ctx.API.StringMatcher;

        AddPage(string.Empty, AllSettings.Settings);
        _rootSettings = [.. _allSettings[AllSettings.Settings.LocalPath].Select(e => e.Result)];
    }

    private void AddPage(string path, SettingsPage page)
    {
        AddSetting(path, page);
        path += page.LocalPath;

        foreach (Setting setting in page.Settings)
        {
            if (setting is SettingsPage subPage)
                AddPage(path, subPage);
            else
                AddSetting(path, setting);
        }
    }

    private void AddSetting(string path, Setting setting)
    {
        Result result = setting.ToResult(_context);
        result.SubTitle = path.TrimEnd('/').Replace("/", "  ˃  ");

        if (setting is SettingsPage page)
            result.AutocompleteText = (true, path + page.LocalPath);

        SettingEntry entry = new(result, [setting.Name, .. setting.AlternativeNames]);
        if (!_allSettings.TryGetValue(path, out List<SettingEntry>? pathSettings))
        {
            pathSettings = [];
            _allSettings[path] = pathSettings;
        }

        pathSettings.Add(entry);
    }

    public List<Result>? Query(Query query)
    {
        // On empty queries, show all the root setting pages
        if (query.IsHomeQuery || (query.ActionKeyword.Length > 0 && string.IsNullOrWhiteSpace(query.Search)))
            return _rootSettings;

        IEnumerable<SettingEntry>? settingsToSearch = null;

        // If the query has a valid path, remove it and limit the search to that path's settings
        ReadOnlySpan<char> search = query.Search.Replace('\\', '/');
        int lastSeparatorIndex = search.LastIndexOf('/');
        if (lastSeparatorIndex != -1)
        {
            string path = search[..(lastSeparatorIndex + 1)].ToString();
            if (_allSettings.TryGetValue(path, out List<SettingEntry>? pathSettings))
            {
                settingsToSearch = pathSettings;
                // Remove the path, leave only the search term
                search = search[(lastSeparatorIndex + 1)..];
            }
        }

        settingsToSearch ??= _allSettings.Values.SelectMany(list => list);

        // If the query does not have a search term, return all the settings for this path
        if (search.IsWhiteSpace())
            return [.. settingsToSearch.Select(e => e.Result)];

        // Query has a search term: only return settings that match
        List<Result> results = [];
        foreach (SettingEntry entry in settingsToSearch)
        {
            MatchResult match = _matcher.FuzzySearchBest(search, entry.SearchNames);
            if (match.IsThresholdMet)
                results.Add(entry.Result with { Score = match.Score });
        }

        return results;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <param name="SearchNames">The searchable names of a setting: its display name and all its alternative names.</param>
    private sealed record SettingEntry(Result Result, string[] SearchNames);
}
