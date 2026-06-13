using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Results;
using Flow.Launcher.PluginSDK.Logging;

namespace Flow.Launcher.ViewModel
{
    public partial class ResultsViewModel : ObservableObject
    {
        private readonly Logger<ResultsViewModel> _logger;
        private readonly MainViewModel _mainVM;
        private readonly Settings _settings;
        private readonly PluginManager _pluginManager;
        private readonly object _collectionLock = new();

        public ResultCollection Results { get; }

        public ResultsViewModel(Logger<ResultsViewModel> logger,
            MainViewModel mainVM, Settings settings, PluginManager pluginManager)
        {
            _logger = logger;
            _mainVM = mainVM;
            _settings = settings;
            _pluginManager = pluginManager;

            Results = [];
            BindingOperations.EnableCollectionSynchronization(Results, _collectionLock);

            _settings.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(_settings.MaxResultsToShow):
                        OnPropertyChanged(nameof(MaxHeight));
                        break;
                }
            };
        }

        [ObservableProperty]
        public partial bool IsPreviewOn { get; set; }

        public double MaxHeight
        {
            get
            {
                var newResultsCount = _settings.MaxResultsToShow;
                if (IsPreviewOn)
                {
                    newResultsCount = (int)Math.Ceiling(380 / Const.ItemHeightSize);
                    if (newResultsCount < _settings.MaxResultsToShow)
                    {
                        newResultsCount = _settings.MaxResultsToShow;
                    }
                }
                return newResultsCount * Const.ItemHeightSize;
            }
        }

        [ObservableProperty]
        public partial int SelectedIndex { get; set; }

        [ObservableProperty]
        public partial ResultViewModel? SelectedItem { get; set; }
        [ObservableProperty]
        public partial Thickness Margin { get; set; }
        [ObservableProperty]
        public partial Visibility Visibility { get; set; } = Visibility.Collapsed;

        public required ICommand RightClickResultCommand { get; init; }
        public required ICommand LeftClickResultCommand { get; init; }

        private int NewIndex(int i)
        {
            var n = Results.Count;
            if (n > 0)
            {
                i = (n + i) % n;
                return i;
            }
            else
            {
                // SelectedIndex returns -1 if selection is empty.
                return -1;
            }
        }

        public void SelectNextResult()
        {
            SelectedIndex = NewIndex(SelectedIndex + 1);
        }

        public void SelectPrevResult()
        {
            SelectedIndex = NewIndex(SelectedIndex - 1);
        }

        public void SelectNextPage()
        {
            SelectedIndex = NewIndex(SelectedIndex + _settings.MaxResultsToShow);
        }

        public void SelectPrevPage()
        {
            SelectedIndex = NewIndex(SelectedIndex - _settings.MaxResultsToShow);
        }

        public void SelectFirstResult()
        {
            SelectedIndex = NewIndex(0);
        }

        public void SelectLastResult()
        {
            SelectedIndex = NewIndex(Results.Count - 1);
        }

        public void Clear()
        {
            lock (_collectionLock)
                Results.RemoveAll();
        }

        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void AddResults(List<Result> newRawResults, string resultId)
        {
            var newResults = NewResults(newRawResults, resultId);

            UpdateResults(newResults);
        }

        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void AddResults(ICollection<ResultsForUpdate> resultsForUpdates, CancellationToken token, bool reselect = true)
        {
            // Since NewResults may need to clear existing results, do not check token cancellation after this point
            var newResults = NewResults(resultsForUpdates);

            UpdateResults(newResults, reselect, token);
        }

        private void UpdateResults(List<ResultViewModel> newResults, bool reselect = true, CancellationToken token = default)
        {
            lock (_collectionLock)
            {
                // update UI in one run, so it can avoid UI flickering
                Results.Update(newResults, token);
                if (reselect && Results.Any())
                    SelectedItem = Results[0];
            }

            if (token.IsCancellationRequested)
                return;

            switch (Visibility)
            {
                case Visibility.Collapsed when Results.Count > 0:
                    if (_mainVM == null || // The results are for preview only in appearance page
                        _mainVM.ResultsSelected(this)) // The results are selected
                    {
                        SelectedIndex = 0;
                        Visibility = Visibility.Visible;
                    }
                    break;
                case Visibility.Visible when Results.Count == 0:
                    Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private List<ResultViewModel> NewResults(List<Result> newRawResults, string resultId)
        {
            if (newRawResults.Count == 0)
                return Results;

            var newResults = newRawResults.Select(r => new ResultViewModel(r, _settings));

            return [.. Results.Where(r => r.Result.PluginID != resultId)
                .Concat(newResults)
                .OrderByDescending(r => r.Result.Score)
            ];
        }

        private List<ResultViewModel> NewResults(ICollection<ResultsForUpdate> resultsForUpdates)
        {
            if (resultsForUpdates.Count == 0)
            {
                _logger.LogDebug($"No results for updates, returning existing results");
                return Results;
            }

            var newResults = resultsForUpdates.SelectMany(u => u.Results, (u, r) => new ResultViewModel(r, _settings));

            if (resultsForUpdates.Any(x => x.ShouldClearExistingResults))
            {
                _logger.LogDebug($"Existing results are cleared for query");
                return [.. newResults.OrderByDescending(rv => rv.Result.Score)];
            }

            _logger.LogDebug($"Keeping existing results for {resultsForUpdates.Count} queries");
            return [.. Results.Where(r => r?.Result != null && resultsForUpdates.All(u => u.ID != r.Result.PluginID))
                              .Concat(newResults)
                              .OrderByDescending(rv => rv.Result.Score)];
        }

        public class ResultCollection : List<ResultViewModel>, INotifyCollectionChanged
        {
            private long editTime = 0;

            public event NotifyCollectionChangedEventHandler? CollectionChanged;

            protected void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
            {
                CollectionChanged?.Invoke(this, e);
            }

            private void BulkAddAll(List<ResultViewModel> resultViews, CancellationToken token = default)
            {
                AddRange(resultViews);

                // can return because the list will be cleared next time updated, which include a reset event
                if (token.IsCancellationRequested)
                    return;

                // manually update event
                // wpf use DirectX / double buffered already, so just reset all won't cause ui flickering
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }

            private void AddAll(List<ResultViewModel> Items, CancellationToken token = default)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var item = Items[i];
                    if (token.IsCancellationRequested)
                        return;
                    Add(item);
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, i));
                }
            }

            public void RemoveAll(int Capacity = 512)
            {
                Clear();
                if (this.Capacity > 8000 && Capacity < this.Capacity)
                    this.Capacity = Capacity;

                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }

            /// <summary>
            /// Update the results collection with new results, try to keep identical results
            /// </summary>
            /// <param name="newItems"></param>
            public void Update(List<ResultViewModel> newItems, CancellationToken token = default)
            {
                // Since NewResults may need to clear existing results, so we cannot check token cancellation here
                if (Count == 0 && newItems.Count == 0)
                    return;

                if (editTime < 10 || newItems.Count < 30)
                {
                    if (Count != 0) RemoveAll(newItems.Count);

                    // After results are removed, we need to check the token cancellation
                    // so that we will not add new items from the cancelled queries
                    if (token.IsCancellationRequested) return;

                    AddAll(newItems, token);
                    editTime++;
                }
                else
                {
                    Clear();

                    // After results are removed, we need to check the token cancellation
                    // so that we will not add new items from the cancelled queries
                    if (token.IsCancellationRequested) return;

                    BulkAddAll(newItems, token);
                    if (Capacity > 8000 && newItems.Count < 3000)
                    {
                        Capacity = newItems.Count;
                    }
                    editTime++;
                }
            }
        }
    }
}
