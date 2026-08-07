using System.Windows.Controls;
using Flow.Launcher.Interop.Shell;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using Flow.Launcher.Plugin.Explorer.Views;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;

namespace Flow.Launcher.Plugin.Explorer;

public static class PluginMetadataDefinition
{
    public static readonly PluginMetadata Metadata = new()
    {
        ID = "572be03c74c642baae319fc283e561a8",
        ActionKeywords = ["*"],
        Name = "Explorer",
        Description = "Explore and manage files and folders",
        Author = "Jeremy Wu",
        Version = "1.0.0",
        IcoPath = "Images/Plugin.Explorer.png",

        Plugin = new Main()
    };
}

public class Main : ISettingProvider, IAsyncPlugin, IContextMenu, IMagicQueryProvider
{
    internal static PluginInitContext Context { get; private set; } = null!;

    internal static Settings Settings { get; private set; } = null!;

    private SettingsViewModel _viewModel = null!;
    private ContextMenu _contextMenu = null!;
    private SearchManager _searchManager = null!;

    public Control CreateSettingPanel()
    {
        return new ExplorerSettings(_viewModel);
    }

    public Task InitAsync(PluginInitContext context)
    {
        Context = context;
        Settings = context.API.LoadSettingJsonStorage<Settings>();

        _viewModel = new SettingsViewModel(context, Settings);
        _contextMenu = new ContextMenu(Context, Settings);
        _searchManager = new SearchManager(Settings, Context);

        return Task.CompletedTask;
    }

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        return _contextMenu.LoadContextMenus(selectedResult);
    }

    public async Task<List<Result>?> QueryAsync(Query query, CancellationToken token)
    {
        if (query.IsHomeQuery)
            return null;

        return await _searchManager.SearchAsync(query, token);
    }

    public string? GenerateMagicQuery()
    {
        string? explorerPath = FileExplorerHelper.GetForegroundExplorerPath();
        return explorerPath;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
