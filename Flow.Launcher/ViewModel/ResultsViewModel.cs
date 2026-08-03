using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.Helper;
using Flow.Launcher.PluginSDK;

namespace Flow.Launcher.ViewModel;

public partial class ResultsViewModel : ObservableObject, IDisposable
{
    private readonly MainViewModel _mainVM;
    private readonly ISettingsAPI _settings;
    private readonly object _collectionLock = new();

    public BulkObservableCollection<ResultViewModel> Results { get; }

    public ResultsViewModel(MainViewModel mainVM, ISettingsAPI settings)
    {
        _mainVM = mainVM;
        _settings = settings;

        Results = [];
        BindingOperations.EnableCollectionSynchronization(Results, _collectionLock);

        _settings.PropertyChanged += OnSettingChanged;
    }

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(_settings.MaxResultsToShow):
                OnPropertyChanged(nameof(MaxHeight));
                break;
        }
    }

    public double MaxHeight => _settings.MaxResultsToShow * Const.ItemHeightSize;

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
        int n = Results.Count;
        if (n == 0)
            return -1;

        return ((i % n) + n) % n;
    }

    [RelayCommand]
    public void SelectNextResult() => SelectedIndex = NewIndex(SelectedIndex + 1);
    [RelayCommand]
    public void SelectPrevResult() => SelectedIndex = NewIndex(SelectedIndex - 1);
    [RelayCommand]
    public void SelectNextPage() => SelectedIndex = NewIndex(SelectedIndex + _settings.MaxResultsToShow);
    [RelayCommand]
    public void SelectPrevPage() => SelectedIndex = NewIndex(SelectedIndex - _settings.MaxResultsToShow);
    [RelayCommand]
    public void SelectFirstResult() => SelectedIndex = NewIndex(0);
    [RelayCommand]
    public void SelectLastResult() => SelectedIndex = NewIndex(Results.Count - 1);

    public void Clear()
    {
        lock (_collectionLock)
            Results.Clear();
    }

    /// <summary>
    /// To avoid deadlock, this method should not called from main thread
    /// </summary>
    public void AddResults(List<Result> newRawResults)
    {
        var newResults = NewResults(newRawResults);
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

    private void UpdateResults(IEnumerable<ResultViewModel> newResults, bool reselect = true, CancellationToken token = default)
    {
        lock (_collectionLock)
        {
            // update UI in one run, so it can avoid UI flickering
            Results.ReplaceAll(newResults);
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

    private IEnumerable<ResultViewModel> NewResults(List<Result> newRawResults)
    {
        if (newRawResults.Count == 0)
            return Results;

        var newResults = newRawResults.Select(r => new ResultViewModel(r, _settings));
        return Results.Concat(newResults).OrderByDescending(r => r.Result.Score);
    }

    private IEnumerable<ResultViewModel> NewResults(ICollection<ResultsForUpdate> resultsForUpdates)
    {
        if (resultsForUpdates.Count == 0)
            return Results;

        var newResults = resultsForUpdates.SelectMany(u => u.Results, (u, r) => new ResultViewModel(r, _settings));

        if (resultsForUpdates.Any(x => x.ShouldClearExistingResults))
            return newResults.OrderByDescending(rv => rv.Result.Score);

        return Results.Where(r => resultsForUpdates.All(u => u.ID != r.Result.PluginID))
                .Concat(newResults)
                .OrderByDescending(rv => rv.Result.Score);
    }

    public void Dispose()
    {
        _settings.PropertyChanged -= OnSettingChanged;
    }
}
