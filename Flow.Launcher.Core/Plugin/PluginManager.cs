using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using ISavable = Flow.Launcher.Plugin.ISavable;

namespace Flow.Launcher.Core.Plugin
{
    /// <summary>
    /// Class for co-ordinating and managing all plugin lifecycle.
    /// </summary>
    public static class PluginManager
    {
        private static readonly string ClassName = nameof(PluginManager);

        private static readonly ConcurrentDictionary<string, PluginPair> _allLoadedPlugins = [];
        private static readonly ConcurrentDictionary<string, PluginPair> _allInitializedPlugins = [];
        private static readonly ConcurrentDictionary<string, PluginPair> _initFailedPlugins = [];
        private static readonly ConcurrentDictionary<string, PluginPair> _globalPlugins = [];
        private static readonly ConcurrentDictionary<string, PluginPair> _nonGlobalPlugins = [];

        private static PluginsSettings Settings;

        private static readonly ConcurrentBag<PluginPair> _contextMenuPlugins = [];
        private static readonly ConcurrentBag<PluginPair> _homePlugins = [];
        private static readonly ConcurrentBag<PluginPair> _translationPlugins = [];
        private static readonly ConcurrentBag<PluginPair> _externalPreviewPlugins = [];

        /// <summary>
        /// Directories that will hold Flow Launcher plugin directory
        /// </summary>
        public static readonly string[] Directories =
        [
            Constant.PreinstalledDirectory, DataLocation.PluginsDirectory
        ];

        #region Save & Dispose & Reload Plugin
        /// <summary>
        /// Save json and ISavable
        /// </summary>
        public static void Save()
        {
            foreach (var pluginPair in GetAllInitializedPlugins(includeFailed: false))
            {
                var savable = pluginPair.Plugin as ISavable;
                try
                {
                    savable?.Save();
                }
                catch (Exception e)
                {
                    PublicApi.Instance.LogException(ClassName, $"Failed to save plugin {pluginPair.Metadata.Name}", e);
                }
            }

            PublicApi.Instance.SavePluginSettings();
            PublicApi.Instance.SavePluginCaches();
        }

        public static async ValueTask DisposePluginsAsync()
        {
            // Still call dispose for all plugins even if initialization failed, so that we can clean up resources
            foreach (var pluginPair in GetAllInitializedPlugins(includeFailed: true))
            {
                await DisposePluginAsync(pluginPair);
            }
        }

        private static async Task DisposePluginAsync(PluginPair pluginPair)
        {
            try
            {
                switch (pluginPair.Plugin)
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
                PublicApi.Instance.LogException(ClassName, $"Failed to dispose plugin {pluginPair.Metadata.Name}", e);
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
            return GetExternalPreviewPlugins().Any(x => !x.Metadata.Disabled);
        }

        public static bool AllowAlwaysPreview()
        {
            var plugin = GetExternalPreviewPlugins().FirstOrDefault(x => !x.Metadata.Disabled);

            if (plugin is null)
                return false;

            return ((IAsyncExternalPreview)plugin.Plugin).AllowAlwaysPreview();
        }

        private static IList<PluginPair> GetExternalPreviewPlugins()
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
            var metadatas = PluginConfig.Parse(Directories);
            Settings = settings;
            Settings.UpdatePluginSettings(metadatas);

            // Load plugins
            var allLoadedPlugins = PluginsLoader.Plugins(metadatas, Settings);
            foreach (var plugin in allLoadedPlugins)
            {
                if (plugin != null)
                {
                    if (!_allLoadedPlugins.TryAdd(plugin.Metadata.ID, plugin))
                    {
                        PublicApi.Instance.LogError(ClassName, $"Plugin with ID {plugin.Metadata.ID} already loaded");
                    }
                }
            }

            // Since dotnet plugins need to get assembly name first, we should update plugin directory after loading plugins
            UpdatePluginDirectory(metadatas);
        }

