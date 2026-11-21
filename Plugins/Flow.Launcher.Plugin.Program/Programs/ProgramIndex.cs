using System.IO;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Threading;
using Flow.Launcher.Plugin.Program.Settings;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Plugins;
using Windows.ApplicationModel;
using Windows.Foundation;

namespace Flow.Launcher.Plugin.Program.Programs;

internal sealed class ProgramIndex : IAsyncDisposable
{
    private const string Win32CacheName = "Win32Programs";
    private const string UwpCacheName = "UwpPrograms";
    private static readonly TimeSpan AutoReindexInterval = TimeSpan.FromHours(30);

    private readonly PluginInitContext _context;
    private readonly Settings.Settings _settings;
    private readonly SemaphoreSlim _reindexLock = new(1, 1);
    private readonly Lock _disabledLock = new();
    private readonly Lock _watchersLock = new();
    private readonly List<FileSystemWatcher> _fileWatchers = [];
    private readonly CancellationTokenSource _disposeCts = new();

    private readonly Channel<byte> _win32ChangeChannel = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Channel<byte> _uwpChangeChannel = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

    private Win32ProgramCache _win32Cache = new();
    private UwpProgramCache _uwpCache = new();
    private volatile IProgram[] _activePrograms = [];
    private PackageCatalog? _packageCatalog;
    private Task? _win32WatcherTask;
    private Task? _uwpWatcherTask;
    private int _indexingCount;
    private int _disposed;

    public event Action? IndexStateChanged;

    public bool IsIndexing => Volatile.Read(ref _indexingCount) > 0;
    public int IndexedProgramCount => _activePrograms.Length;

    public ProgramIndex(PluginInitContext context, Settings.Settings settings)
    {
        _context = context;
        _settings = settings;
    }

    public async Task InitializeAsync()
    {
        string cacheDir = _context.CurrentPluginMetadata.PluginCacheDirectoryPath;
        Directory.CreateDirectory(cacheDir);

        TryDeleteLegacyCacheFile(Path.Combine(cacheDir, "Win32.cache"));
        TryDeleteLegacyCacheFile(Path.Combine(cacheDir, "UWP.cache"));

        _win32Cache = await _context.API
            .LoadCacheBinaryStorageAsync(Win32CacheName, cacheDir, new Win32ProgramCache())
            .ConfigureAwait(false);

        _uwpCache = await _context.API
            .LoadCacheBinaryStorageAsync(UwpCacheName, cacheDir, new UwpProgramCache())
            .ConfigureAwait(false);

        RebuildActivePrograms();

        _context.Logger.LogInfo(
            $"Preloaded {_win32Cache.Programs.Length} Win32 programs and {_uwpCache.Programs.Length} UWP apps.");

        bool cacheEmpty = _win32Cache.Programs.Length == 0
            || (_settings.EnableUWP && UwpIndexer.IsSupported && _uwpCache.Programs.Length == 0);
        bool staleIndex = _settings.LastIndexTime == default
            || _settings.LastIndexTime + AutoReindexInterval < DateTime.Now;

        StartWatchers();

        if (cacheEmpty || staleIndex)
        {
            _ = ReindexAllAsync(_disposeCts.Token);
        }
    }

    public List<Result> Query(string search, CancellationToken token)
    {
        IProgram[] programs = _activePrograms;
        if (programs.Length == 0)
            return [];

        ReadOnlySpan<char> querySpan = search.AsSpan().Trim();
        List<Result> results = [];

        foreach (IProgram program in programs)
        {
            token.ThrowIfCancellationRequested();

            Result? result = program.CreateResult(querySpan, _settings, _context);
            if (result is not null)
                results.Add(result);
        }

        return results;
    }

