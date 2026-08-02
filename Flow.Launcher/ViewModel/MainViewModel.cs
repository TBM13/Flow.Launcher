using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
using Flow.Launcher.Interop;
using Flow.Launcher.Interop.Shell;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;
using Flow.Launcher.Storage;
using iNKORE.UI.WPF.Modern;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;

namespace Flow.Launcher.ViewModel
{
    public partial class MainViewModel : ObservableObject, ISavable, IDisposable
    {
        private readonly PluginSDK.Logging.Logger<MainViewModel> _logger;
        private readonly PluginManager _pluginManager;
        private readonly HotkeyManager _hotkeyManager;

        private Query? _lastQuery;
        private bool _previousIsHomeQuery;
        private string? _ignoredQueryText; // Used to ignore query text change when switching between context menu and query results

        private readonly JsonStorage<UserSelectedRecord> _userSelectedRecordStorage;
        private readonly FlowLauncherJsonStorageTopMostRecord _topMostRecord;
        private readonly UserSelectedRecord _userSelectedRecord;

        private CancellationTokenSource? _updateSource; // Used to cancel old query flows

        private ChannelWriter<ResultsForUpdate> _resultsUpdateChannelWriter;
        private Task _resultsViewUpdateTask;

        private readonly ResultsViewModel _results, _contextMenu;
        private readonly IReadOnlyList<Result> _emptyResult = [];

        private bool _taskbarShownByFlow = false;

        public MainViewModel(ILoggerFactory loggerFactory,
            ISettingsAPI settings, PluginManager pluginManager, HotkeyManager hotkeyManager)
        {
            _logger = new(loggerFactory);
            _pluginManager = pluginManager;
            _hotkeyManager = hotkeyManager;

            _queryText = "";
            _lastQuery = null;
            _ignoredQueryText = null; // null as invalid value

            Settings = settings;
            Settings.PropertyChanged += (_, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(Settings.WindowWidth):
                        OnPropertyChanged(nameof(MainWindowWidth));
                        break;
                }
            };

            _userSelectedRecordStorage = new JsonStorage<UserSelectedRecord>(
                loggerFactory, Path.Combine(DataLocation.SettingsDirectory, "UserSelectedRecord.json"));
            _topMostRecord = new FlowLauncherJsonStorageTopMostRecord(loggerFactory);
            _userSelectedRecord = _userSelectedRecordStorage.TryLoad();

            PluginSDK.Logging.Logger<ResultsViewModel> resultsLogger = new(loggerFactory);
            _contextMenu = new ResultsViewModel(resultsLogger, this, Settings)
            {
                LeftClickResultCommand = OpenResultCommand,
                RightClickResultCommand = LoadContextMenuCommand,
                IsPreviewOn = Settings.AlwaysPreview
            };
            _results = new ResultsViewModel(resultsLogger, this, Settings)
            {
                LeftClickResultCommand = OpenResultCommand,
                RightClickResultCommand = LoadContextMenuCommand,
                IsPreviewOn = Settings.AlwaysPreview
            };
            SelectedResults = _results;
            LateSelectedResults = SelectedResults;

            _results.PropertyChanged += (o, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(_results.SelectedItem):
                        PreviewSelectedItem = _results.SelectedItem;
                        _ = UpdatePreviewAsync();
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

            RegisterViewUpdate();
        }

