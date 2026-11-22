using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.Helper;

namespace Flow.Launcher.ViewModel;

public partial class ResultsViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsAPI _settings;

    public BulkObservableCollection<ResultViewModel> Results { get; } = [];

    /// <summary>
    /// True while <see cref="Results"/> is being replaced or cleared so transient selection resets do not flicker the preview pane.
    /// </summary>
    public bool IsUpdating { get; private set; }

    public ResultsViewModel(ISettingsAPI settings)
    {
        _settings = settings;
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
        if (Results.Count == 0 && SelectedItem is null && Visibility == Visibility.Collapsed)
            return;

        IsUpdating = true;
        try
        {
            Results.Clear();
        }
        finally
        {
            IsUpdating = false;
        }

        SelectedIndex = -1;
        SelectedItem = null;
        Visibility = Visibility.Collapsed;
    }

    public void UpdateResults(IReadOnlyList<ResultViewModel> newResults, bool reselect = true)
    {
        if (newResults.Count == 0)
        {
            Clear();
            return;
        }

        ResultViewModel? previousSelection = !reselect ? SelectedItem : null;

        IsUpdating = true;
        try
        {
            Results.ReplaceAll(newResults);
        }
        finally
        {
            IsUpdating = false;
        }

        int indexToSelect = 0;
        if (previousSelection is not null)
        {
            int existingIndex = Results.IndexOf(previousSelection);
            if (existingIndex >= 0)
                indexToSelect = existingIndex;
        }

        ResultViewModel targetSelection = Results[indexToSelect];
        bool sameReference = ReferenceEquals(SelectedItem, targetSelection);

        SelectedIndex = indexToSelect;
        SelectedItem = targetSelection;
        if (sameReference)
            OnPropertyChanged(nameof(SelectedItem));

        Visibility = Visibility.Visible;
    }

    public void Dispose()
    {
        _settings.PropertyChanged -= OnSettingChanged;
    }
}