    public async Task ReindexAllAsync(CancellationToken cancellationToken = default)
    {
        await ReindexInternalAsync(includeWin32: true, includeUwp: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReindexWin32Async(CancellationToken cancellationToken = default)
    {
        await ReindexInternalAsync(includeWin32: true, includeUwp: false, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReindexInternalAsync(
        bool includeWin32,
        bool includeUwp,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        BeginIndexing();
        try
        {
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _disposeCts.Token,
                cancellationToken);
            CancellationToken ct = linkedCts.Token;

            await _reindexLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (ct.IsCancellationRequested || Volatile.Read(ref _disposed) != 0)
                    return;

                string cacheDir = _context.CurrentPluginMetadata.PluginCacheDirectoryPath;

                if (includeUwp)
                {
                    UwpProgram[] uwps = await Task.Run(
                        () => UwpIndexer.IndexAll(_settings, _context),
                        ct).ConfigureAwait(false);

                    _uwpCache.Programs = uwps;
                    _context.API.SaveCacheBinaryStorage<UwpProgramCache>(UwpCacheName, cacheDir);
                }

                if (includeWin32)
                {
                    UwpProgram[] currentUwps = _uwpCache.Programs;
                    Win32Program[] win32s = await Task.Run(
                        () => Win32Indexer.IndexAll(_settings, currentUwps, _context),
                        ct).ConfigureAwait(false);

                    _win32Cache.Programs = win32s;
                    _context.API.SaveCacheBinaryStorage<Win32ProgramCache>(Win32CacheName, cacheDir);
                }

                _settings.LastIndexTime = DateTime.Now;
                _context.API.SaveSettingJsonStorage<Settings.Settings>();
                RebuildActivePrograms();
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                // Ignore cancellation or disposal during shutdown
            }
            catch (Exception ex)
            {
                _context.Logger.LogError(ex, $"Failed to reindex programs");
            }
            finally
            {
                try
                {
                    _reindexLock.Release();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            // Ignore cancellation or disposal while creating CTS or waiting on lock
        }
        finally
        {
            EndIndexing();
        }
    }

    public void HideProgram(IProgram program)
    {
        string id = ProgramSource.NormalizeId(program.UniqueIdentifier);
        if (string.IsNullOrEmpty(id))
            return;

        bool added = false;
        RunOnUiThread(() =>
        {
            lock (_disabledLock)
            {
                if (_settings.DisabledProgramSources.Any(
                    x => string.Equals(x.UniqueIdentifier, id, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                _settings.DisabledProgramSources.Add(new ProgramSource(program.Name, program.Location, id));
                RebuildActiveProgramsLocked();
                added = true;
            }
        });

        if (!added)
            return;

        IndexStateChanged?.Invoke();
        _context.API.SaveSettingJsonStorage<Settings.Settings>();
        _context.API.ReQuery();
    }

    public void UnhideProgram(ProgramSource source)
    {
        bool removed = false;
        RunOnUiThread(() =>
        {
            lock (_disabledLock)
            {
                removed = _settings.DisabledProgramSources.Remove(source);
                if (removed)
                    RebuildActiveProgramsLocked();
            }
        });

        if (!removed)
            return;

        IndexStateChanged?.Invoke();
        _context.API.SaveSettingJsonStorage<Settings.Settings>();
        _context.API.ReQuery();
    }

    public void UnhideAllPrograms()
    {
        bool cleared = false;
        RunOnUiThread(() =>
        {
            lock (_disabledLock)
            {
                if (_settings.DisabledProgramSources.Count == 0)
                    return;

                _settings.DisabledProgramSources.Clear();
                RebuildActiveProgramsLocked();
                cleared = true;
            }
        });

        if (!cleared)
            return;

        IndexStateChanged?.Invoke();
        _context.API.SaveSettingJsonStorage<Settings.Settings>();
        _context.API.ReQuery();
    }

    public void RefreshFileWatchers()
    {
        lock (_watchersLock)
        {
            ClearFileWatchersLocked();

            if (Volatile.Read(ref _disposed) != 0)
                return;

            List<string> rawPaths = [];
            if (_settings.EnableStartMenuSource)
                rawPaths.AddRange(Win32Indexer.GetStartMenuPaths());

            foreach (ProgramSource source in _settings.ProgramSources)
            {
                if (source.Enabled && !string.IsNullOrWhiteSpace(source.Location))
                    rawPaths.Add(source.Location);
            }

            List<string> watchDirs = Win32Indexer.GetCommonParents(rawPaths);
            HashSet<string> extensions = _settings.GetAllExtensions();

            foreach (string dir in watchDirs)
            {
                if (!Directory.Exists(dir))
                    continue;

                try
                {
                    FileSystemWatcher watcher = new(dir)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                    };

                    foreach (string ext in extensions)
                        watcher.Filters.Add($"*.{ext}");

                    watcher.Created += OnFileSystemChanged;
                    watcher.Deleted += OnFileSystemChanged;
                    watcher.Renamed += OnFileSystemRenamed;
                    watcher.EnableRaisingEvents = true;

                    _fileWatchers.Add(watcher);
                }
                catch (Exception ex)
                {
                    _context.Logger.LogDebug($"Failed to watch directory '{dir}': {ex.Message}");
                }
            }
        }
    }

    private void RebuildActivePrograms()
    {
        lock (_disabledLock)
        {
            RebuildActiveProgramsLocked();
        }

        IndexStateChanged?.Invoke();
    }

    private void RebuildActiveProgramsLocked()
    {
        HashSet<string> disabledIds = _settings.DisabledProgramSources
            .Select(x => x.UniqueIdentifier)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Win32Program[] win32s = _win32Cache.Programs;
        UwpProgram[] uwps = _settings.EnableUWP ? _uwpCache.Programs : [];

        List<IProgram> active = new(win32s.Length + uwps.Length);

        foreach (Win32Program p in win32s)
        {
            if (!disabledIds.Contains(p.UniqueIdentifier))
                active.Add(p);
        }

        foreach (UwpProgram p in uwps)
        {
            if (!disabledIds.Contains(p.UniqueIdentifier))
                active.Add(p);
        }

        _activePrograms = [.. active];
    }

    private void StartWatchers()
    {
        RefreshFileWatchers();

        _win32WatcherTask = Task.Run(() => RunDebouncedWatcherAsync(
            _win32ChangeChannel.Reader,
            TimeSpan.FromMilliseconds(500),
            ct => ReindexWin32Async(ct),
            _disposeCts.Token));

        if (UwpIndexer.IsSupported)
        {
            try
            {
                _packageCatalog = PackageCatalog.OpenForCurrentUser();
                _packageCatalog.PackageInstalling += OnPackageInstalling;
                _packageCatalog.PackageUninstalling += OnPackageUninstalling;
                _packageCatalog.PackageUpdating += OnPackageUpdating;

                _uwpWatcherTask = Task.Run(() => RunDebouncedWatcherAsync(
                    _uwpChangeChannel.Reader,
                    TimeSpan.FromSeconds(3),
                    ct => ReindexAllAsync(ct),
                    _disposeCts.Token));
            }
            catch (Exception ex)
            {
                _context.Logger.LogDebug($"Failed to start UWP package watcher: {ex.Message}");
            }
        }
    }

    private async Task RunDebouncedWatcherAsync(
        ChannelReader<byte> reader,
        TimeSpan debounceDelay,
        Func<CancellationToken, Task> reindexAction,
        CancellationToken ct)
    {
        try
        {
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                await Task.Delay(debounceDelay, ct).ConfigureAwait(false);
                while (reader.TryRead(out _))
                {
                }

                await reindexAction(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e) =>
        _win32ChangeChannel.Writer.TryWrite(default);

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e) =>
        _win32ChangeChannel.Writer.TryWrite(default);

    private void OnPackageInstalling(PackageCatalog sender, PackageInstallingEventArgs args)
    {
        if (args.IsComplete)
            _uwpChangeChannel.Writer.TryWrite(default);
    }

    private void OnPackageUninstalling(PackageCatalog sender, PackageUninstallingEventArgs args)
    {
        if (args.IsComplete)
            _uwpChangeChannel.Writer.TryWrite(default);
    }

    private void OnPackageUpdating(PackageCatalog sender, PackageUpdatingEventArgs args)
    {
        if (args.IsComplete)
            _uwpChangeChannel.Writer.TryWrite(default);
    }

    private void BeginIndexing()
    {
        Interlocked.Increment(ref _indexingCount);
        IndexStateChanged?.Invoke();
    }

    private void EndIndexing()
    {
        Interlocked.Decrement(ref _indexingCount);
        IndexStateChanged?.Invoke();
    }

    private void ClearFileWatchersLocked()
    {
        foreach (FileSystemWatcher watcher in _fileWatchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnFileSystemChanged;
            watcher.Deleted -= OnFileSystemChanged;
            watcher.Renamed -= OnFileSystemRenamed;
            watcher.Dispose();
        }

        _fileWatchers.Clear();
    }

    private static void RunOnUiThread(Action action)
    {
        Dispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
            dispatcher.Invoke(action);
        else
            action();
    }

    private static void TryDeleteLegacyCacheFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Ignore cleanup errors for legacy cache files
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _disposeCts.Cancel();

        lock (_watchersLock)
        {
            ClearFileWatchersLocked();
        }

        if (_packageCatalog is not null)
        {
            _packageCatalog.PackageInstalling -= OnPackageInstalling;
            _packageCatalog.PackageUninstalling -= OnPackageUninstalling;
            _packageCatalog.PackageUpdating -= OnPackageUpdating;
            _packageCatalog = null;
        }

        _win32ChangeChannel.Writer.TryComplete();
        _uwpChangeChannel.Writer.TryComplete();

        if (_win32WatcherTask is not null)
            await _win32WatcherTask.ConfigureAwait(false);

        if (_uwpWatcherTask is not null)
            await _uwpWatcherTask.ConfigureAwait(false);

        await _reindexLock.WaitAsync().ConfigureAwait(false);
        _reindexLock.Dispose();
        _disposeCts.Dispose();
    }
}
