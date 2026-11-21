using System.Windows.Controls;
using Flow.Launcher.Plugin.Program.Programs;
using Flow.Launcher.Plugin.Program.Settings;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;

namespace Flow.Launcher.Plugin.Program;

public static class PluginMetadataDefinition
{
    public static readonly PluginMetadata Metadata = new()
    {
        ID = "1acadb24-1a50-46ff-8674-702f4cc40029",
        ActionKeywords = ["*"],
        Name = "Programs",
        Description = "Search and launch installed programs.",
        Author = "TBM13",
        Version = "1.0.0",
        IcoPath = "Images/Plugin.Program.png",

        Plugin = new Main()
    };
}

public sealed class Main : IAsyncPlugin, ISettingProvider, IContextMenu
{
    private PluginInitContext _context = null!;
    private Settings.Settings _settings = null!;
    private ProgramIndex _index = null!;
    private SettingsViewModel _viewModel = null!;

    public async Task InitAsync(PluginInitContext context)
    {
        _context = context;
        _settings = context.API.LoadSettingJsonStorage<Settings.Settings>();
        _index = new ProgramIndex(context, _settings);
        _viewModel = new SettingsViewModel(_settings, _index, context);

        await _index.InitializeAsync().ConfigureAwait(false);
    }

    public async Task<List<Result>?> QueryAsync(Query query, CancellationToken token)
    {
        if (query.IsHomeQuery)
            return null;

        return await Task.Run(() => _index.Query(query.Search, token), token).ConfigureAwait(false);
    }

    public List<Result>? LoadContextMenus(Result selectedResult, ActionContext ctx)
    {
        if (selectedResult.ContextData is not IProgram program)
            return null;

        List<Result> menus = program.CreateContextMenus(_context);
        menus.Add(new Result
        {
            Title = "Hide",
            IconOrGlyph = "\xece4",
            Action = _ =>
            {
                _index.HideProgram(program);
                _context.API.ShowMsg("Success", "This program will no longer appear in your search results.");
                return false;
            }
        });

        return menus;
    }

    public Control CreateSettingPanel()
    {
        return new SettingsControl(_viewModel);
    }

    public ValueTask DisposeAsync()
    {
        return _index is not null ? _index.DisposeAsync() : ValueTask.CompletedTask;
    }
}