        private static void UpdatePluginDirectory(List<PluginMetadata> metadatas)
        {
            foreach (var metadata in metadatas)
            {
                if (AllowedLanguage.IsDotNet(metadata.Language))
                {
                    if (string.IsNullOrEmpty(metadata.AssemblyName))
                    {
                        PublicApi.Instance.LogWarn(ClassName, $"AssemblyName is empty for plugin with metadata: {metadata.Name}");
                        continue; // Skip if AssemblyName is not set, which can happen for erroneous plugins
                    }
                    metadata.PluginSettingsDirectoryPath = Path.Combine(DataLocation.PluginSettingsDirectory, metadata.AssemblyName);
                    metadata.PluginCacheDirectoryPath = Path.Combine(DataLocation.PluginCacheDirectory, metadata.AssemblyName);
                }
                else
                {
                    if (string.IsNullOrEmpty(metadata.Name))
                    {
                        PublicApi.Instance.LogWarn(ClassName, $"Name is empty for plugin with metadata: {metadata.Name}");
                        continue; // Skip if Name is not set, which can happen for erroneous plugins
                    }
                    metadata.PluginSettingsDirectoryPath = Path.Combine(DataLocation.PluginSettingsDirectory, metadata.Name);
                    metadata.PluginCacheDirectoryPath = Path.Combine(DataLocation.PluginCacheDirectory, metadata.Name);
                }
            }
        }

        /// <summary>
        /// Initialize all plugins asynchronously.
        /// </summary>
        /// <param name="register">The register to register results updated event for each plugin.</param>
        /// <returns>return the list of failed to init plugins or null for none</returns>
        public static async Task InitializePluginsAsync(IResultUpdateRegister register)
        {
            var initTasks = _allLoadedPlugins.Select(x => Task.Run(async () =>
            {
                var pair = x.Value;

                // Register plugin action keywords so that plugins can be queried in results
                RegisterPluginActionKeywords(pair);

                try
                {
                    var milliseconds = await PublicApi.Instance.StopwatchLogDebugAsync(ClassName, $"Init method time cost for <{pair.Metadata.Name}>",
                        () => pair.Plugin.InitAsync(new PluginInitContext(pair.Metadata, PublicApi.Instance)));

                    pair.Metadata.InitTime += milliseconds;
                    PublicApi.Instance.LogInfo(ClassName,
                        $"Total init cost for <{pair.Metadata.Name}> is <{pair.Metadata.InitTime}ms>");
                }
                catch (Exception e)
                {
                    PublicApi.Instance.LogException(ClassName, $"Fail to Init plugin: {pair.Metadata.Name}", e);
                    if (pair.Metadata.Disabled && pair.Metadata.HomeDisabled)
                    {
                        // If this plugin is already disabled, do not show error message again
                        // Or else it will be shown every time
                        PublicApi.Instance.LogDebug(ClassName, $"Skipped init for <{pair.Metadata.Name}> due to error");
                    }
                    else
                    {
                        pair.Metadata.Disabled = true;
                        pair.Metadata.HomeDisabled = true;
                        PublicApi.Instance.LogDebug(ClassName, $"Disable plugin <{pair.Metadata.Name}> because init failed");
                    }

                    // Even if the plugin cannot be initialized, we still need to add it in all plugin list so that
                    // we can remove the plugin from Plugin or Store page or Plugin Manager plugin.
                    _allInitializedPlugins.TryAdd(pair.Metadata.ID, pair);
                    _initFailedPlugins.TryAdd(pair.Metadata.ID, pair);
                    return;
                }

                // Register ResultsUpdated event so that plugin query can use results updated interface
                register.RegisterResultsUpdatedEvent(pair);

                // Update plugin metadata translation after the plugin is initialized with IPublicAPI instance
                Internationalization.UpdatePluginMetadataTranslation(pair);

                // Add plugin to lists after the plugin is initialized
                AddPluginToLists(pair);
            }));

            await Task.WhenAll(initTasks);

            if (!_initFailedPlugins.IsEmpty)
            {
                var failed = string.Join(",", _initFailedPlugins.Values.Select(x => x.Metadata.Name));
                PublicApi.Instance.ShowMsg(
                    Localize.failedToInitializePluginsTitle(),
                    Localize.failedToInitializePluginsMessage(failed),
                    "",
                    false
                );
            }
        }

        private static void RegisterPluginActionKeywords(PluginPair pair)
        {
            // set distinct on each plugin's action keywords helps only firing global(*) and action keywords once where a plugin
            // has multiple global and action keywords because we will only add them here once.
            foreach (var actionKeyword in pair.Metadata.ActionKeywords.Distinct())
            {
                switch (actionKeyword)
                {
                    case Query.GlobalPluginWildcardSign:
                        _globalPlugins.TryAdd(pair.Metadata.ID, pair);
                        break;
                    default:
                        _nonGlobalPlugins.TryAdd(actionKeyword, pair);
                        break;
                }
            }
        }

        private static void AddPluginToLists(PluginPair pair)
        {
            if (pair.Plugin is IContextMenu)
            {
                _contextMenuPlugins.Add(pair);
            }
            if (pair.Plugin is IAsyncHomeQuery)
            {
                _homePlugins.Add(pair);
            }
            if (pair.Plugin is IPluginI18n)
            {
                _translationPlugins.Add(pair);
            }
            if (pair.Plugin is IAsyncExternalPreview)
            {
                _externalPreviewPlugins.Add(pair);
            }
            _allInitializedPlugins.TryAdd(pair.Metadata.ID, pair);
        }

