using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Plugins;
using Flow.Launcher.Infrastructure.Plugins.Interfaces;
using Flow.Launcher.Plugin.Explorer.Exceptions;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using Flow.Launcher.Plugin.Explorer.Views;

namespace Flow.Launcher.Plugin.Explorer
{
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

    public class Main : ISettingProvider, IAsyncPlugin, IContextMenu
    {
        internal static PluginInitContext Context { get; set; }

        internal static Settings Settings { get; set; }

        private SettingsViewModel viewModel;

        private ContextMenu contextMenu;

        private SearchManager searchManager;

        public Control CreateSettingPanel()
        {
            return new ExplorerSettings(viewModel);
        }

        public Task InitAsync(PluginInitContext context)
        {
            Context = context;

            Settings = context.API.LoadSettingJsonStorage<Settings>();

            viewModel = new SettingsViewModel(context, Settings);
            contextMenu = new ContextMenu(Context, Settings);
            searchManager = new SearchManager(Settings, Context);
            ResultManager.Init(Context, Settings);

            return Task.CompletedTask;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return contextMenu.LoadContextMenus(selectedResult);
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            try
            {
                return await searchManager.SearchAsync(query, token);
            }
            catch (SearchException e)
            {
                return
                [
                    new()
                    {
                        Title = e.Message,
                        SubTitle = "Enter to copy the message to clipboard",
                        Score = 501,
                        AsyncAction =  _ =>
                        {
                            Context.API.CopyToClipboard(e.ToString());
                            return new ValueTask<bool>(true);
                        }
                    }
                ];
            }
        }
    }
}
