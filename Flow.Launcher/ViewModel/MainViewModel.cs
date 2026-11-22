using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Controls;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Hotkeys;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.Core.Storage;
using Flow.Launcher.Core.UserSettings;
using Flow.Launcher.Interop.Shell;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;
using Microsoft.Extensions.Logging;

namespace Flow.Launcher.ViewModel;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly PluginSDK.Logging.Logger<MainViewModel> _logger;
    private readonly ISettingsAPI _settings;
    private readonly PluginManager _pluginManager;
    private readonly HotkeyManager _hotkeyManager;
    private readonly IImageLoader _imageLoader;

    private Query? _lastQuery;
    private string? _ignoredQueryText; // Used to ignore query text change when switching between context menu and query results

    private readonly JsonStorage<UserSelectedRecord> _userSelectedRecordStorage;
    private readonly UserSelectedRecord _userSelectedRecord;

    private CancellationTokenSource? _updateSource; // Used to cancel old query flows

    private readonly ResultsViewModel _results, _contextMenu;
    private List<Result>? _contextMenuItems;

    private bool _taskbarShownByFlow = false;

    public MainViewModel(ILoggerFactory loggerFactory,
        ISettingsAPI settings, PluginManager pluginManager, HotkeyManager hotkeyManager,
        IImageLoader imageLoader)
    {
        _logger = new(loggerFactory);
        _pluginManager = pluginManager;
        _hotkeyManager = hotkeyManager;
        _imageLoader = imageLoader;

        _queryText = "";
        _lastQuery = null;
        _ignoredQueryText = null; // null as invalid value

        _settings = settings;
        _settings.PropertyChanged += (_, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(_settings.WindowWidth):
                    OnPropertyChanged(nameof(MainWindowWidth));
                    break;
            }
        };

        _userSelectedRecordStorage = new JsonStorage<UserSelectedRecord>(
            loggerFactory, Path.Combine(DataLocation.SettingsDirectory, "UserSelectedRecord.json"));
        _userSelectedRecord = _userSelectedRecordStorage.TryLoad();

        _contextMenu = new ResultsViewModel(_settings)
        {
            LeftClickResultCommand = OpenResultCommand,
            RightClickResultCommand = LoadContextMenuCommand,
        };
        _results = new ResultsViewModel(_settings)
        {
            LeftClickResultCommand = OpenResultCommand,
            RightClickResultCommand = LoadContextMenuCommand,
        };
        SelectedResults = _results;
        LateSelectedResults = SelectedResults;

        _results.PropertyChanged += (o, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(_results.SelectedItem) when !_results.IsUpdating:
                    PreviewSelectedItem = _results.SelectedItem;
                    if (InternalPreviewVisible)
                        PreviewSelectedItem?.LoadPreviewImageAsync();
                    break;
            }
        };

        _results.Results.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(MiddleSeparatorVisibility));
        };
        _contextMenu.Results.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(MiddleSeparatorVisibility));
        };
    }

    [RelayCommand]
    public void ReQuery()
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(ReQuery);
            return;
        }

        if (!ContextMenuSelected)
        {
            // When we are re-querying, we should not delay the query
            _ = QueryResultsAsync(isReQuery: true);
        }
    }

    public void ReQuery(bool reselect)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(() => ReQuery(reselect));
            return;
        }

        if (ContextMenuSelected)
            SelectedResults = _results;

        // When we are re-querying, we should not delay the query
        _ = QueryResultsAsync(isReQuery: true, reSelect: reselect);
    }

    [RelayCommand]
    private async Task LoadContextMenuAsync()
    {
        if (!ContextMenuSelected)
        {
            // When switch to ContextMenu from QueryResults, but no item being chosen or it has no context menu, should do nothing
            // i.e. Shift+Enter/Ctrl+O right after Alt + Space, or Right arrow on a result without context menu, should do nothing
            var selected = _results.SelectedItem;
            if (selected?.PluginID is null
                || _pluginManager.GetPluginForId(selected.PluginID)?.Plugin is not IContextMenu)
                return;

            var context = await CreateActionContextAsync(selected);
            if (ContextMenuSelected || _results.SelectedItem != selected)
                return;

            var results = await _pluginManager.GetContextMenusForPluginAsync(selected.PluginID, selected.Result, context);
            if (ContextMenuSelected || _results.SelectedItem != selected || results.Count == 0)
                return;

            _contextMenuItems = results;
            SelectedResults = _contextMenu;
        }
        else
            SelectedResults = _results;
    }

    [RelayCommand]
    private void Backspace(object index)
    {
        if (_lastQuery is null)
        {
            _logger.LogError($"Backspace command called but _lastQuery is null");
            return;
        }

        string actionKeyword = _lastQuery.ActionKeyword.Length == 0
            ? string.Empty
            : _lastQuery.ActionKeyword + ' ';

        ReadOnlySpan<char> search = _lastQuery.Search;
        if (search.Length > 0 && search[^1] is '\\' or '/')
            search = search[..^1];

        int lastSeparatorIndex = Math.Max(search.LastIndexOf('\\'), search.LastIndexOf('/'));
        search = lastSeparatorIndex >= 0 ? search[..(lastSeparatorIndex + 1)] : string.Empty;

        ChangeQueryText($"{actionKeyword}{search}");
    }

    [RelayCommand]
    private void AutocompleteQuery()
    {
        Result? result = SelectedResults.SelectedItem?.Result;
        if (result is not null && !ContextMenuSelected)
        {
            if (result.AutocompleteText.HasValue)
            {
                string autocompleteText = result.AutocompleteText.Value.text;
                if (!result.AutocompleteText.Value.prependActionKeyword)
                    ChangeQueryText(autocompleteText);
                else
                {
                    string actionKeyword = _lastQuery!.ActionKeyword.Length == 0
                        ? string.Empty
                        : _lastQuery.ActionKeyword + ' ';

                    ChangeQueryText(actionKeyword + autocompleteText);
                }
            }
        }
    }

    [RelayCommand]
    private async Task OpenResultAsync()
    {
        var selectedItem = SelectedResults.SelectedItem;
        if (selectedItem is null)
            return;

        var result = selectedItem.Result;
        var context = await CreateActionContextAsync(selectedItem);
        var hideWindow = await result.ExecuteAsync(context);

        if (hideWindow)
            Hide();

        // Record user selected result for result ranking
        _userSelectedRecord.Add(result, _lastQuery);
    }

    private async Task<ActionContext> CreateActionContextAsync(ResultViewModel selectedItem)
    {
        var positionFunc = selectedItem.GetScreenCenterPoint
            ?? throw new NullReferenceException(nameof(selectedItem.GetScreenCenterPoint));
        var position = positionFunc() ?? throw new Exception("Failed to get result position");

        return new ActionContext
        {
            // not null means pressing modifier key + number, should ignore the modifier key
            PressedKeys = await _hotkeyManager.GetPressedKeysAsync(),
            ResultPosition = position
        };
    }

    #region BasicCommands

    [RelayCommand]
    private void OpenSetting()
    {
        IPublicAPI.Instance.OpenSettingDialog();
    }

    [RelayCommand]
    private void Esc()
    {
        if (ContextMenuSelected)
            SelectedResults = _results;
        else
            Hide();
    }

    [RelayCommand]
    public void CopyAlternative()
    {
        string? copyText = _results.SelectedItem?.Result.CopyText;
        if (copyText is not null)
            IPublicAPI.Instance.CopyToClipboard(copyText, directCopy: false);
    }

    #endregion

    #region ViewModel Properties

    private string _queryText;
    public string QueryText
    {
        get => _queryText;
        set
        {
            _queryText = value;
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void IncreaseWidth()
    {
        MainWindowWidth += 100;
        MainWindowLeft -= 50;
    }

    [RelayCommand]
    private void DecreaseWidth()
    {
        if (MainWindowWidth - 100 < 400)
            MainWindowWidth = 400;
        else
        {
            MainWindowWidth -= 100;
            MainWindowLeft += 50;
        }
    }

    [RelayCommand]
    private void IncreaseMaxResult()
    {
        if (_settings.MaxResultsToShow < 17)
            _settings.MaxResultsToShow++;
    }

    [RelayCommand]
    private void DecreaseMaxResult()
    {
        if (_settings.MaxResultsToShow > 2)
            _settings.MaxResultsToShow--;
    }

    /// <summary>
    /// we need move cursor to end when we manually changed query
    /// but we don't want to move cursor to end when query is updated from TextBox
    /// </summary>
    /// <param name="queryText"></param>
    /// <param name="isReQuery">Force query even when Query Text doesn't change</param>
    public void ChangeQueryText(string queryText, bool isReQuery = false)
    {
        // Must check access so that we will not block the UI thread which causes window visibility issue
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => ChangeQueryText(queryText, isReQuery));
            return;
        }

        if (QueryText != queryText)
        {
            // Change query text first
            QueryText = queryText;
            // When we are changing query from codes, we should not delay the query
            Query(isReQuery: false);

            // set to false so the subsequent set true triggers
            // PropertyChanged and MoveQueryTextToEnd is called
            QueryTextCursorMovedToEnd = false;
        }
        else if (isReQuery)
        {
            // When we are re-querying, we should not delay the query
            Query(isReQuery: true);
        }

        QueryTextCursorMovedToEnd = true;
    }

    /// <summary>
    /// Async version of <see cref="ChangeQueryText"/>
    /// </summary>
    private async Task ChangeQueryTextAsync(string queryText, bool isReQuery = false)
    {
        // Must check access so that we will not block the UI thread which causes window visibility issue
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            await Application.Current.Dispatcher.InvokeAsync(() => ChangeQueryTextAsync(queryText, isReQuery));
            return;
        }

        if (QueryText != queryText)
        {
            // Change query text first
            QueryText = queryText;
            // When we are changing query from codes, we should not delay the query
            await QueryAsync(isReQuery: false);

            // set to false so the subsequent set true triggers
            // PropertyChanged and MoveQueryTextToEnd is called
            QueryTextCursorMovedToEnd = false;
        }
        else if (isReQuery)
        {
            // When we are re-querying, we should not delay the query
            await QueryAsync(isReQuery: true);
        }

        QueryTextCursorMovedToEnd = true;
    }

    [ObservableProperty]
    public partial bool LastQuerySelected { get; set; }

    // This is not a reliable indicator of the cursor's position, it is manually set for a specific purpose.
    [ObservableProperty]
    public partial bool QueryTextCursorMovedToEnd { get; set; }

    private string _queryTextBeforeLeaveResults = string.Empty;
    public ResultsViewModel SelectedResults
    {
        get;
        private set
        {
            if (ReferenceEquals(field, value))
                return;

            bool isInitialAssignment = field is null;
            var isReturningFromContextMenu = ContextMenuSelected;
            field = value;
            OnPropertyChanged();

            if (isInitialAssignment)
                return;

            if (!ContextMenuSelected)
            {
                // QueryText setter (used in ChangeQueryText) runs the query again, resetting the selected
                // result from the one that was selected before going into the context menu to the first result.
                // The code below correctly restores QueryText and puts the text caret at the end without
                // running the query again when returning from the context menu.
                if (isReturningFromContextMenu)
                {
                    _contextMenuItems = null;
                    _queryText = _queryTextBeforeLeaveResults;
                    // When executing OnPropertyChanged, QueryTextBox_TextChanged1 and Query will be called
                    // So we need to ignore it so that we will not call Query again
                    _ignoredQueryText = _queryText;
                    OnPropertyChanged(nameof(QueryText));
                    QueryTextCursorMovedToEnd = true;
                }
                else
                {
                    ChangeQueryText(_queryTextBeforeLeaveResults);
                }
            }
            else
            {
                _queryTextBeforeLeaveResults = QueryText;
                QueryText = string.Empty;

                // setter won't be called when property value is not changed.
                // so we need manually call Query()
                if (_queryTextBeforeLeaveResults == string.Empty)
                    Query();
            }

            // Update LateSelectedResults later so UI doesn't flicker when entering context menu
            LateSelectedResults = field;

            OnPropertyChanged(nameof(MiddleSeparatorVisibility));
        }
    }

    [ObservableProperty]
    public partial ResultsViewModel LateSelectedResults { get; private set; }

    public Visibility MiddleSeparatorVisibility
        => SelectedResults.Results.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

    [ObservableProperty]
    public partial Visibility MainWindowVisibility { get; set; }

    // This is to be used for determining the visibility status of the main window instead of MainWindowVisibility
    // because it is more accurate and reliable representation than using Visibility as a condition check
    [ObservableProperty]
    public partial bool MainWindowVisibilityStatus { get; set; } = false;

    public double MainWindowWidth
    {
        get => _settings.WindowWidth;
        set
        {
            if (!MainWindowVisibilityStatus) return;
            _settings.WindowWidth = value;
        }
    }

    [ObservableProperty]
    public partial double MainWindowLeft { get; set; }

    [ObservableProperty]
    public partial ImageSource? PluginIconSource { get; private set; } = null;

    [ObservableProperty]
    public partial string? PluginIconPath { get; set; } = null;

    #endregion

    #region Preview

    private const int RESULTAREA_COLUMN_PREVIEWSHOWN = 1;
    private const int RESULTAREA_COLUMN_PREVIEWHIDDEN = 3;

    private DefaultPreview? _defaultPreview;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewContent))]
    [NotifyPropertyChangedFor(nameof(PreviewVisibility))]
    [NotifyPropertyChangedFor(nameof(PreviewMinHeight))]
    public partial ResultViewModel? PreviewSelectedItem { get; set; }

    public bool InternalPreviewVisible
    {
        get
        {
            if (ResultAreaColumn == RESULTAREA_COLUMN_PREVIEWSHOWN)
                return true;
            if (ResultAreaColumn == RESULTAREA_COLUMN_PREVIEWHIDDEN)
                return false;

            throw new InvalidOperationException();
        }
    }

    public Control? PreviewContent
    {
        get
        {
            if (!InternalPreviewVisible || PreviewSelectedItem == null)
                return null;

            if (PreviewSelectedItem.Result.PreviewPanel != null)
                return PreviewSelectedItem.Result.PreviewPanel.Value;

            _defaultPreview ??= new();
            _defaultPreview.DataContext = PreviewSelectedItem;
            return _defaultPreview;
        }
    }

    public Visibility PreviewVisibility =>
        !InternalPreviewVisible || PreviewSelectedItem == null ? Visibility.Collapsed : Visibility.Visible;

    public double PreviewMinHeight =>
        PreviewVisibility == Visibility.Visible ? 380 : 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InternalPreviewVisible))]
    [NotifyPropertyChangedFor(nameof(PreviewContent))]
    [NotifyPropertyChangedFor(nameof(PreviewVisibility))]
    [NotifyPropertyChangedFor(nameof(PreviewMinHeight))]
    public partial int ResultAreaColumn { get; set; } = RESULTAREA_COLUMN_PREVIEWHIDDEN;

    private void HidePreview()
    {
        if (InternalPreviewVisible)
            ResultAreaColumn = RESULTAREA_COLUMN_PREVIEWHIDDEN;
    }

    [RelayCommand]
    private void TogglePreview()
    {
        if (InternalPreviewVisible)
            HidePreview();
        else
            ShowInternalPreview();
    }

    private void ShowInternalPreview()
    {
        ResultAreaColumn = RESULTAREA_COLUMN_PREVIEWSHOWN;
        PreviewSelectedItem?.LoadPreviewImageAsync();
    }

    public void ResetPreview()
    {
        if (_settings.AlwaysPreview)
            ShowInternalPreview();
        else
            HidePreview();
    }

    #endregion

    #region Query

    public void QueryResults()
    {
        _ = QueryResultsAsync();
    }

    public void Query(bool isReQuery = false)
    {
        if (_ignoredQueryText != null)
        {
            if (_ignoredQueryText == QueryText)
            {
                _ignoredQueryText = null;
                return;
            }
            else
            {
                // If _ignoredQueryText does not match current QueryText, we should still execute Query
                _ignoredQueryText = null;
            }
        }

        if (!ContextMenuSelected)
            _ = QueryResultsAsync(isReQuery);
        else
            QueryContextMenu();
    }

    private async Task QueryAsync(bool isReQuery = false)
    {
        if (!ContextMenuSelected)
            await QueryResultsAsync(isReQuery);
        else
            QueryContextMenu();
    }

    private void QueryContextMenu()
    {
        if (_contextMenuItems is null || _contextMenuItems.Count == 0)
        {
            _contextMenu.Clear();
            return;
        }

        ReadOnlySpan<char> query = QueryText.AsSpan().Trim();
        List<ResultViewModel> viewModels;

        if (!query.IsEmpty)
        {
            viewModels = [];
            foreach (Result result in _contextMenuItems)
            {
                MatchResult match = IPublicAPI.Instance.StringMatcher.FuzzySearchBest(query, result.Title, result.SubTitle);
                if (match.IsThresholdMet)
                    viewModels.Add(new ResultViewModel(_imageLoader, result, score: match.Score));
            }

            viewModels = [.. viewModels.OrderByDescending(r => r.Score)];
        }
        else
        {
            viewModels = [.. _contextMenuItems.Select(r => new ResultViewModel(_imageLoader, r))];
        }

        _contextMenu.UpdateResults(viewModels);
    }

    private async Task QueryResultsAsync(bool isReQuery = false, bool reSelect = true)
    {
        const int initialBatchWindowMs = 20;
        const int streamingFlushIntervalMs = 25;

        CancellationTokenSource currentSource = new();
        CancellationTokenSource? previousSource = Interlocked.Exchange(ref _updateSource, currentSource);
        if (previousSource is not null)
            await previousSource.CancelAsync();

        CancellationToken token = currentSource.Token;
        Dispatcher? uiDispatcher = Application.Current?.Dispatcher;
        Task? allPluginsTask = null;

        try
        {
            if (token.IsCancellationRequested)
                return;

            Query? previousQuery = _lastQuery;
            Query query = _pluginManager.BuildQuery(QueryText, isReQuery);
            _lastQuery = query;

            _logger.LogDebug($"Start query with text: <{QueryText}> & ActionKeyword <{query.ActionKeyword}> and query <{query.OriginalQuery}>");

            IReadOnlyList<PluginMetadata> plugins;
            if (query.IsHomeQuery)
            {
                plugins = _settings.ShowHomePage
                    ? _pluginManager.GetAllInitializedPlugins(includeFailed: false)
                    : [];
            }
            else
            {
                plugins = _pluginManager.ValidPluginsForQuery(query);
            }

            _ = UpdatePluginIconAsync(query, plugins, token);

            List<PluginMetadata> activePlugins = [.. plugins.Where(p => query.IsHomeQuery ? !p.HomeDisabled : !p.Disabled)];
            _logger.LogDebug($"Valid <{activePlugins.Count}> plugins: {string.Join(" ", activePlugins.Select(x => $"<{x.Name}>"))}");

            if (activePlugins.Count == 0)
            {
                _results.Clear();
                return;
            }

            bool queryModeChanged = previousQuery is not null
                && (previousQuery.IsHomeQuery != query.IsHomeQuery
                    || !string.Equals(previousQuery.ActionKeyword, query.ActionKeyword, StringComparison.Ordinal));

            Lock collectedLock = new();
            List<ResultViewModel> collectedResults = [];
            int hasPendingResults = 0;
            bool hasFlushedOnce = false;

            allPluginsTask = Task.WhenAll(activePlugins.Select(plugin => Task.Run(async () =>
            {
                int priorityScore = plugin.Priority * 150;
                await foreach (Result result in _pluginManager.QueryForPluginAsync(plugin, query, token).ConfigureAwait(false))
                {
                    if (token.IsCancellationRequested)
                        return;

                    int finalScore = CalculateFinalScore(result, priorityScore, query);
                    ResultViewModel vm = new(_imageLoader, result, plugin.ID, finalScore);

                    lock (collectedLock)
                    {
                        collectedResults.Add(vm);
                    }

                    Volatile.Write(ref hasPendingResults, 1);
                }
            }, token)));

            await Task.Run(async () =>
            {
                Task completed = await Task.WhenAny(allPluginsTask, Task.Delay(initialBatchWindowMs, token)).ConfigureAwait(false);
                if (token.IsCancellationRequested)
                    return;

                if (completed != allPluginsTask)
                {
                    if (Volatile.Read(ref hasPendingResults) != 0)
                    {
                        await FlushSnapshotAsync().ConfigureAwait(false);
                    }
                    else if (queryModeChanged)
                    {
                        await RunOnUiAsync(() =>
                        {
                            if (!token.IsCancellationRequested)
                                _results.Clear();
                        }).ConfigureAwait(false);
                    }

                    while (!allPluginsTask.IsCompleted && !token.IsCancellationRequested)
                    {
                        await Task.WhenAny(allPluginsTask, Task.Delay(streamingFlushIntervalMs, token)).ConfigureAwait(false);
                        if (token.IsCancellationRequested)
                            return;

                        if (!allPluginsTask.IsCompleted && Volatile.Read(ref hasPendingResults) != 0)
                            await FlushSnapshotAsync().ConfigureAwait(false);
                    }
                }

                await allPluginsTask.ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                    await FlushSnapshotAsync().ConfigureAwait(false);
            });

            async Task FlushSnapshotAsync()
            {
                if (Interlocked.Exchange(ref hasPendingResults, 0) == 0 && hasFlushedOnce)
                    return;

                ResultViewModel[] sorted;
                lock (collectedLock)
                {
                    if (collectedResults.Count == 0 && hasFlushedOnce)
                        return;

                    sorted = [.. collectedResults.OrderByDescending(static r => r.Score)];
                }

                if (token.IsCancellationRequested)
                    return;

                bool isFirstFlush = !hasFlushedOnce;
                hasFlushedOnce = true;

                await RunOnUiAsync(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    bool effectiveReselect = reSelect && (isFirstFlush || _results.SelectedIndex <= 0);
                    _results.UpdateResults(sorted, effectiveReselect);
                }).ConfigureAwait(false);
            }

            Task RunOnUiAsync(Action action)
            {
                if (uiDispatcher is not null && !uiDispatcher.CheckAccess())
                    return uiDispatcher.InvokeAsync(action).Task;

                action();
                return Task.CompletedTask;
            }
        }
        catch (OperationCanceledException)
        {
            // Query was superseded by a newer query
        }
        finally
        {
            Interlocked.CompareExchange(ref _updateSource, null, currentSource);
            if (allPluginsTask is not null && !allPluginsTask.IsCompleted)
            {
                _ = allPluginsTask.ContinueWith(
                    static (_, state) => ((CancellationTokenSource)state!).Dispose(),
                    currentSource,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            else
            {
                currentSource.Dispose();
            }
        }
    }

    private async Task UpdatePluginIconAsync(Query query, IReadOnlyList<PluginMetadata> plugins, CancellationToken token)
    {
        if (query.IsHomeQuery || plugins.Count != 1)
        {
            PluginIconPath = null;
            PluginIconSource = null;
            return;
        }

        string iconPath = plugins[0].IcoPath;
        PluginIconPath = iconPath;

        try
        {
            ImageSource icon = await _imageLoader.LoadAsync(iconPath);
            if (!token.IsCancellationRequested)
                PluginIconSource = icon;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to load plugin icon '{iconPath}'");
        }
    }

    private int CalculateFinalScore(Result result, int priorityScore, Query query)
    {
        int selectedCount = result.AddSelectedCount
            ? _userSelectedRecord.GetSelectedCount(result, query)
            : 0;

        long total = (long)result.Score + selectedCount + priorityScore;
        return (int)Math.Clamp(total, int.MinValue, Result.MaxScore);
    }

    public bool ContextMenuSelected => SelectedResults == _contextMenu;

    #endregion

    #region Public Methods

#pragma warning disable VSTHRD100 // Avoid async void methods

    public void Show()
    {
        // Update WPF properties
        MainWindowVisibility = Visibility.Visible;
        MainWindowVisibilityStatus = true;

        // Show the taskbar if the setting is enabled
        if (_settings.ShowTaskbarWhenOpened && !_taskbarShownByFlow)
        {
            TaskbarHelper.ShowTaskbar();
            _taskbarShownByFlow = true;
        }
    }

    public async void Hide()
    {
        if (ContextMenuSelected)
            SelectedResults = _results;

        switch (_settings.LastQueryMode)
        {
            case LastQueryMode.Empty:
                await ChangeQueryTextAsync(string.Empty);
                break;
            case LastQueryMode.Preserved:
            case LastQueryMode.Selected:
                LastQuerySelected = _settings.LastQueryMode == LastQueryMode.Preserved;
                break;
            case LastQueryMode.ActionKeywordPreserved:
            case LastQueryMode.ActionKeywordSelected:
                var newQuery = _lastQuery?.ActionKeyword;

                if (!string.IsNullOrEmpty(newQuery))
                    newQuery += " ";
                await ChangeQueryTextAsync(newQuery ?? string.Empty);

                if (_settings.LastQueryMode == LastQueryMode.ActionKeywordSelected)
                    LastQuerySelected = false;
                break;
        }

        // Hide the taskbar if the setting is enabled
        if (_taskbarShownByFlow)
        {
            TaskbarHelper.HideTaskbar();
            _taskbarShownByFlow = false;
        }

        // Update WPF properties
        MainWindowVisibilityStatus = false;
        MainWindowVisibility = Visibility.Collapsed;
    }

#pragma warning restore VSTHRD100 // Avoid async void methods

    /// <summary>
    /// Save user selected records
    /// </summary>
    public bool TrySave()
    {
        return _userSelectedRecord.SaveTo(_userSelectedRecordStorage);
    }

    #endregion

    private bool _disposed = false;
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            CancellationTokenSource? activeSource = Interlocked.Exchange(ref _updateSource, null);
            if (activeSource is not null)
            {
                try
                {
                    activeSource.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
    }
}
