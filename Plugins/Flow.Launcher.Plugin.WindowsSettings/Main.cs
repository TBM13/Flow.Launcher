using Flow.Launcher.Plugin.WindowsSettings.Settings;
using Flow.Launcher.PluginSDK;
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
        Description = "Show and search Windows 11 settings.",
        Author = "TBM13",
        Version = "1.0.0",
        IcoPath = "Images/Plugin.WindowsSettings.png",

        Plugin = new Main()
    };
}

public sealed class Main : IPlugin
{
    private PluginInitContext _context = null!;

    /// <summary>
    /// Key is a page's path (e.g. "System/Display/").
    /// Value is all the settings that should be shown on that path.
    /// </summary>
    private readonly Dictionary<string, List<Result>> _allSettings = [];

    public void Init(PluginInitContext ctx)
    {
        _context = ctx;
        AddPage(string.Empty, AllSettings.Settings);
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
        result.SubTitle = path.Replace("/", "  ˃  ");

        if (setting is SettingsPage page)
            result.AutocompleteText = (true, path + page.LocalPath);

        if (!_allSettings.TryGetValue(path, out List<Result>? pathSettings))
        {
            pathSettings = [];
            _allSettings[path] = pathSettings;
        }

        pathSettings.Add(result);
    }

    public List<Result>? Query(Query query)
    {
        // On empty queries, show all the root setting pages
        if (query.IsHomeQuery || (query.ActionKeyword.Length > 0 && string.IsNullOrWhiteSpace(query.Search)))
            return _allSettings[AllSettings.Settings.LocalPath];

        /*IEnumerable<Result> settingsToSearch;

        string search = query.Search.Replace('\\', '/');
        int lastSeparatorIndex = search.LastIndexOf('/');
        if (lastSeparatorIndex != -1)
        {
            string path = search[..(lastSeparatorIndex + 1)];
            if (_allSettings.TryGetValue(path, out List<Result>? pathSettings))
                return _context.API.Search(pathSettings, search[(lastSeparatorIndex + 1)..]);
        }*/

        return null;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