        private void RegisterViewUpdate()
        {
            var resultUpdateChannel = Channel.CreateUnbounded<ResultsForUpdate>();
            _resultsUpdateChannelWriter = resultUpdateChannel.Writer;
            _resultsViewUpdateTask =
                Task.Run(UpdateActionAsync).ContinueWith(continueAction, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

            async Task UpdateActionAsync()
            {
                var queue = new Dictionary<string, ResultsForUpdate>();
                var channelReader = resultUpdateChannel.Reader;

                // it is not supposed to be false because it won't be complete
                while (await channelReader.WaitToReadAsync())
                {
                    await Task.Delay(20);
                    while (channelReader.TryRead(out var item))
                    {
                        if (!item.Token.IsCancellationRequested)
                        {
                            // Indicate if to clear existing results so to show only ones from plugins with action keywords
                            var query = item.Query;
                            var currentIsHomeQuery = query.IsHomeQuery;
                            var shouldClearExistingResults = ShouldClearExistingResultsForQuery(query, currentIsHomeQuery);
                            _lastQuery = item.Query;
                            _previousIsHomeQuery = currentIsHomeQuery;

                            // If the queue already has the item, we need to pass the shouldClearExistingResults flag
                            if (queue.TryGetValue(item.ID, out var existingItem))
                            {
                                item.ShouldClearExistingResults = shouldClearExistingResults || existingItem.ShouldClearExistingResults;
                            }
                            else
                            {
                                item.ShouldClearExistingResults = shouldClearExistingResults;
                            }

                            queue[item.ID] = item;
                        }
                    }

                    UpdateResultView(queue.Values);
                    queue.Clear();
                }

                if (!_disposed)
                    _logger.LogError($"Unexpected ResultViewUpdate ends");
            }

            void continueAction(Task t)
            {
#if DEBUG
                throw t.Exception;
#else
                _logger.LogError(t.Exception, $"Error happen in task dealing with viewupdate for results");
                _resultsViewUpdateTask =
                    Task.Run(UpdateActionAsync).ContinueWith(continueAction, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
#endif
            }
        }

        [RelayCommand]
        private async Task ReloadPluginDataAsync()
        {
            Hide();

            await _pluginManager.ReloadDataAsync().ConfigureAwait(false);
            IPublicAPI.Instance.ShowMsg("Success", "Completed successfully");
        }

        [RelayCommand]
        public void ReQuery()
        {
            if (QueryResultsSelected())
            {
                // When we are re-querying, we should not delay the query
                _ = QueryResultsAsync(isReQuery: true);
            }
        }

        public void ReQuery(bool reselect)
        {
            BackToQueryResults();
            // When we are re-querying, we should not delay the query
            _ = QueryResultsAsync(isReQuery: true, reSelect: reselect);
        }

        [RelayCommand]
        private void LoadContextMenu()
        {
            if (QueryResultsSelected())
            {
                // When switch to ContextMenu from QueryResults, but no item being chosen, should do nothing
                // i.e. Shift+Enter/Ctrl+O right after Alt + Space should do nothing
                if (SelectedResults.SelectedItem != null)
                    SelectedResults = _contextMenu;
            }
            else
            {
                SelectedResults = _results;
            }
        }

        [RelayCommand]
        private void Backspace(object index)
        {
            var query = QueryBuilder.Build(QueryText, isRequery: false, _pluginManager.GetNonGlobalPlugins());
            string actionKeyword = query.ActionKeyword.Length == 0
                ? string.Empty
                : query.ActionKeyword + PluginSDK.Query.TermSeparator;

            string search = query.Search;
            if (search.EndsWith('\\') || search.EndsWith('/'))
                search = search[..^1];

            int lastSeparatorIndex = Math.Max(search.LastIndexOf('\\'), search.LastIndexOf('/'));
            search = lastSeparatorIndex >= 0 ? search[..(lastSeparatorIndex + 1)] : string.Empty;

            ChangeQueryText($"{actionKeyword}{search}");
        }

        [RelayCommand]
        private void AutocompleteQuery()
        {
            var result = SelectedResults.SelectedItem?.Result;
            if (result != null && QueryResultsSelected()) // SelectedItem returns null if selection is empty.
            {
                var autoCompleteText = result.Title;

                if (!string.IsNullOrEmpty(result.AutoCompleteText))
                {
                    autoCompleteText = result.AutoCompleteText;
                }

                ChangeQueryText(autoCompleteText);
            }
        }

        [RelayCommand]
        private async Task OpenResultAsync()
        {
            var selectedItem = SelectedResults.SelectedItem;
            if (selectedItem is null)
                return;

            var result = selectedItem.Result;
            var positionFunc = selectedItem.GetScreenCenterPoint
                ?? throw new NullReferenceException(nameof(selectedItem.GetScreenCenterPoint));
            var position = positionFunc();

            var hideWindow = await result.ExecuteAsync(new ActionContext
            {
                // not null means pressing modifier key + number, should ignore the modifier key
                PressedKeys = _hotkeyManager.GetPressedKeys(),
                ResultPosition = position ?? throw new Exception("Failed to get result position")
            }).ConfigureAwait(false);

            if (hideWindow)
            {
                Hide();
            }

            // Record user selected result for result ranking
            _userSelectedRecord.Add(result);
        }

        private static List<Result> DeepCloneResults(IReadOnlyList<Result> results, CancellationToken token = default)
        {
            var resultsCopy = new List<Result>();
            foreach (var result in results.ToList())
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                resultsCopy.Add(result with { });
            }
            return resultsCopy;
        }

        #region BasicCommands

        [RelayCommand]
        private void OpenSetting()
        {
            IPublicAPI.Instance.OpenSettingDialog();
        }

        [RelayCommand]
        private void SelectFirstResult()
        {
            SelectedResults.SelectFirstResult();
        }

        [RelayCommand]
        private void SelectLastResult()
        {
            SelectedResults.SelectLastResult();
        }

        [RelayCommand]
        private void SelectPrevPage()
        {
            SelectedResults.SelectPrevPage();
        }

        [RelayCommand]
        private void SelectNextPage()
        {
            SelectedResults.SelectNextPage();
        }

        [RelayCommand]
        private void SelectPrevItem()
        {
            if (QueryResultsSelected() // Results selected
                && string.IsNullOrEmpty(QueryText) // No input
                && _results.Visibility != Visibility.Visible) // No items in result list, e.g. when home page is off and no query text is entered, therefore the view is collapsed.
            {

            }
            else
            {
                SelectedResults.SelectPrevResult();
            }
        }

        [RelayCommand]
        private void SelectNextItem()
        {
            SelectedResults.SelectNextResult();
        }

        [RelayCommand]
        private void Esc()
        {
            if (!QueryResultsSelected())
            {
                SelectedResults = _results;
            }
            else
            {
                Hide();
            }
        }

        public void BackToQueryResults()
        {
            if (!QueryResultsSelected())
            {
                SelectedResults = _results;
            }
        }

        [RelayCommand]
        public void CopyAlternative()
        {
            var result = _results.SelectedItem?.Result?.CopyText;

            if (result != null)
            {
                IPublicAPI.Instance.CopyToClipboard(result, directCopy: false);
            }
        }

        #endregion

        #region ViewModel Properties
        public ISettingsAPI Settings { get; }

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
            if (MainWindowWidth - 100 < 400 || MainWindowWidth == 400)
            {
                MainWindowWidth = 400;
            }
            else
            {
                MainWindowWidth -= 100;
                MainWindowLeft += 50;
            }
        }

