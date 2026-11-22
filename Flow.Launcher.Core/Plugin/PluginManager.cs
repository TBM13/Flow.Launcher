using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Flow.Launcher.Core.UserSettings;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;
using Microsoft.Extensions.Logging;

namespace Flow.Launcher.Core.Plugin;

public class PluginManager : IAsyncDisposable
{
    private static readonly PluginMetadata[] DefaultPlugins =
    [
        Launcher.Plugin.Calculator.PluginMetadataDefinition.Metadata,
        Launcher.Plugin.MagicQuery.PluginMetadataDefinition.Metadata,
        Launcher.Plugin.Explorer.PluginMetadataDefinition.Metadata,
        Launcher.Plugin.PluginIndicator.PluginMetadataDefinition.Metadata,
        Launcher.Plugin.ProcessKiller.PluginMetadataDefinition.Metadata,
        Launcher.Plugin.Sys.PluginMetadataDefinition.Metadata,
        Launcher.Plugin.WindowsDevices.PluginMetadataDefinition.Metadata,
        Launcher.Plugin.WindowsRegistry.PluginMetadataDefinition.Metadata,
        Launcher.Plugin.WindowsServices.PluginMetadataDefinition.Metadata,
        Launcher.Plugin.WindowsSettings.PluginMetadataDefinition.Metadata,
        Launcher.Plugin.WindowsTasks.PluginMetadataDefinition.Metadata,
        Launcher.Plugin.Program.PluginMetadataDefinition.Metadata,
    ];

    private enum PluginInitState : byte
    {
        Uninitialized,
        Initializing,
        Initialized,
        Failed
    }

    private sealed class PluginEntry(PluginMetadata metadata)
    {
        public PluginMetadata Metadata { get; } = metadata;
        public volatile PluginInitState State = PluginInitState.Uninitialized;
    }

    private readonly ILoggerFactory _loggerFactory;
    private readonly PluginSDK.Logging.Logger<PluginManager> _logger;
    private readonly PluginMetadata[] _plugins;
    private readonly PluginEntry[] _entries;
    private readonly FrozenDictionary<string, PluginEntry> _pluginsById;
    private readonly Lock _keywordsLock = new();

    private PluginsSettings? _settings;
    private volatile FrozenDictionary<string, PluginMetadata> _nonGlobalPlugins = FrozenDictionary<string, PluginMetadata>.Empty;
    private volatile PluginMetadata[] _globalPlugins = [];
    private volatile PluginMetadata[] _initializedPlugins = [];
    private bool _disposed;

    public PluginManager(ILoggerFactory loggerFactory)
        : this(loggerFactory, DefaultPlugins)
    {
    }

    internal PluginManager(ILoggerFactory loggerFactory, IEnumerable<PluginMetadata> plugins)
    {
        _loggerFactory = loggerFactory;
        _logger = new(loggerFactory);
        _plugins = [.. plugins];
        _entries = new PluginEntry[_plugins.Length];

        Dictionary<string, PluginEntry> entries = new(_plugins.Length, StringComparer.Ordinal);
        for (int i = 0; i < _plugins.Length; i++)
        {
            PluginMetadata plugin = _plugins[i];
            PluginEntry entry = new(plugin);
            if (!entries.TryAdd(plugin.ID, entry))
                throw new InvalidOperationException($"Plugin with ID {plugin.ID} is already loaded");

            _entries[i] = entry;
        }

        _pluginsById = entries.ToFrozenDictionary(StringComparer.Ordinal);
        RebuildActionKeywordCaches();
    }

    /// <summary>
    /// Applies persisted settings and configures per-plugin directories.
    /// </summary>
    public void LoadPlugins(PluginsSettings settings)
    {
        _settings = settings;
        _initializedPlugins = [];
        settings.UpdatePluginSettings(_plugins);

        foreach (PluginEntry entry in _entries)
        {
            PluginMetadata plugin = entry.Metadata;
            plugin.PluginSettingsDirectoryPath = Path.Combine(DataLocation.PluginSettingsDirectory, plugin.ID);
            plugin.PluginCacheDirectoryPath = Path.Combine(DataLocation.PluginCacheDirectory, plugin.ID);
            entry.State = PluginInitState.Initializing;
        }

        RebuildActionKeywordCaches();
    }

    /// <summary>
    /// Initializes all loaded plugins asynchronously in parallel.
    /// </summary>
    public async Task InitializePluginsAsync()
    {
        RebuildActionKeywordCaches();

        IPublicAPI? api = IPublicAPI.Instance;
        Task[] initTasks = [.. _entries.Select(entry => Task.Run(async () =>
        {
            PluginMetadata metadata = entry.Metadata;
            entry.State = PluginInitState.Initializing;
            ILogger rawLogger = _loggerFactory.CreateLogger(metadata.Name);

            try
            {
                await metadata.Plugin.InitAsync(new PluginInitContext
                {
                    Logger = new(rawLogger),
                    API = api!,
                    CurrentPluginMetadata = metadata
                }).ConfigureAwait(false);

                entry.State = PluginInitState.Initialized;
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

                    if (_settings?.GetPluginSettings(metadata.ID) is { } pluginSettings)
                    {
                        pluginSettings.Disabled = true;
                        pluginSettings.HomeDisabled = true;
                    }

                    _logger.LogDebug($"Disable plugin <{metadata.Name}> because init failed");
                }

                entry.State = PluginInitState.Failed;
            }
        }))];