        #endregion

        #region Validate & Query Plugins

        public static ICollection<PluginPair> ValidPluginsForQuery(Query query)
        {
            if (query is null)
                return Array.Empty<PluginPair>();

            if (!_nonGlobalPlugins.TryGetValue(query.ActionKeyword, out var plugin))
            {
                return [.. GetGlobalPlugins()];
            }

            return [plugin];
        }

        public static ICollection<PluginPair> ValidPluginsForHomeQuery()
        {
            return [.. _homePlugins];
        }

        public static async Task<List<Result>> QueryForPluginAsync(PluginPair pair, Query query, CancellationToken token)
        {
            var results = new List<Result>();
            var metadata = pair.Metadata;

            if (IsPluginInitializing(metadata))
            {
                Result r = new()
                {
                    Title = Localize.pluginStillInitializing(metadata.Name),
                    SubTitle = Localize.pluginStillInitializingSubtitle(),
                    AutoCompleteText = query.RawQuery,
                    IcoPath = metadata.IcoPath,
                    PluginDirectory = metadata.PluginDirectory,
                    ActionKeywordAssigned = query.ActionKeyword,
                    PluginID = metadata.ID,
                    OriginQuery = query,
                    Action = _ =>
                    {
                        PublicApi.Instance.ReQuery();
                        return false;
                    }
                };
                results.Add(r);
                return results;
            }

            try
            {
                var milliseconds = await PublicApi.Instance.StopwatchLogDebugAsync(ClassName, $"Cost for {metadata.Name}",
                    async () => results = await pair.Plugin.QueryAsync(query, token).ConfigureAwait(false));

                token.ThrowIfCancellationRequested();
                if (results == null)
                    return null;
                UpdatePluginMetadata(results, metadata, query);

                metadata.QueryCount += 1;
                metadata.AvgQueryTime =
                    metadata.QueryCount == 1 ? milliseconds : (metadata.AvgQueryTime + milliseconds) / 2;
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
                    Title = Localize.pluginFailedToRespond(metadata.Name),
                    SubTitle = Localize.pluginFailedToRespondSubtitle(),
                    AutoCompleteText = query.RawQuery,
                    IcoPath = Constant.ErrorIcon,
                    PluginDirectory = metadata.PluginDirectory,
                    ActionKeywordAssigned = query.ActionKeyword,
                    PluginID = metadata.ID,
                    OriginQuery = query,
                    Action = _ => { throw new FlowPluginException(metadata, e); }
                };
                results.Add(r);
            }
            return results;
        }

