using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.UserSettings;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;
using Microsoft.Extensions.Logging;

namespace Flow.Launcher.Core.Plugin
{
    /// <summary>
    /// Class for co-ordinating and managing all plugin lifecycle.
    /// </summary>
    public class PluginManager
    {
        private readonly PluginMetadata[] Plugins =
        [
            Launcher.Plugin.Calculator.PluginMetadataDefinition.Metadata,
            Launcher.Plugin.Explorer.PluginMetadataDefinition.Metadata,
            Launcher.Plugin.PluginIndicator.PluginMetadataDefinition.Metadata,
            Launcher.Plugin.ProcessKiller.PluginMetadataDefinition.Metadata,
            Launcher.Plugin.Sys.PluginMetadataDefinition.Metadata,
            Launcher.Plugin.WindowsServices.PluginMetadataDefinition.Metadata,
            Launcher.Plugin.WindowsSettings.PluginMetadataDefinition.Metadata,
            Launcher.Plugin.WindowsTasks.PluginMetadataDefinition.Metadata,
            Launcher.Plugin.Program.PluginMetadataDefinition.Metadata,
        ];

        private readonly PluginSDK.Logging.Logger<PluginManager> _logger;

        private readonly ConcurrentDictionary<string, PluginMetadata> _allLoadedPlugins = [];
        private readonly ConcurrentDictionary<string, PluginMetadata> _allInitializedPlugins = [];
        private readonly ConcurrentDictionary<string, PluginMetadata> _initFailedPlugins = [];
        private readonly ConcurrentDictionary<string, PluginMetadata> _globalPlugins = [];
        private readonly ConcurrentDictionary<string, PluginMetadata> _nonGlobalPlugins = [];

        private PluginsSettings _settings;

        private readonly ConcurrentBag<PluginMetadata> _contextMenuPlugins = [];

        #region Dispose
        public async ValueTask DisposePluginsAsync()
        {
            // Still call dispose for all plugins even if initialization failed, so that we can clean up resources
            foreach (var pluginPair in GetAllInitializedPlugins(includeFailed: true))
            {
                await DisposePluginAsync(pluginPair);
            }
        }