        await Task.WhenAll(initTasks).ConfigureAwait(false);

        _initializedPlugins = [.. _entries
            .Where(e => e.State == PluginInitState.Initialized)
            .Select(e => e.Metadata)];

        List<string> failed = [.. _entries
            .Where(p => p.State == PluginInitState.Failed)
            .Select(p => p.Metadata.Name)];

        if (failed.Count > 0)
        {
            RebuildActionKeywordCaches();
            api?.ShowMsg(
                "Fail to Init Plugins",
                $"Plugins: {string.Join(",", failed)} - fail to load and would be disabled, please contact plugin creator for help",
                string.Empty,
                false);
        }
    }

    private void RebuildActionKeywordCaches()
    {
        lock (_keywordsLock)
        {
            Dictionary<string, PluginMetadata> nonGlobal = new(StringComparer.Ordinal);
            List<PluginMetadata> global = [];

            foreach (PluginMetadata plugin in _plugins)
            {
                bool hasGlobal = false;
                foreach (string actionKeyword in plugin.ActionKeywords.Distinct(StringComparer.Ordinal))
                {
                    if (actionKeyword == Query.GlobalPluginWildcard)
                        hasGlobal = true;
                    else
                        nonGlobal.TryAdd(actionKeyword, plugin);
                }

                if (hasGlobal)
                    global.Add(plugin);
            }

            _nonGlobalPlugins = nonGlobal.ToFrozenDictionary(StringComparer.Ordinal);
            _globalPlugins = [.. global];
        }
    }

    public Query BuildQuery(string rawQuery, bool isReQuery)
        => QueryBuilder.Build(rawQuery, isReQuery, _nonGlobalPlugins);

    public IReadOnlyList<PluginMetadata> ValidPluginsForQuery(Query query)
    {
        if (!string.IsNullOrEmpty(query.ActionKeyword)
            && _nonGlobalPlugins.TryGetValue(query.ActionKeyword, out PluginMetadata? plugin))
        {
            return [plugin];
        }

        return _globalPlugins;
    }

    public async IAsyncEnumerable<Result> QueryForPluginAsync(
        PluginMetadata metadata,
        Query query,
        [EnumeratorCancellation] CancellationToken token)
    {
        if (!_pluginsById.TryGetValue(metadata.ID, out PluginEntry? entry)
            || entry.State == PluginInitState.Failed)
        {
            yield break;
        }

        if (entry.State != PluginInitState.Initialized)
        {
            if (query.IsHomeQuery)
                yield break;

            yield return new Result
            {
                Title = $"{metadata.Name}: This plugin is still initializing...",
                IconOrGlyph = metadata.IcoPath,
                Action = _ =>
                {
                    IPublicAPI.Instance.ReQuery();
                    return false;
                }
            };
            yield break;
        }

        IAsyncEnumerator<Result>? enumerator = null;
        Exception? queryException = null;
        try
        {
            enumerator = metadata.Plugin.QueryAsync(query, token).GetAsyncEnumerator(token);
        }
        catch (OperationCanceledException)
        {
            yield break;
        }
        catch (Exception e)
        {
            queryException = e;
        }

        if (queryException is not null)
        {
            if (HandleQueryException(metadata, query, queryException) is { } errorResult)
                yield return errorResult;
            yield break;
        }

        if (enumerator is null)
            yield break;

        await using (enumerator.ConfigureAwait(false))
        {
            while (true)
            {
                Result current = null!;
                bool moved = false;
                try
                {
                    moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                    if (moved)
                        current = enumerator.Current;
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
                catch (Exception e)
                {
                    queryException = e;
                }

                if (queryException is not null)
                {
                    if (HandleQueryException(metadata, query, queryException) is { } errorResult)
                        yield return errorResult;
                    yield break;
                }

                if (!moved || token.IsCancellationRequested)
                    yield break;

                yield return current;
            }
        }
    }

    private Result? HandleQueryException(PluginMetadata metadata, Query query, Exception e)
    {
        if (query.IsHomeQuery)
        {
            _logger.LogError(e, $"Failed to query home for plugin: {metadata.Name}");
            return null;
        }

        _logger.LogError(e, $"Failed to query plugin: {metadata.Name}");
        return new Result
        {
            Title = $"{metadata.Name}: Failed to respond!",
            IconOrGlyph = Constant.ErrorIcon,
            Action = _ => throw new FlowPluginException(metadata, e)
        };
    }

    public string? GenerateMagicQuery()
    {
        foreach (PluginEntry entry in _entries
            .Where(e => e.State == PluginInitState.Initialized && !e.Metadata.Disabled)
            .OrderByDescending(e => e.Metadata.Priority))
        {
            PluginMetadata metadata = entry.Metadata;
            if (metadata.Plugin is IMagicQueryProvider provider)
            {
                try
                {
                    string? query = provider.GenerateMagicQuery();
                    if (query is not null)
                        return query;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to generate magic query for plugin <{metadata.Name}>");
                }
            }
        }

        return null;
    }

    public IReadOnlyList<PluginMetadata> GetAllLoadedPlugins() => _plugins;

    public IReadOnlyList<PluginMetadata> GetAllInitializedPlugins(bool includeFailed)
    {
        if (!includeFailed)
            return _initializedPlugins;

        return [.. _entries
            .Where(p => p.State is PluginInitState.Initialized or PluginInitState.Failed)
            .Select(p => p.Metadata)];
    }

    /// <summary>
    /// Gets the specified plugin, or null if not found.
    /// </summary>
    /// <remarks>
    /// Plugin may not be initialized, so do not use its plugin model to execute any commands.
    /// </remarks>
    public PluginMetadata? GetPluginForId(string id)
        => _pluginsById.TryGetValue(id, out PluginEntry? entry) ? entry.Metadata : null;

    public async Task<List<Result>> GetContextMenusForPluginAsync(
        string pluginId,
        Result result,
        ActionContext context,
        CancellationToken token = default)
    {
        if (!_pluginsById.TryGetValue(pluginId, out PluginEntry? entry)
            || entry.State != PluginInitState.Initialized
            || entry.Metadata.Plugin is not IContextMenu contextMenu)
        {
            return [];
        }

        try
        {
            return await Task.Run(async () =>
            {
                List<Result> results = [];
                await foreach (Result menu in contextMenu.LoadContextMenusAsync(result, context, token).ConfigureAwait(false))
                {
                    token.ThrowIfCancellationRequested();
                    results.Add(menu);
                }
                return results;
            }, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Can't load context menus for plugin <{entry.Metadata.Name}>");
            return [];
        }
    }

    public bool ActionKeywordRegistered(string actionKeyword)
    {
        // This method only checks non-global action keywords (not '*')
        return actionKeyword != Query.GlobalPluginWildcard
            && _nonGlobalPlugins.ContainsKey(actionKeyword);
    }

    /// <summary>
    /// Adds an action keyword to a plugin.
    /// </summary>
    public void AddActionKeyword(string id, string newActionKeyword)
    {
        if (!_pluginsById.TryGetValue(id, out PluginEntry? entry))
            throw new KeyNotFoundException($"Plugin with ID '{id}' was not found.");

        lock (_keywordsLock)
        {
            PluginMetadata plugin = entry.Metadata;
            if (!plugin.ActionKeywords.Contains(newActionKeyword, StringComparer.Ordinal))
                plugin.ActionKeywords.Add(newActionKeyword);

            if (_settings?.GetPluginSettings(id) is { } pluginSettings
                && !pluginSettings.ActionKeywords.Contains(newActionKeyword, StringComparer.Ordinal))
            {
                pluginSettings.ActionKeywords.Add(newActionKeyword);
            }

            RebuildActionKeywordCaches();
        }
    }

    /// <summary>
    /// Removes an action keyword from a plugin.
    /// </summary>
    public void RemoveActionKeyword(string id, string oldActionKeyword)
    {
        if (!_pluginsById.TryGetValue(id, out PluginEntry? entry))
            throw new KeyNotFoundException($"Plugin with ID '{id}' was not found.");

        lock (_keywordsLock)
        {
            PluginMetadata plugin = entry.Metadata;
            plugin.ActionKeywords.Remove(oldActionKeyword);

            _settings?.GetPluginSettings(id)?.ActionKeywords.Remove(oldActionKeyword);

            RebuildActionKeywordCaches();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, true))
            return;

        Task[] disposeTasks = [.. _entries
            .Where(e => e.State != PluginInitState.Uninitialized)
            .Select(e => DisposePluginAsync(e.Metadata.Plugin))];

        if (disposeTasks.Length == 0)
            return;

        _logger.LogInfo($"Disposing {disposeTasks.Length} plugins");
        Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        Task allDisposedTask = Task.WhenAll(disposeTasks);
        Task completedTask = await Task.WhenAny(allDisposedTask, timeoutTask).ConfigureAwait(false);

        if (completedTask == timeoutTask)
            _logger.LogWarn($"One or more plugins timed out during disposal");

        List<Exception> exceptions = [.. disposeTasks
            .Where(t => t.IsFaulted && t.Exception is not null)
            .SelectMany(t => t.Exception!.InnerExceptions)];

        if (exceptions.Count > 0)
        {
            _logger.LogError(
                new AggregateException(exceptions),
                $"{exceptions.Count} plugin(s) failed to dispose");
        }
    }

    private static async Task DisposePluginAsync(IPlugin plugin)
        => await plugin.DisposeAsync().ConfigureAwait(false);
}