        public static async Task<List<Result>> QueryHomeForPluginAsync(PluginPair pair, Query query, CancellationToken token)
        {
            var results = new List<Result>();
            var metadata = pair.Metadata;

            if (IsPluginInitializing(metadata))
            {
                Result r = new()
                {
                    Title = Localize.pluginStillInitializing(metadata.Name),
                    SubTitle = Localize.pluginStillInitializingSubtitle(),
                    AutoCompleteText = query.RawQuery,
                    IcoPath = metadata.IcoPath,
                    PluginDirectory = metadata.PluginDirectory,
                    ActionKeywordAssigned = query.ActionKeyword,
                    PluginID = metadata.ID,
                    OriginQuery = query,
                    Action = _ =>
                    {
                        PublicApi.Instance.ReQuery();
                        return false;
                    }
                };
                results.Add(r);
                return results;
            }

            try
            {
                var milliseconds = await PublicApi.Instance.StopwatchLogDebugAsync(ClassName, $"Cost for {metadata.Name}",
                    async () => results = await ((IAsyncHomeQuery)pair.Plugin).HomeQueryAsync(token).ConfigureAwait(false));

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
                PublicApi.Instance.LogException(ClassName, $"Failed to query home for plugin: {metadata.Name}", e);
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

        public static List<PluginPair> GetAllLoadedPlugins()
        {
            return [.. _allLoadedPlugins.Values];
        }

        public static List<PluginPair> GetAllInitializedPlugins(bool includeFailed)
        {
            if (includeFailed)
            {
                return [.. _allInitializedPlugins.Values];
            }
            else
            {
                return [.. _allInitializedPlugins.Values
                    .Where(p => !_initFailedPlugins.ContainsKey(p.Metadata.ID))];
            }
        }

        private static List<PluginPair> GetGlobalPlugins()
        {
            return [.. _globalPlugins.Values];
        }

        public static Dictionary<string, PluginPair> GetNonGlobalPlugins()
        {
            return _nonGlobalPlugins.ToDictionary();
        }

        public static List<PluginPair> GetTranslationPlugins()
        {
            return [.. _translationPlugins];
        }

        #endregion

        #region Update Metadata & Get Plugin

        public static void UpdatePluginMetadata(IReadOnlyList<Result> results, PluginMetadata metadata, Query query)
        {
            foreach (var r in results)
            {
                r.PluginDirectory = metadata.PluginDirectory;
                r.PluginID = metadata.ID;
                r.OriginQuery = query;

                // ActionKeywordAssigned is used for constructing MainViewModel's query text auto-complete suggestions
                // Plugins may have multi-actionkeywords eg. WebSearches. In this scenario it needs to be overriden on the plugin level
                if (metadata.ActionKeywords.Count == 1)
                    r.ActionKeywordAssigned = query.ActionKeyword;
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
        public static PluginPair GetPluginForId(string id)
        {
            return GetAllLoadedPlugins().FirstOrDefault(o => o.Metadata.ID == id);
        }

        #endregion

        #region Get Context Menus

        public static List<Result> GetContextMenusForPlugin(Result result)
        {
            var results = new List<Result>();
            var pluginPair = _contextMenuPlugins.FirstOrDefault(o => o.Metadata.ID == result.PluginID);
            if (pluginPair != null)
            {
                var plugin = (IContextMenu)pluginPair.Plugin;

                try
                {
                    results = plugin.LoadContextMenus(result) ?? results;
                    foreach (var r in results)
                    {
                        r.PluginDirectory = pluginPair.Metadata.PluginDirectory;
                        r.PluginID = pluginPair.Metadata.ID;
                        r.OriginQuery = result.OriginQuery;
                    }
                }
                catch (Exception e)
                {
                    PublicApi.Instance.LogException(ClassName,
                        $"Can't load context menus for plugin <{pluginPair.Metadata.Name}>",
                        e);
                }
            }

            return results;
        }

        #endregion

        #region Check Home Plugin

        public static bool IsHomePlugin(string id)
        {
            return _homePlugins.Any(p => p.Metadata.ID == id);
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
            return actionKeyword != Query.GlobalPluginWildcardSign
                && _nonGlobalPlugins.ContainsKey(actionKeyword);
        }

        /// <summary>
        /// used to add action keyword for multiple action keyword plugin
        /// e.g. web search
        /// </summary>
        public static void AddActionKeyword(string id, string newActionKeyword)
        {
            var plugin = GetPluginForId(id);
            if (newActionKeyword == Query.GlobalPluginWildcardSign)
            {
                _globalPlugins.TryAdd(id, plugin);
            }
            else
            {
                _nonGlobalPlugins.AddOrUpdate(newActionKeyword, plugin, (key, oldValue) => plugin);
            }

            // Update action keywords and action keyword in plugin metadata
            plugin.Metadata.ActionKeywords.Add(newActionKeyword);
            if (plugin.Metadata.ActionKeywords.Count > 0)
            {
                plugin.Metadata.ActionKeyword = plugin.Metadata.ActionKeywords[0];
            }
            else
            {
                plugin.Metadata.ActionKeyword = string.Empty;
            }
        }

        /// <summary>
        /// used to remove action keyword for multiple action keyword plugin
        /// e.g. web search
        /// </summary>
        public static void RemoveActionKeyword(string id, string oldActionkeyword)
        {
            var plugin = GetPluginForId(id);
            if (oldActionkeyword == Query.GlobalPluginWildcardSign
                && // Plugins may have multiple ActionKeywords that are global, eg. WebSearch
                plugin.Metadata.ActionKeywords
                    .Count(x => x == Query.GlobalPluginWildcardSign) == 1)
            {
                _globalPlugins.TryRemove(id, out _);
            }

            if (oldActionkeyword != Query.GlobalPluginWildcardSign)
            {
                _nonGlobalPlugins.TryRemove(oldActionkeyword, out _);
            }

            // Update action keywords and action keyword in plugin metadata
            plugin.Metadata.ActionKeywords.Remove(oldActionkeyword);
            if (plugin.Metadata.ActionKeywords.Count > 0)
            {
                plugin.Metadata.ActionKeyword = plugin.Metadata.ActionKeywords[0];
            }
            else
            {
                plugin.Metadata.ActionKeyword = string.Empty;
            }
        }

        #endregion
    }
}
