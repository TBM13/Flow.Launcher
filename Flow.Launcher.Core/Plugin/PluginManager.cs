using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.UserSettings;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;
using Microsoft.Extensions.Logging;

namespace Flow.Launcher.Core.Plugin;

public class PluginManager(PluginSDK.Logging.Logger<PluginManager> logger) : IAsyncDisposable
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

    private readonly PluginSDK.Logging.Logger<PluginManager> _logger = logger;

    private readonly ConcurrentDictionary<string, PluginMetadata> _allLoadedPlugins = [];
    private readonly ConcurrentDictionary<string, InitializedPlugin> _allInitializedPlugins = [];
    private readonly ConcurrentDictionary<string, PluginMetadata> _nonGlobalPlugins = [];

    /// <summary>
    /// Cached set of global plugins, rebuilt only when global action keywords are registered or removed.
    /// </summary>
    private volatile PluginMetadata[] _globalPlugins = [];

    private bool _disposed;

    private sealed record InitializedPlugin(PluginMetadata Metadata, bool InitFailed);

    /// <summary>
    /// Load plugins from the directories specified in Directories.
    /// </summary>
    /// <param name="settings"></param>
    public void LoadPlugins(PluginsSettings settings)
    {
        settings.UpdatePluginSettings(Plugins);

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
                _allInitializedPlugins.TryAdd(metadata.ID, new(metadata, InitFailed: true));
                return;
            }

            // Add plugin to lists after the plugin is initialized
            _allInitializedPlugins.TryAdd(metadata.ID, new(metadata, InitFailed: false));
        }));

        await Task.WhenAll(initTasks);

        RefreshGlobalPlugins();

        var failed = _allInitializedPlugins.Values
            .Where(p => p.InitFailed)
            .Select(p => p.Metadata.Name)
            .ToList();

        if (failed.Count > 0)
        {
            IPublicAPI.Instance.ShowMsg(
                "Fail to Init Plugins",
                $"Plugins: {string.Join(",", failed)} - fail to load and would be disabled, please contact plugin creator for help",
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
            if (actionKeyword != Query.GlobalPluginWildcard)
            {
                _nonGlobalPlugins.TryAdd(actionKeyword, metadata);
            }
        }
    }

    public IReadOnlyList<PluginMetadata> ValidPluginsForQuery(Query query)
    {
        if (_nonGlobalPlugins.TryGetValue(query.ActionKeyword, out var plugin))
        {
            return [plugin];
        }

        return _globalPlugins;
    }

    private void RefreshGlobalPlugins()
    {
        _globalPlugins = [.. _allLoadedPlugins.Values
            .Where(p => p.ActionKeywords.Contains(Query.GlobalPluginWildcard))];
    }

    public Task<List<Result>?> QueryForPluginAsync(PluginMetadata metadata, Query query, CancellationToken token)
        => QueryPluginAsync(metadata, query, token, isHomeQuery: false);

    public Task<List<Result>?> QueryHomeForPluginAsync(PluginMetadata metadata, Query query, CancellationToken token)
        => QueryPluginAsync(metadata, query, token, isHomeQuery: true);

    private async Task<List<Result>?> QueryPluginAsync(PluginMetadata metadata, Query query, CancellationToken token, bool isHomeQuery)
    {
        if (IsPluginInitializing(metadata))
        {
            Result r = new()
            {
                Title = metadata.Name + ": This plugin is still initializing...",
                IconOrGlyph = metadata.IcoPath,
                PluginID = metadata.ID,
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

            if (results is not null)
            {
                token.ThrowIfCancellationRequested();
                UpdatePluginMetadata(results, metadata);
            }

            return results;
        }
        catch (OperationCanceledException)
        {
            // null will be fine since the results will only be added into queue if the token hasn't been cancelled
            return null;
        }
        catch (Exception e)
        {
            if (isHomeQuery)
            {
                _logger.LogError(e, $"Failed to query home for plugin: {metadata.Name}");
                return null;
            }

            Result r = new()
            {
                Title = metadata.Name + ": Failed to respond!",
                IconOrGlyph = Constant.ErrorIcon,
                PluginID = metadata.ID,
                Action = _ => { throw new FlowPluginException(metadata, e); }
            };

            return [r];
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

    public List<PluginMetadata> GetAllLoadedPlugins()
    {
        return [.. _allLoadedPlugins.Values];
    }

    public List<PluginMetadata> GetAllInitializedPlugins(bool includeFailed)
    {
        return [.. _allInitializedPlugins.Values
            .Where(p => includeFailed || !p.InitFailed)
            .Select(p => p.Metadata)];
    }

    public Dictionary<string, PluginMetadata> GetNonGlobalPlugins()
    {
        return _nonGlobalPlugins.ToDictionary();
    }

    public void UpdatePluginMetadata(IReadOnlyList<Result> results, PluginMetadata metadata)
    {
        foreach (var r in results)
        {
            r.PluginID = metadata.ID;
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
        _allLoadedPlugins.TryGetValue(id, out var plugin);
        return plugin;
    }

    public List<Result>? GetContextMenusForPlugin(Result result)
    {
        if (result.PluginID is null
            || !_allInitializedPlugins.TryGetValue(result.PluginID, out var entry)
            || entry.Metadata.Plugin is not IContextMenu plugin)
        {
            return null;
        }

        var metadata = entry.Metadata;

        try
        {
            List<Result>? results = plugin.LoadContextMenus(result);
            if (results is null)
                return null;

            foreach (Result r in results)
            {
                r.PluginID = metadata.ID;
            }

            return results;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Can't load context menus for plugin <{metadata.Name}>");
            return null;
        }
    }

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
        if (newActionKeyword != Query.GlobalPluginWildcard)
        {
            _nonGlobalPlugins.AddOrUpdate(newActionKeyword, plugin, (key, oldValue) => plugin);
        }

        // Update action keywords and action keyword in plugin metadata
        plugin.ActionKeywords.Add(newActionKeyword);

        if (newActionKeyword == Query.GlobalPluginWildcard)
        {
            RefreshGlobalPlugins();
        }
    }

    /// <summary>
    /// used to remove action keyword for multiple action keyword plugin
    /// e.g. web search
    /// </summary>
    public void RemoveActionKeyword(string id, string oldActionkeyword)
    {
        var plugin = GetPluginForId(id);
        if (oldActionkeyword != Query.GlobalPluginWildcard)
        {
            _nonGlobalPlugins.TryRemove(oldActionkeyword, out _);
        }

        // Update action keywords in plugin metadata
        plugin.ActionKeywords.Remove(oldActionkeyword);

        if (oldActionkeyword == Query.GlobalPluginWildcard)
        {
            RefreshGlobalPlugins();
        }
    }

    // TODO: Dispose plugins individually when they are disabled
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, true) == true)
            return;

        List<Task> disposeTasks = [.. GetAllInitializedPlugins(includeFailed: true)
            .Select(r => r.Plugin.DisposeAsync().AsTask())];

        if (disposeTasks.Count == 0)
            return;

        _logger.LogInfo($"Disposing {disposeTasks.Count} plugins");
        Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        Task allDisposedTask = Task.WhenAll(disposeTasks);
        Task completedTask = await Task.WhenAny(allDisposedTask, timeoutTask).ConfigureAwait(false);

        if (completedTask == timeoutTask)
            _logger.LogWarn($"One or more plugins timed out during disposal");

        // Extract all thrown exceptions
        List<Exception> exceptions = [.. disposeTasks
            .Where(t => t.IsFaulted && t.Exception is not null)
            .SelectMany(t => t.Exception!.InnerExceptions)];

        if (exceptions.Count > 0)
        {
            _logger.LogError(
                new AggregateException(exceptions),
                $"{exceptions.Count} plugin(s) failed to dispose"
            );
        }
    }
}
