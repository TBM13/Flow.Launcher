using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Plugins;
using Flow.Launcher.Infrastructure.Plugins.Interfaces;
using Flow.Launcher.Infrastructure.Results;
using Flow.Launcher.Infrastructure.UserSettings;
using ISavable = Flow.Launcher.Infrastructure.Plugins.Interfaces.ISavable;

namespace Flow.Launcher.Core.Plugin
{
    /// <summary>
    /// Class for co-ordinating and managing all plugin lifecycle.
    /// </summary>
    public static class PluginManager
    {
        private static readonly string ClassName = nameof(PluginManager);

        private static readonly PluginMetadata[] Plugins =
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

        private static readonly ConcurrentDictionary<string, PluginMetadata> _allLoadedPlugins = [];
        private static readonly ConcurrentDictionary<string, PluginMetadata> _allInitializedPlugins = [];
        private static readonly ConcurrentDictionary<string, PluginMetadata> _initFailedPlugins = [];
        private static readonly ConcurrentDictionary<string, PluginMetadata> _globalPlugins = [];
        private static readonly ConcurrentDictionary<string, PluginMetadata> _nonGlobalPlugins = [];

        private static PluginsSettings Settings;

        private static readonly ConcurrentBag<PluginMetadata> _contextMenuPlugins = [];
        private static readonly ConcurrentBag<PluginMetadata> _homePlugins = [];
        private static readonly ConcurrentBag<PluginMetadata> _externalPreviewPlugins = [];

        #region Save & Dispose & Reload Plugin
        /// <summary>
        /// Save json and ISavable
        /// </summary>
        public static void Save()
        {
            foreach (var metadata in GetAllInitializedPlugins(includeFailed: false))
            {
                var savable = metadata.Plugin as ISavable;
                try
                {
                    savable?.Save();
                }
                catch (Exception e)
                {
                    IPublicAPI.Instance.LogException(ClassName, $"Failed to save plugin {metadata.Name}", e);
                }
            }

            IPublicAPI.Instance.SavePluginSettings();
            IPublicAPI.Instance.SavePluginCaches();
        }

        public static async ValueTask DisposePluginsAsync()
        {
            // Still call dispose for all plugins even if initialization failed, so that we can clean up resources
            foreach (var pluginPair in GetAllInitializedPlugins(includeFailed: true))
            {
                await DisposePluginAsync(pluginPair);
            }
        }