        [RelayCommand]
        private void IncreaseMaxResult()
        {
            if (Settings.MaxResultsToShow == 17)
                return;

            Settings.MaxResultsToShow += 1;
        }

        [RelayCommand]
        private void DecreaseMaxResult()
        {
            if (Settings.MaxResultsToShow == 2)
                return;

            Settings.MaxResultsToShow -= 1;
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
            get => field;
            private set
            {
                var isReturningFromContextMenu = ContextMenuSelected();
                field = value;
                OnPropertyChanged();

                if (QueryResultsSelected())
                {
                    // QueryText setter (used in ChangeQueryText) runs the query again, resetting the selected
                    // result from the one that was selected before going into the context menu to the first result.
                    // The code below correctly restores QueryText and puts the text caret at the end without
                    // running the query again when returning from the context menu.
                    if (isReturningFromContextMenu)
                    {
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
        public partial bool MainWindowVisibilityStatus { get; set; } = true;

        public double MainWindowWidth
        {
            get => Settings.WindowWidth;
            set
            {
                if (!MainWindowVisibilityStatus) return;
                Settings.WindowWidth = value;
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

        // This is not a reliable indicator of whether external preview is visible due to the
        // ability of manually closing/exiting the external preview program which, does not inform flow that
        // preview is no longer available.
        [ObservableProperty]
        public partial bool ExternalPreviewVisible { get; private set; }

        private async Task ShowPreviewAsync()
        {
            var useExternalPreview = _pluginManager.UseExternalPreview();

            switch (useExternalPreview)
            {
                case true
                    when CanExternalPreviewSelectedResult(out var path):
                    // Internal preview may still be on when user switches to external
                    if (InternalPreviewVisible)
                        HideInternalPreview();

                    _ = OpenExternalPreviewAsync(path);
                    break;

                case true
                    when !CanExternalPreviewSelectedResult(out var _):
                    if (ExternalPreviewVisible)
                        await CloseExternalPreviewAsync();

                    ShowInternalPreview();
                    break;

                case false:
                    ShowInternalPreview();
                    break;
            }
        }

        private void HidePreview()
        {
            if (_pluginManager.UseExternalPreview())
                _ = CloseExternalPreviewAsync();

            if (InternalPreviewVisible)
                HideInternalPreview();
        }

        [RelayCommand]
        private void TogglePreview()
        {
            if (InternalPreviewVisible || ExternalPreviewVisible)
            {
                HidePreview();
            }
            else
            {
                _ = ShowPreviewAsync();
            }
        }

        private async Task OpenExternalPreviewAsync(string path, bool sendFailToast = true)
        {
            await _pluginManager.OpenExternalPreviewAsync(path, sendFailToast).ConfigureAwait(false);
            ExternalPreviewVisible = true;
        }

        private async Task CloseExternalPreviewAsync()
        {
            await _pluginManager.CloseExternalPreviewAsync().ConfigureAwait(false);
            ExternalPreviewVisible = false;
        }

        private async Task SwitchExternalPreviewAsync(string path, bool sendFailToast = true)
        {
            await _pluginManager.SwitchExternalPreviewAsync(path, sendFailToast).ConfigureAwait(false);
        }

        private void ShowInternalPreview()
        {
            ResultAreaColumn = RESULTAREA_COLUMN_PREVIEWSHOWN;
            PreviewSelectedItem?.LoadPreviewImage();
        }

        private void HideInternalPreview()
        {
            ResultAreaColumn = RESULTAREA_COLUMN_PREVIEWHIDDEN;
        }

        public void ResetPreview()
        {
            switch (Settings.AlwaysPreview)
            {
                case true
                    when _pluginManager.AllowAlwaysPreview() && CanExternalPreviewSelectedResult(out var path):
                    _ = OpenExternalPreviewAsync(path);
                    break;
                case true:
                    ShowInternalPreview();
                    break;
                case false:
                    HidePreview();
                    break;
            }
        }

        private async Task UpdatePreviewAsync()
        {
            switch (_pluginManager.UseExternalPreview())
            {
                case true
                    when CanExternalPreviewSelectedResult(out var path):
                    if (ExternalPreviewVisible)
                    {
                        _ = SwitchExternalPreviewAsync(path, false);
                    }
                    else if (InternalPreviewVisible)
                    {
                        HideInternalPreview();
                        _ = OpenExternalPreviewAsync(path);
                    }
                    break;
                case true
                    when !CanExternalPreviewSelectedResult(out var _):
                    if (ExternalPreviewVisible)
                    {
                        await CloseExternalPreviewAsync();
                        ShowInternalPreview();
                    }
                    break;
                case false
                    when InternalPreviewVisible:
                    PreviewSelectedItem?.LoadPreviewImage();
                    break;
            }
        }

        private bool CanExternalPreviewSelectedResult([NotNullWhen(true)] out string? path)
        {
            path = QueryResultsPreviewed() ? _results.SelectedItem?.Result.Preview.FilePath : string.Empty;
            return !string.IsNullOrEmpty(path);
        }

        private bool QueryResultsPreviewed()
        {
            var previewed = PreviewSelectedItem == _results.SelectedItem;
            return previewed;
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

            if (QueryResultsSelected())
            {
                _ = QueryResultsAsync(isReQuery);
            }
            else if (ContextMenuSelected())
            {
                QueryContextMenu();
            }
        }

        private async Task QueryAsync(bool isReQuery = false)
        {
            if (QueryResultsSelected())
            {
                await QueryResultsAsync(isReQuery);
            }
            else if (ContextMenuSelected())
            {
                QueryContextMenu();
            }
        }

        private void QueryContextMenu()
        {
            const string id = "Context Menu ID";
            var query = QueryText.ToLower().Trim();
            _contextMenu.Clear();

            var selected = _results.SelectedItem?.Result;

            if (selected != null) // SelectedItem returns null if selection is empty.
            {
                List<Result> results;
                if (selected.PluginID == null) // SelectedItem from history in home page.
                {
                    results =
                    [
                        ContextMenuTopMost(selected)
                    ];
                }
                else
                {
                    results = _pluginManager.GetContextMenusForPlugin(selected) ?? [];
                    results.Add(ContextMenuTopMost(selected));
                }

                if (!string.IsNullOrEmpty(query))
                {
                    var filtered = results.Select(x => x with { }).Where
                    (
                        r =>
                        {
                            var match = IPublicAPI.Instance.FuzzySearch(query, r.Title);
                            if (!match.IsSearchPrecisionScoreMet)
                            {
                                match = IPublicAPI.Instance.FuzzySearch(query, r.SubTitle);
                            }

                            if (!match.IsSearchPrecisionScoreMet) return false;

                            r.Score = match.Score;
                            return true;
                        }).ToList();
                    _contextMenu.AddResults(filtered, id);
                }
                else
                {
                    _contextMenu.AddResults(results, id);
                }
            }
        }

        private async Task QueryResultsAsync(bool isReQuery = false, bool reSelect = true)
        {
            if (_updateSource is not null)
                await _updateSource.CancelAsync();

            _logger.LogDebug($"Start query with text: <{QueryText}>");

            var query = await ConstructQueryAsync(QueryText, isReQuery, Settings.CustomShortcuts, Settings.BuiltinShortcuts);

            if (query == null) // shortcut expanded
            {
                ClearResults();
                return;
            }

            _logger.LogDebug($"Start query with ActionKeyword <{query.ActionKeyword}> and TrimmedQuery <{query.TrimmedQuery}>");

            var currentIsHomeQuery = query.IsHomeQuery;

            _updateSource?.Dispose();

            var currentUpdateSource = new CancellationTokenSource();
            _updateSource = currentUpdateSource;
            var currentCancellationToken = _updateSource.Token;

            // Switch to ThreadPool thread
            await TaskScheduler.Default;

            if (currentCancellationToken.IsCancellationRequested) return;

            ICollection<PluginMetadata> plugins = Array.Empty<PluginMetadata>();
            if (currentIsHomeQuery)
            {
                if (Settings.ShowHomePage)
                {
                    plugins = _pluginManager.ValidPluginsForHomeQuery();
                }

                PluginIconPath = null;
                PluginIconSource = null;
            }
            else
            {
                plugins = _pluginManager.ValidPluginsForQuery(query);

                if (plugins.Count == 1)
                {
                    PluginIconPath = plugins.Single().IcoPath;
                    PluginIconSource = await IPublicAPI.Instance.LoadImageAsync(PluginIconPath);
                }
                else
                {
                    PluginIconPath = null;
                    PluginIconSource = null;
                }
            }

            _logger.LogDebug($"Valid <{plugins.Count}> plugins: {string.Join(" ", plugins.Select(x => $"<{x.Name}>"))}");

            // Do not wait for performance improvement
            /*if (string.IsNullOrEmpty(query.ActionKeyword))
            {
                // Wait 15 millisecond for query change in global query
                // if query changes, return so that it won't be calculated
                await Task.Delay(15, currentCancellationToken);
                if (currentCancellationToken.IsCancellationRequested) return;
            }*/

            // plugins are ICollection, meaning LINQ will get the Count and preallocate Array

            Task[] tasks;
            if (currentIsHomeQuery)
            {
                if (ShouldClearExistingResultsForNonQuery(plugins))
                {
                    // there are no update tasks and so we can directly return
                    ClearResults();
                    return;
                }

                tasks = [.. plugins.Select(plugin => plugin.HomeDisabled switch
                {
                    false => QueryTaskAsync(plugin, currentCancellationToken),
                    true => Task.CompletedTask
                })];
            }
            else
            {
                tasks = [.. plugins.Select(plugin => plugin.Disabled switch
                {
                    false => QueryTaskAsync(plugin, currentCancellationToken),
                    true => Task.CompletedTask
                })];
            }

            try
            {
                // Check the code, WhenAll will translate all type of IEnumerable or Collection to Array, so make an array at first
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // nothing to do here
            }

            if (currentCancellationToken.IsCancellationRequested) return;

            // Local function
            void ClearResults()
            {
                _logger.LogDebug($"Clear query results");

                // Hide and clear results again because running query may show and add some results
                _results.Visibility = Visibility.Collapsed;
                _results.Clear();

                // Reset plugin icon
                PluginIconPath = null;
                PluginIconSource = null;
            }

            // Local function
            async Task QueryTaskAsync(PluginMetadata plugin, CancellationToken token)
            {
                _logger.LogDebug($"Wait for querying plugin <{plugin.Name}>");

                // Since it is wrapped within a ThreadPool Thread, the synchronous context is null
                // Task.Yield will force it to run in ThreadPool
                await Task.Yield();

                var results = currentIsHomeQuery ?
                    await _pluginManager.QueryHomeForPluginAsync(plugin, query, token) :
                    await _pluginManager.QueryForPluginAsync(plugin, query, token);

                if (token.IsCancellationRequested) return;

                IReadOnlyList<Result> resultsCopy;
                if (results == null)
                {
                    resultsCopy = _emptyResult;
                }
                else
                {
                    // make a copy of results to avoid possible issue that FL changes some properties of the records, like score, etc.
                    resultsCopy = DeepCloneResults(results, token);
                }

                if (token.IsCancellationRequested) return;

                _logger.LogDebug($"Update results for plugin <{plugin.Name}>");

                if (!_resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(resultsCopy, plugin, query,
                    token, reSelect)))
                {
                    _logger.LogError($"Unable to add item to Result Update Queue");
                }
            }
        }

        private async Task<Query?> ConstructQueryAsync(
            string queryText, bool isRequery,
            IEnumerable<CustomShortcutModel> customShortcuts,
            IEnumerable<BaseBuiltinShortcutModel> builtInShortcuts)
        {
            if (string.IsNullOrWhiteSpace(queryText))
            {
                return QueryBuilder.Build(string.Empty, isRequery, _pluginManager.GetNonGlobalPlugins());
            }

            var queryBuilder = new StringBuilder(queryText);
            var queryBuilderTmp = new StringBuilder(queryText);

            // Sorting order is important here, the reason is for matching longest shortcut by default
            foreach (var shortcut in customShortcuts.OrderByDescending(x => x.Key.Length))
            {
                if (queryBuilder.Equals(shortcut.Key))
                {
                    queryBuilder.Replace(shortcut.Key, shortcut.Expand());
                }

                queryBuilder.Replace('@' + shortcut.Key, shortcut.Expand());
            }

            // Apply builtin shortcuts
            await BuildQueryAsync(builtInShortcuts, queryBuilder, queryBuilderTmp);

            return QueryBuilder.Build(queryBuilder.ToString(), isRequery, _pluginManager.GetNonGlobalPlugins());
        }

        private async Task BuildQueryAsync(IEnumerable<BaseBuiltinShortcutModel> builtInShortcuts,
            StringBuilder queryBuilder, StringBuilder queryBuilderTmp)
        {
            var customExpanded = queryBuilder.ToString();

            var queryChanged = false;

            foreach (var shortcut in builtInShortcuts)
            {
                try
                {
                    if (customExpanded.Contains(shortcut.Key))
                    {
                        string expansion;
                        if (shortcut is BuiltinShortcutModel syncShortcut)
                        {
                            expansion = syncShortcut.Expand();
                        }
                        else if (shortcut is AsyncBuiltinShortcutModel asyncShortcut)
                        {
                            expansion = await asyncShortcut.ExpandAsync();
                        }
                        else
                        {
                            continue;
                        }
                        queryBuilder.Replace(shortcut.Key, expansion);
                        queryBuilderTmp.Replace(shortcut.Key, expansion);
                        queryChanged = true;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Error when expanding shortcut {shortcut.Key}");
                }
            }

            // Show expanded builtin shortcuts
            if (queryChanged)
            {
                // Use private field to avoid infinite recursion
                _queryText = queryBuilderTmp.ToString();
                // When executing OnPropertyChanged, QueryTextBox_TextChanged1 and Query will be called
                // So we need to ignore it so that we will not call Query again
                _ignoredQueryText = _queryText;
                OnPropertyChanged(nameof(QueryText));
            }
        }

        /// <summary>
        /// Determines whether the existing search results should be cleared based on the current query and the previous query type.
        /// This is used to indicate to QueryTaskAsync whether to clear results. If QueryTaskAsync is not called then use ShouldClearExistingResultsForNonQuery instead.
        /// This method needed because of the design that treats plugins with action keywords and global action keywords separately. Results are gathered
        /// either from plugins with matching action keywords or global action keyword, but not both. So when the current results are from plugins
        /// with a matching action keyword and a new result set comes from a new query with the global action keyword, the existing results need to be cleared,
        /// and vice versa. The same applies to home page query results.
        /// 
        /// There is no need to clear results from global action keyword if a new set of results comes along that is also from global action keywords.
        /// This is because the removal of obsolete results is handled in ResultsViewModel.NewResults(ICollection<ResultsForUpdate>).
        /// </summary>
        /// <param name="query">The current query.</param>
        /// <param name="currentIsHomeQuery">A flag indicating if the current query is a home query.</param>
        /// <returns>True if the existing results should be cleared, false otherwise.</returns>
        private bool ShouldClearExistingResultsForQuery(Query query, bool currentIsHomeQuery)
        {
            // If previous or current results are from home query, we need to clear them
            if (_previousIsHomeQuery || currentIsHomeQuery)
            {
                _logger.LogDebug($"Existing results should be cleared for query");
                return true;
            }

            // If the last and current query are not home query type, we need to check the action keyword
            if (_lastQuery?.ActionKeyword != query?.ActionKeyword)
            {
                _logger.LogDebug($"Existing results should be cleared for query");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether existing results should be cleared for non-query calls.
        /// A non-query call is where QueryTaskAsync is not called.
        /// QueryTaskAsync handles result updating (clearing if required) so directly calling
        /// Results.Clear() is not required. However when both are not called, we need to directly clear results and this
        /// method determines on the condition when clear results should happen.
        /// </summary>
        /// <param name="plugins">The collection of plugins to check.</param>
        /// <returns>True if existing results should be cleared, false otherwise.</returns>
        private bool ShouldClearExistingResultsForNonQuery(ICollection<PluginMetadata> plugins)
        {
            if (plugins.Count == 0 || plugins.All(x => x.HomeDisabled == true))
            {
                _logger.LogDebug($"Existing results should be cleared for non-query");
                return true;
            }

            return false;
        }

        private Result ContextMenuTopMost(Result result)
        {
            Result menu;
            if (_topMostRecord.IsTopMost(result))
            {
                menu = new Result
                {
                    Title = "Cancel topmost in this query",
                    Action = _ =>
                    {
                        _topMostRecord.Remove(result);
                        IPublicAPI.Instance.ShowMsg("Success");
                        IPublicAPI.Instance.ReQuery();
                        return false;
                    },
                    Glyph = new GlyphInfo(Glyph: "\uE74B"),
                    OriginQuery = result.OriginQuery
                };
            }
            else
            {
                menu = new Result
                {
                    Title = "Set as topmost in this query",
                    Action = _ =>
                    {
                        _topMostRecord.AddOrUpdate(result);
                        IPublicAPI.Instance.ShowMsg("Success");
                        IPublicAPI.Instance.ReQuery();
                        return false;
                    },
                    Glyph = new GlyphInfo(Glyph: "\uE74A"),
                    OriginQuery = result.OriginQuery
                };
            }

            return menu;
        }

        internal bool QueryResultsSelected()
        {
            var selected = SelectedResults == _results;
            return selected;
        }

        private bool ContextMenuSelected()
        {
            var selected = SelectedResults == _contextMenu;
            return selected;
        }

        internal bool ResultsSelected(ResultsViewModel results)
        {
            var selected = SelectedResults == results;
            return selected;
        }

        #endregion

        #region Public Methods

#pragma warning disable VSTHRD100 // Avoid async void methods

        public void Show()
        {
            // When application is exiting, we should not show the main window
            if (App.App.LoadingOrExiting) return;

            // When application is exiting, the Application.Current will be null
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // When application is exiting, the Application.Current will be null
                if (Application.Current?.MainWindow is MainWindow mainWindow)
                {
                    // 📌 Remove DWM Cloak (Make the window visible normally)
                    WindowHelper.DWMSetCloakForWindow(mainWindow, false);
                }
            }, DispatcherPriority.Render);

            // Update WPF properties
            MainWindowVisibility = Visibility.Visible;
            MainWindowVisibilityStatus = true;

            // Show the taskbar if the setting is enabled
            if (Settings.ShowTaskbarWhenOpened && !_taskbarShownByFlow)
            {
                TaskbarHelper.ShowTaskbar();
                _taskbarShownByFlow = true;
            }
        }

        public async void Hide()
        {
            if (ExternalPreviewVisible)
            {
                await CloseExternalPreviewAsync();
            }

            BackToQueryResults();

            switch (Settings.LastQueryMode)
            {
                case LastQueryMode.Empty:
                    await ChangeQueryTextAsync(string.Empty);
                    break;
                case LastQueryMode.Preserved:
                case LastQueryMode.Selected:
                    LastQuerySelected = Settings.LastQueryMode == LastQueryMode.Preserved;
                    break;
                case LastQueryMode.ActionKeywordPreserved:
                case LastQueryMode.ActionKeywordSelected:
                    var newQuery = _lastQuery?.ActionKeyword;

                    if (!string.IsNullOrEmpty(newQuery))
                        newQuery += " ";
                    await ChangeQueryTextAsync(newQuery);

                    if (Settings.LastQueryMode == LastQueryMode.ActionKeywordSelected)
                        LastQuerySelected = false;
                    break;
            }

            // When application is exiting, the Application.Current will be null
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // When application is exiting, the Application.Current will be null
                if (Application.Current?.MainWindow is MainWindow mainWindow)
                {
                    // 📌 Apply DWM Cloak (Completely hide the window)
                    WindowHelper.DWMSetCloakForWindow(mainWindow, true);
                }
            }, DispatcherPriority.Render);

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
        /// Save user selected records and top most records
        /// </summary>
        public bool TrySave()
        {
            return _userSelectedRecordStorage.TrySave() && _topMostRecord.TrySave();
        }

        /// <summary>
        /// To avoid deadlock, this method should not be called from main thread
        /// </summary>
        public void UpdateResultView(ICollection<ResultsForUpdate> resultsForUpdates)
        {
            if (resultsForUpdates.Count == 0)
                return;

            CancellationToken token;

            try
            {
                // Don't know why sometimes even resultsForUpdates is empty, the method won't return;
                token = resultsForUpdates.Select(r => r.Token).Distinct().SingleOrDefault();
            }
#if DEBUG
            catch
            {
                throw new ArgumentException("Unacceptable token");
            }
#else
            catch
            {
                token = default;
            }
#endif

            foreach (var metaResults in resultsForUpdates)
            {
                foreach (var result in metaResults.Results)
                {
                    var deviationIndex = _topMostRecord.GetTopMostIndex(result);
                    if (deviationIndex != -1)
                    {
                        // Adjust the score based on the result's position in the top-most list.
                        // A lower deviationIndex (closer to the top) results in a higher score.
                        result.Score = Result.MaxScore - deviationIndex;
                    }
                    else
                    {
                        var priorityScore = metaResults.Metadata.Priority * 150;
                        if (result.AddSelectedCount)
                        {
                            if ((long)result.Score + _userSelectedRecord.GetSelectedCount(result) + priorityScore > Result.MaxScore)
                            {
                                result.Score = Result.MaxScore;
                            }
                            else
                            {
                                result.Score += _userSelectedRecord.GetSelectedCount(result) + priorityScore;
                            }
                        }
                        else
                        {
                            if ((long)result.Score + priorityScore > Result.MaxScore)
                            {
                                result.Score = Result.MaxScore;
                            }
                            else
                            {
                                result.Score += priorityScore;
                            }
                        }
                    }
                }
            }

            // it should be the same for all results
            var reSelect = resultsForUpdates.First().ReSelectFirstResult;

            _results.AddResults(resultsForUpdates, token, reSelect);
        }

        #endregion

        #region IDisposable

        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _updateSource?.Dispose();
                    _resultsUpdateChannelWriter?.Complete();
                    if (_resultsViewUpdateTask?.IsCompleted == true)
                    {
                        _resultsViewUpdateTask.Dispose();
                    }
                    _disposed = true;
                }
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
