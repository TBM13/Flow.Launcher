using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.ViewModel;

public partial class SelectFileManagerViewModel : BaseModel
{
    private readonly Settings _settings;

    private int selectedCustomExplorerIndex;

    public int SelectedCustomExplorerIndex
    {
        get => selectedCustomExplorerIndex;
        set
        {
            // When one custom file manager is selected and removed, the index will become -1, so we need to ignore this change
            if (value < 0) return;
            if (selectedCustomExplorerIndex != value)
            {
                selectedCustomExplorerIndex = value;
                OnPropertyChanged(nameof(CustomExplorer));
            }
        }
    }

    public ObservableCollection<CustomExplorerViewModel> CustomExplorers { get; }

    public CustomExplorerViewModel CustomExplorer => CustomExplorers[SelectedCustomExplorerIndex];

    public SelectFileManagerViewModel(Settings settings)
    {
        _settings = settings;
        CustomExplorers = new ObservableCollection<CustomExplorerViewModel>(_settings.CustomExplorerList.Select(x => x.Copy()));
        SelectedCustomExplorerIndex = _settings.CustomExplorerIndex;
    }

    public bool SaveSettings()
    {
        _settings.CustomExplorerList = CustomExplorers.ToList();
        _settings.CustomExplorerIndex = SelectedCustomExplorerIndex;
        return true;
    }

    [RelayCommand]
    private void Add()
    {
        CustomExplorers.Add(new()
        {
            Name = Localize.defaultBrowser_new_profile()
        });
        SelectedCustomExplorerIndex = CustomExplorers.Count - 1;
    }

    [RelayCommand]
    private void Delete()
    {
        var currentIndex = SelectedCustomExplorerIndex;
        if (currentIndex >= 0 && currentIndex < CustomExplorers.Count)
        {
            CustomExplorers.RemoveAt(currentIndex);
            SelectedCustomExplorerIndex = currentIndex > 0 ? currentIndex - 1 : 0;
        }
    }
}