        private static async Task DisposePluginAsync(PluginMetadata metadata)
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
                IPublicAPI.Instance.LogException(ClassName, $"Failed to dispose plugin {metadata.Name}", e);
            }
        }

        public static async Task ReloadDataAsync()
        {
            await Task.WhenAll([.. GetAllInitializedPlugins(includeFailed: false).Select(plugin => plugin.Plugin switch
            {
                IReloadable p => Task.Run(p.ReloadData),
                IAsyncReloadable p => p.ReloadDataAsync(),
                _ => Task.CompletedTask,
            })]);
        }

        #endregion

        #region External Preview

        public static async Task OpenExternalPreviewAsync(string path, bool sendFailToast = true)
        {
            await Task.WhenAll([.. GetAllInitializedPlugins(includeFailed: false).Select(plugin => plugin.Plugin switch
            {
                IAsyncExternalPreview p => p.OpenPreviewAsync(path, sendFailToast),
                _ => Task.CompletedTask,
            })]);
        }

        public static async Task CloseExternalPreviewAsync()
        {
            await Task.WhenAll([.. GetAllInitializedPlugins(includeFailed: false).Select(plugin => plugin.Plugin switch
            {
                IAsyncExternalPreview p => p.ClosePreviewAsync(),
                _ => Task.CompletedTask,
            })]);
        }

        public static async Task SwitchExternalPreviewAsync(string path, bool sendFailToast = true)
        {
            await Task.WhenAll([.. GetAllInitializedPlugins(includeFailed: false).Select(plugin => plugin.Plugin switch
            {
                IAsyncExternalPreview p => p.SwitchPreviewAsync(path, sendFailToast),
                _ => Task.CompletedTask,
            })]);
        }

        public static bool UseExternalPreview()
        {
            return GetExternalPreviewPlugins().Any(x => !x.Disabled);
        }

        public static bool AllowAlwaysPreview()
        {
            var plugin = GetExternalPreviewPlugins().FirstOrDefault(x => !x.Disabled);

            if (plugin is null)
                return false;

            return ((IAsyncExternalPreview)plugin.Plugin).AllowAlwaysPreview();
        }

        private static IList<PluginMetadata> GetExternalPreviewPlugins()
        {
            return [.. _externalPreviewPlugins];
        }

        #endregion

        #region Constructor

        static PluginManager()
        {
            // validate user directory
            Directory.CreateDirectory(DataLocation.PluginsDirectory);
        }

        #endregion

        #region Load & Initialize Plugins

        /// <summary>
        /// Load plugins from the directories specified in Directories.
        /// </summary>
        /// <param name="settings"></param>
        public static void LoadPlugins(PluginsSettings settings)
        {
            Settings = settings;
            Settings.UpdatePluginSettings(Plugins);

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
        public static async Task InitializePluginsAsync()
        {
            var initTasks = _allLoadedPlugins.Select(x => Task.Run(async () =>
            {
                var metadata = x.Value;

                // Register plugin action keywords so that plugins can be queried in results
                RegisterPluginActionKeywords(metadata);

                try
                {
                    await metadata.Plugin.InitAsync(new PluginInitContext(metadata, IPublicAPI.Instance));
                }
                catch (Exception e)
                {
                    IPublicAPI.Instance.LogException(ClassName, $"Fail to Init plugin: {metadata.Name}", e);
                    if (metadata.Disabled && metadata.HomeDisabled)
                    {
                        // If this plugin is already disabled, do not show error message again
                        // Or else it will be shown every time
                        IPublicAPI.Instance.LogDebug(ClassName, $"Skipped init for <{metadata.Name}> due to error");
                    }
                    else
                    {
                        metadata.Disabled = true;
                        metadata.HomeDisabled = true;
                        IPublicAPI.Instance.LogDebug(ClassName, $"Disable plugin <{metadata.Name}> because init failed");
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

        private static void RegisterPluginActionKeywords(PluginMetadata metadata)
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

        private static void AddPluginToLists(PluginMetadata metadata)
        {
            if (metadata.Plugin is IContextMenu)
            {
                _contextMenuPlugins.Add(metadata);
            }
            if (metadata.Plugin is IAsyncHomeQuery)
            {
                _homePlugins.Add(metadata);
            }
            if (metadata.Plugin is IAsyncExternalPreview)
            {
                _externalPreviewPlugins.Add(metadata);
            }
            _allInitializedPlugins.TryAdd(metadata.ID, metadata);
        }

        #endregion

        #region Validate & Query Plugins

        public static ICollection<PluginMetadata> ValidPluginsForQuery(Query query)
        {
            if (query is null)
                return Array.Empty<PluginMetadata>();

            if (!_nonGlobalPlugins.TryGetValue(query.ActionKeyword, out var plugin))
            {
                return [.. GetGlobalPlugins()];
            }

            return [plugin];
        }

        public static ICollection<PluginMetadata> ValidPluginsForHomeQuery()
        {
            return [.. _homePlugins];
        }

        public static async Task<List<Result>?> QueryForPluginAsync(PluginMetadata metadata, Query query, CancellationToken token)
        {
            var results = new List<Result>();

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
                results.Add(r);
                return results;
            }

            try
            {
                results = await metadata.Plugin.QueryAsync(query, token).ConfigureAwait(false);

                token.ThrowIfCancellationRequested();
                if (results == null)
                    return null;
                UpdatePluginMetadata(results, metadata, query);

                token.ThrowIfCancellationRequested();
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
                results.Add(r);
            }
            return results;
        }

        public static async Task<List<Result>?> QueryHomeForPluginAsync(PluginMetadata metadata, Query query, CancellationToken token)
        {
            var results = new List<Result>();

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
                results.Add(r);
                return results;
            }

            try
            {
                results = await ((IAsyncHomeQuery)metadata.Plugin).HomeQueryAsync(token).ConfigureAwait(false);

                token.ThrowIfCancellationRequested();
                if (results == null)
                    return null;
                UpdatePluginMetadata(results, metadata, query);

                token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                // null will be fine since the results will only be added into queue if the token hasn't been cancelled
                return null;
            }
            catch (Exception e)
            {
                IPublicAPI.Instance.LogException(ClassName, $"Failed to query home for plugin: {metadata.Name}", e);
                return null;
            }
            return results;
        }

        private static bool IsPluginInitializing(PluginMetadata metadata)
        {
            return !_allInitializedPlugins.ContainsKey(metadata.ID);
        }

        #endregion

        #region Get Plugin List

        public static List<PluginMetadata> GetAllLoadedPlugins()
        {
            return [.. _allLoadedPlugins.Values];
        }

        public static List<PluginMetadata> GetAllInitializedPlugins(bool includeFailed)
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

        private static List<PluginMetadata> GetGlobalPlugins()
        {
            return [.. _globalPlugins.Values];
        }

        public static Dictionary<string, PluginMetadata> GetNonGlobalPlugins()
        {
            return _nonGlobalPlugins.ToDictionary();
        }

        #endregion

        #region Update Metadata & Get Plugin

        public static void UpdatePluginMetadata(IReadOnlyList<Result> results, PluginMetadata metadata, Query query)
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
        public static PluginMetadata? GetPluginForId(string id)
        {
            return GetAllLoadedPlugins().FirstOrDefault(o => o.ID == id);
        }

        #endregion

        #region Get Context Menus

        public static List<Result> GetContextMenusForPlugin(Result result)
        {
            var results = new List<Result>();
            var metadata = _contextMenuPlugins.FirstOrDefault(o => o.ID == result.PluginID);
            if (metadata != null)
            {
                var plugin = (IContextMenu)metadata.Plugin;

                try
                {
                    results = plugin.LoadContextMenus(result) ?? results;
                    foreach (var r in results)
                    {
                        r.PluginID = metadata.ID;
                        r.OriginQuery = result.OriginQuery;
                    }
                }
                catch (Exception e)
                {
                    IPublicAPI.Instance.LogException(ClassName,
                        $"Can't load context menus for plugin <{metadata.Name}>",
                        e);
                }
            }

            return results;
        }

        #endregion

        #region Check Home Plugin

        public static bool IsHomePlugin(string id)
        {
            return _homePlugins.Any(p => p.ID == id);
        }

        #endregion

        #region Check Initializing & Init Failed

        public static bool IsInitializingOrInitFailed(string id)
        {
            // Id does not exist in loaded plugins
            if (!_allLoadedPlugins.ContainsKey(id)) return false;

            // Plugin initialized already
            if (_allInitializedPlugins.ContainsKey(id))
            {
                // Check if the plugin initialization failed
                return _initFailedPlugins.ContainsKey(id);
            }
            // Plugin is still initializing
            else
            {
                return true;
            }
        }

        public static bool IsInitializing(string id)
        {
            // Id does not exist in loaded plugins
            if (!_allLoadedPlugins.ContainsKey(id)) return false;

            // Plugin initialized already
            if (_allInitializedPlugins.ContainsKey(id))
            {
                return false;
            }
            // Plugin is still initializing
            else
            {
                return true;
            }
        }

        public static bool IsInitializationFailed(string id)
        {
            // Id does not exist in loaded plugins
            if (!_allLoadedPlugins.ContainsKey(id)) return false;

            // Plugin initialized already
            if (_allInitializedPlugins.ContainsKey(id))
            {
                // Check if the plugin initialization failed
                return _initFailedPlugins.ContainsKey(id);
            }
            // Plugin is still initializing
            else
            {
                return false;
            }
        }

        #endregion

        #region Plugin Action Keyword

        public static bool ActionKeywordRegistered(string actionKeyword)
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
        public static void AddActionKeyword(string id, string newActionKeyword)
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
        public static void RemoveActionKeyword(string id, string oldActionkeyword)
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
