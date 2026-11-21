using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Interop.Shell;
using Flow.Launcher.Plugin.Program.Programs;
using Flow.Launcher.PluginSDK.Plugins;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.Program.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly Settings _settings;
    private readonly ProgramIndex _index;
    private readonly PluginInitContext _context;

    private string _customSuffixesText;

    public Settings Settings => _settings;

    public bool IsIndexing => _index.IsIndexing;
    public bool CanReindex => !_index.IsIndexing;
    public bool ShowUwpOption => UwpIndexer.IsSupported;
    public bool CanUnhideAll => _settings.DisabledProgramSources.Count > 0;

    public string IndexStatusText => _index.IsIndexing
        ? "Indexing programs..."
        : $"{_index.IndexedProgramCount} programs indexed";

    public bool EnableStartMenuSource
    {
        get => _settings.EnableStartMenuSource;
        set
        {
            if (_settings.EnableStartMenuSource == value)
                return;

            _settings.EnableStartMenuSource = value;
            OnPropertyChanged();
            OnWin32SourcesChanged();
        }
    }

    public bool EnableUWP
    {
        get => _settings.EnableUWP;
        set
        {
            if (_settings.EnableUWP == value)
                return;

            _settings.EnableUWP = value;
            OnPropertyChanged();
            _context.API.SaveSettingJsonStorage<Settings>();
            _ = _index.ReindexAllAsync();
        }
    }

    public bool EnableRegistrySource
    {
        get => _settings.EnableRegistrySource;
        set
        {
            if (_settings.EnableRegistrySource == value)
                return;

            _settings.EnableRegistrySource = value;
            OnPropertyChanged();
            OnWin32SourcesChanged();
        }
    }

    public bool EnablePathSource
    {
        get => _settings.EnablePathSource;
        set
        {
            if (_settings.EnablePathSource == value)
                return;

            _settings.EnablePathSource = value;
            OnPropertyChanged();
            OnWin32SourcesChanged();
        }
    }

    public bool HideUninstallers
    {
        get => _settings.HideUninstallers;
        set
        {
            if (_settings.HideUninstallers == value)
                return;

            _settings.HideUninstallers = value;
            OnPropertyChanged();
            _context.API.SaveSettingJsonStorage<Settings>();
        }
    }

    public bool HideAppsPath
    {
        get => _settings.HideAppsPath;
        set
        {
            if (_settings.HideAppsPath == value)
                return;

            _settings.HideAppsPath = value;
            OnPropertyChanged();
            _context.API.SaveSettingJsonStorage<Settings>();
        }
    }

    public bool EnableDescription
    {
        get => _settings.EnableDescription;
        set
        {
            if (_settings.EnableDescription == value)
                return;

            _settings.EnableDescription = value;
            OnPropertyChanged();
            _context.API.SaveSettingJsonStorage<Settings>();
        }
    }

    public string CustomSuffixesText
    {
        get => _customSuffixesText;
        set => SetProperty(ref _customSuffixesText, value);
    }

    internal SettingsViewModel(Settings settings, ProgramIndex index, PluginInitContext context)
    {
        _settings = settings;
        _index = index;
        _context = context;
        _customSuffixesText = string.Join(Settings.SuffixSeparator, settings.CustomSuffixes);

        _index.IndexStateChanged += OnIndexStateChanged;
        _settings.DisabledProgramSources.CollectionChanged += OnDisabledProgramSourcesChanged;
    }

    [RelayCommand(CanExecute = nameof(CanReindex))]
    private async Task ReindexAsync()
    {
        await _index.ReindexAllAsync();
    }

    [RelayCommand]
    private void ApplyCustomSuffixes()
    {
        string[] parsed = Settings.ParseCustomSuffixes(_customSuffixesText);
        string normalizedText = string.Join(Settings.SuffixSeparator, parsed);

        if (!string.Equals(_customSuffixesText, normalizedText, StringComparison.Ordinal))
        {
            _customSuffixesText = normalizedText;
            OnPropertyChanged(nameof(CustomSuffixesText));
        }

        if (_settings.CustomSuffixes.SequenceEqual(parsed, StringComparer.OrdinalIgnoreCase))
            return;

        _settings.CustomSuffixes = parsed;
        OnWin32SourcesChanged();
    }

    [RelayCommand]
    private void AddDirectory()
    {
        OpenFolderDialog dialog = new()
        {
            Multiselect = true,
            Title = "Select program directories to index"
        };

        if (dialog.ShowDialog() == true)
        {
            AddDirectories(dialog.FolderNames);
        }
    }

    public bool AddDirectories(IEnumerable<string> paths)
    {
        bool added = false;

        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                continue;

            string normalizedId = ProgramSource.NormalizeId(path);
            if (_settings.ProgramSources.Any(
                x => string.Equals(x.UniqueIdentifier, normalizedId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _settings.ProgramSources.Add(new ProgramSource(path));
            added = true;
        }

        if (added)
            OnWin32SourcesChanged();

        return added;
    }

    [RelayCommand]
    private void RemoveDirectory(ProgramSource? source)
    {
        if (source is null)
            return;

        if (_settings.ProgramSources.Remove(source))
            OnWin32SourcesChanged();
    }

    [RelayCommand]
    private void ToggleDirectory(ProgramSource? source)
    {
        if (source is null)
            return;

        OnWin32SourcesChanged();
    }

    [RelayCommand]
    private void OpenDirectory(ProgramSource? source)
    {
        if (source is null || string.IsNullOrWhiteSpace(source.Location))
            return;

        try
        {
            FileExplorerHelper.OpenFolder(source.Location);
        }
        catch (Exception ex)
        {
            _context.API.ShowMsgError("Unable to open folder", ex.Message);
        }
    }

    [RelayCommand]
    private void UnhideProgram(ProgramSource? source)
    {
        if (source is not null)
            _index.UnhideProgram(source);
    }

    [RelayCommand(CanExecute = nameof(CanUnhideAll))]
    private void UnhideAllPrograms()
    {
        _index.UnhideAllPrograms();
    }

    private void OnWin32SourcesChanged()
    {
        _context.API.SaveSettingJsonStorage<Settings>();
        _index.RefreshFileWatchers();
        _ = _index.ReindexWin32Async();
    }

    private void OnDisabledProgramSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanUnhideAll));
        UnhideAllProgramsCommand.NotifyCanExecuteChanged();
    }

    private void OnIndexStateChanged()
    {
        void Notify()
        {
            OnPropertyChanged(nameof(IsIndexing));
            OnPropertyChanged(nameof(CanReindex));
            OnPropertyChanged(nameof(IndexStatusText));
            ReindexCommand.NotifyCanExecuteChanged();
        }

        Dispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
            dispatcher.BeginInvoke(Notify);
        else
            Notify();
    }
}