        private async Task DisposePluginAsync(PluginMetadata metadata)
        {
            try
            {
                switch (metadata.Plugin)
                {
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync();
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to dispose plugin {metadata.Name}");
            }
        }

        #endregion

        public PluginManager(PluginSDK.Logging.Logger<PluginManager> logger)
        {
            _logger = logger;

            // validate user directory
            Directory.CreateDirectory(DataLocation.PluginsDirectory);
        }

        #region Load & Initialize Plugins

        /// <summary>
        /// Load plugins from the directories specified in Directories.
        /// </summary>
        /// <param name="settings"></param>
        public void LoadPlugins(PluginsSettings settings)
        {
            _settings = settings;
            _settings.UpdatePluginSettings(Plugins);

            // Load plugins
            foreach (var plugin in Plugins)
            {
                if (!_allLoadedPlugins.TryAdd(plugin.ID, plugin))
                    throw new Exception($"Plugin with ID {plugin.ID} already loaded");

                plugin.PluginSettingsDirectoryPath = Path.Combine(DataLocation.PluginSettingsDirectory, plugin.ID);
                plugin.PluginCacheDirectoryPath = Path.Combine(DataLocation.PluginCacheDirectory, plugin.ID);
            }
        }

        /// <summary>
        /// Initialize all plugins asynchronously.
        /// </summary>
        /// <param name="register">The register to register results updated event for each plugin.</param>
        /// <returns>return the list of failed to init plugins or null for none</returns>
        public async Task InitializePluginsAsync()
        {
            var initTasks = _allLoadedPlugins.Select(x => Task.Run(async () =>
            {
                var metadata = x.Value;

                // Register plugin action keywords so that plugins can be queried in results
                RegisterPluginActionKeywords(metadata);

                ILogger rawLogger = Ioc.Default.GetRequiredService<ILoggerFactory>().CreateLogger(metadata.Name);

                try
                {
                    await metadata.Plugin.InitAsync(new PluginInitContext()
                    {
                        Logger = new(rawLogger),
                        API = IPublicAPI.Instance,
                        CurrentPluginMetadata = metadata
                    });
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Fail to Init plugin: {metadata.Name}");
                    if (metadata.Disabled && metadata.HomeDisabled)
                    {
                        // If this plugin is already disabled, do not show error message again
                        // Or else it will be shown every time
                        _logger.LogDebug($"Skipped init for <{metadata.Name}> due to error");
                    }
                    else
                    {
                        metadata.Disabled = true;
                        metadata.HomeDisabled = true;
                        _logger.LogDebug($"Disable plugin <{metadata.Name}> because init failed");
                    }

                    // Even if the plugin cannot be initialized, we still need to add it in all plugin list so that
                    // we can remove the plugin from Plugin or Store page or Plugin Manager plugin.
                    _allInitializedPlugins.TryAdd(metadata.ID, metadata);
                    _initFailedPlugins.TryAdd(metadata.ID, metadata);
                    return;
                }

                // Add plugin to lists after the plugin is initialized
                AddPluginToLists(metadata);
            }));

            await Task.WhenAll(initTasks);

            if (!_initFailedPlugins.IsEmpty)
            {
                var failed = string.Join(",", _initFailedPlugins.Values.Select(x => x.Name));
                IPublicAPI.Instance.ShowMsg(
                    Localize.Plugins_FailToInit,
                    Localize.Plugins_FailToInit_Message(failed),
                    "",
                    false
                );
            }
        }

        private void RegisterPluginActionKeywords(PluginMetadata metadata)
        {
            // set distinct on each plugin's action keywords helps only firing global(*) and action keywords once where a plugin
            // has multiple global and action keywords because we will only add them here once.
            foreach (var actionKeyword in metadata.ActionKeywords.Distinct())
            {
                switch (actionKeyword)
                {
                    case Query.GlobalPluginWildcard:
                        _globalPlugins.TryAdd(metadata.ID, metadata);
                        break;
                    default:
                        _nonGlobalPlugins.TryAdd(actionKeyword, metadata);
                        break;
                }
            }
        }

        private void AddPluginToLists(PluginMetadata metadata)
        {
            if (metadata.Plugin is IContextMenu)
            {
                _contextMenuPlugins.Add(metadata);
            }
            _allInitializedPlugins.TryAdd(metadata.ID, metadata);
        }

        #endregion

        #region Validate & Query Plugins

        public ICollection<PluginMetadata> ValidPluginsForQuery(Query query)
        {
            if (!_nonGlobalPlugins.TryGetValue(query.ActionKeyword, out var plugin))
            {
                return [.. GetGlobalPlugins()];
            }

            return [plugin];
        }

        public async Task<List<Result>?> QueryForPluginAsync(PluginMetadata metadata, Query query, CancellationToken token)
        {
            if (IsPluginInitializing(metadata))
            {
                Result r = new()
                {
                    Title = Localize.Plugin_StillInitializing(metadata.Name),
                    SubTitle = Localize.Plugin_StillInitializing_Subtitle,
                    AutoCompleteText = query.TrimmedQuery,
                    IcoPath = metadata.IcoPath,
                    PluginID = metadata.ID,
                    OriginQuery = query,
                    Action = _ =>
                    {
                        IPublicAPI.Instance.ReQuery();
                        return false;
                    }
                };
                return [r];
            }

            try
            {
                List<Result>? results = await metadata.Plugin.QueryAsync(query, token).ConfigureAwait(false);

                token.ThrowIfCancellationRequested();
                if (results is null)
                    return null;
                UpdatePluginMetadata(results, metadata, query);

                token.ThrowIfCancellationRequested();
                return results;
            }
            catch (OperationCanceledException)
            {
                // null will be fine since the results will only be added into queue if the token hasn't been cancelled
                return null;
            }
            catch (Exception e)
            {
                Result r = new()
                {
                    Title = Localize.Plugin_FailedToRespond(metadata.Name),
                    SubTitle = Localize.Plugin_FailedToRespond_Subtitle,
                    AutoCompleteText = query.TrimmedQuery,
                    IcoPath = Constant.ErrorIcon,
                    PluginID = metadata.ID,
                    OriginQuery = query,
                    Action = _ => { throw new FlowPluginException(metadata, e); }
                };

                return [r];
            }
        }

        public async Task<List<Result>?> QueryHomeForPluginAsync(PluginMetadata metadata, Query query, CancellationToken token)
        {
            if (IsPluginInitializing(metadata))
            {
                Result r = new()
                {
                    Title = Localize.Plugin_StillInitializing(metadata.Name),
                    SubTitle = Localize.Plugin_StillInitializing_Subtitle,
                    AutoCompleteText = query.TrimmedQuery,
                    IcoPath = metadata.IcoPath,
                    PluginID = metadata.ID,
                    OriginQuery = query,
                    Action = _ =>
                    {
                        IPublicAPI.Instance.ReQuery();
                        return false;
                    }
                };
                return [r];
            }

            try
            {
                List<Result>? results = await metadata.Plugin.QueryAsync(query, token).ConfigureAwait(false);

                token.ThrowIfCancellationRequested();
                if (results is null)
                    return null;
                UpdatePluginMetadata(results, metadata, query);

                token.ThrowIfCancellationRequested();
                return results;
            }
            catch (OperationCanceledException)
            {
                // null will be fine since the results will only be added into queue if the token hasn't been cancelled
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to query home for plugin: {metadata.Name}");
                return null;
            }
        }

        private bool IsPluginInitializing(PluginMetadata metadata)
        {
            return !_allInitializedPlugins.ContainsKey(metadata.ID);
        }

        public string? GenerateMagicQuery()
        {
            foreach (var metadata in _allLoadedPlugins.Values)
            {
                if (metadata.Plugin is IMagicQueryProvider provider)
                {
                    string? query = provider.GenerateMagicQuery();
                    if (query is not null)
                        return query;
                }
            }

            return null;
        }

        #endregion

        #region Get Plugin List

        public List<PluginMetadata> GetAllLoadedPlugins()
        {
            return [.. _allLoadedPlugins.Values];
        }

        public List<PluginMetadata> GetAllInitializedPlugins(bool includeFailed)
        {
            if (includeFailed)
            {
                return [.. _allInitializedPlugins.Values];
            }
            else
            {
                return [.. _allInitializedPlugins.Values
                    .Where(p => !_initFailedPlugins.ContainsKey(p.ID))];
            }
        }

        private List<PluginMetadata> GetGlobalPlugins()
        {
            return [.. _globalPlugins.Values];
        }

        public Dictionary<string, PluginMetadata> GetNonGlobalPlugins()
        {
            return _nonGlobalPlugins.ToDictionary();
        }

        #endregion

        #region Update Metadata & Get Plugin

        public void UpdatePluginMetadata(IReadOnlyList<Result> results, PluginMetadata metadata, Query query)
        {
            foreach (var r in results)
            {
                r.PluginID = metadata.ID;
                r.OriginQuery = query;
            }
        }

        /// <summary>
        /// get specified plugin, return null if not found
        /// </summary>
        /// <remarks>
        /// Plugin may not be initialized, so do not use its plugin model to execute any commands
        /// </remarks>
        /// <param name="id"></param>
        /// <returns></returns>
        public PluginMetadata? GetPluginForId(string id)
        {
            return GetAllLoadedPlugins().FirstOrDefault(o => o.ID == id);
        }

        #endregion

        #region Get Context Menus

        public List<Result>? GetContextMenusForPlugin(Result result)
        {
            var metadata = _contextMenuPlugins.FirstOrDefault(o => o.ID == result.PluginID);
            if (metadata != null)
            {
                var plugin = (IContextMenu)metadata.Plugin;

                try
                {
                    List<Result>? results = plugin.LoadContextMenus(result);
                    if (results is null)
                        return null;

                    foreach (Result r in results)
                    {
                        r.PluginID = metadata.ID;
                        r.OriginQuery = result.OriginQuery;
                    }

                    return results;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Can't load context menus for plugin <{metadata.Name}>");
                    return null;
                }
            }

            return null;
        }

        #endregion

        #region Check Initializing & Init Failed

        #region Plugin Action Keyword

        public bool ActionKeywordRegistered(string actionKeyword)
        {
            // this method is only checking for action keywords (defined as not '*') registration
            // hence the actionKeyword != Query.GlobalPluginWildcardSign logic
            return actionKeyword != Query.GlobalPluginWildcard
                && _nonGlobalPlugins.ContainsKey(actionKeyword);
        }

        /// <summary>
        /// used to add action keyword for multiple action keyword plugin
        /// e.g. web search
        /// </summary>
        public void AddActionKeyword(string id, string newActionKeyword)
        {
            var plugin = GetPluginForId(id);
            if (newActionKeyword == Query.GlobalPluginWildcard)
            {
                _globalPlugins.TryAdd(id, plugin);
            }
            else
            {
                _nonGlobalPlugins.AddOrUpdate(newActionKeyword, plugin, (key, oldValue) => plugin);
            }

            // Update action keywords and action keyword in plugin metadata
            plugin.ActionKeywords.Add(newActionKeyword);
        }

        /// <summary>
        /// used to remove action keyword for multiple action keyword plugin
        /// e.g. web search
        /// </summary>
        public void RemoveActionKeyword(string id, string oldActionkeyword)
        {
            var plugin = GetPluginForId(id);
            if (oldActionkeyword == Query.GlobalPluginWildcard
                && // Plugins may have multiple ActionKeywords that are global, eg. WebSearch
                plugin.ActionKeywords
                    .Count(x => x == Query.GlobalPluginWildcard) == 1)
            {
                _globalPlugins.TryRemove(id, out _);
            }

            if (oldActionkeyword != Query.GlobalPluginWildcard)
            {
                _nonGlobalPlugins.TryRemove(oldActionkeyword, out _);
            }

            // Update action keywords in plugin metadata
            plugin.ActionKeywords.Remove(oldActionkeyword);
        }

        #endregion
    }
}
