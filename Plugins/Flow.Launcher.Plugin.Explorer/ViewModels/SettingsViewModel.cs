using System.Globalization;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Infrastructure.Plugins;
using Flow.Launcher.Plugin.Explorer.Search;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.Explorer.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public Settings Settings { get; }

    internal PluginInitContext Context { get; }

    public SettingsViewModel(PluginInitContext context, Settings settings)
    {
        Settings = settings;
        Context = context;

        Settings.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(Settings.ShowCreatedDateInPreviewPanel):
                case nameof(Settings.ShowModifiedDateInPreviewPanel):
                    OnPropertyChanged(nameof(ShowPreviewPanelDateTimeChoices));
                    OnPropertyChanged(nameof(PreviewPanelDateTimeChoicesVisibility));
                    break;

                case nameof(Settings.PreviewPanelDateFormat):
                    OnPropertyChanged(nameof(PreviewPanelDateFormatDemo));
                    break;
                case nameof(Settings.PreviewPanelTimeFormat):
                    OnPropertyChanged(nameof(PreviewPanelTimeFormatDemo));
                    break;

                default:
                    break;
            }
        };
    }

    public void Save()
    {
        Context.API.SaveSettingJsonStorage<Settings>();
    }

    #region Preview Panel
    public string PreviewPanelDateFormatDemo
        => DateTime.Now.ToString(Settings.PreviewPanelDateFormat, CultureInfo.CurrentCulture);
    public string PreviewPanelTimeFormatDemo
        => DateTime.Now.ToString(Settings.PreviewPanelTimeFormat, CultureInfo.CurrentCulture);

    public bool ShowPreviewPanelDateTimeChoices
        => Settings.ShowCreatedDateInPreviewPanel || Settings.ShowModifiedDateInPreviewPanel;

    public Visibility PreviewPanelDateTimeChoicesVisibility
        => Settings.ShowCreatedDateInPreviewPanel || Settings.ShowModifiedDateInPreviewPanel ? Visibility.Visible : Visibility.Collapsed;

    public List<string> TimeFormatList { get; } =
    [
        "h:mm",
        "hh:mm",
        "H:mm",
        "HH:mm",
        "tt h:mm",
        "tt hh:mm",
        "h:mm tt",
        "hh:mm tt",
        "hh:mm:ss tt",
        "HH:mm:ss"
    ];

    public List<string> DateFormatList { get; } =
    [
        "dd/MM/yyyy",
        "dd/MM/yyyy ddd",
        "dd/MM/yyyy, dddd",
        "dd-MM-yyyy",
        "dd-MM-yyyy ddd",
        "dd-MM-yyyy, dddd",
        "dd.MM.yyyy",
        "dd.MM.yyyy ddd",
        "dd.MM.yyyy, dddd",
        "MM/dd/yyyy",
        "MM/dd/yyyy ddd",
        "MM/dd/yyyy, dddd",
        "yyyy-MM-dd",
        "yyyy-MM-dd ddd",
        "yyyy-MM-dd, dddd",
        "dd/MMM/yyyy",
        "dd/MMM/yyyy ddd",
        "dd/MMM/yyyy, dddd",
        "dd-MMM-yyyy",
        "dd-MMM-yyyy ddd",
        "dd-MMM-yyyy, dddd",
        "dd.MMM.yyyy",
        "dd.MMM.yyyy ddd",
        "dd.MMM.yyyy, dddd",
        "MMM/dd/yyyy",
        "MMM/dd/yyyy ddd",
        "MMM/dd/yyyy, dddd",
        "yyyy-MMM-dd",
        "yyyy-MMM-dd ddd",
        "yyyy-MMM-dd, dddd",
    ];
    #endregion

    private static string? PromptUserSelectPath(ResultType type, string? initialDirectory = null)
    {
        string? path = null;

        if (type is ResultType.Folder)
        {
            var folderBrowserDialog = new OpenFolderDialog();

            if (initialDirectory is not null)
                folderBrowserDialog.InitialDirectory = initialDirectory;

            if (folderBrowserDialog.ShowDialog() != true)
                return path;

            path = folderBrowserDialog.FolderName;
        }
        else if (type is ResultType.File)
        {
            var openFileDialog = new OpenFileDialog();
            if (initialDirectory is not null)
                openFileDialog.InitialDirectory = initialDirectory;

            if (openFileDialog.ShowDialog() != true)
                return path;

            path = openFileDialog.FileName;
        }
        return path;
    }

    [RelayCommand]
    private void OpenShellPath()
    {
        var path = PromptUserSelectPath(ResultType.File, Settings.ShellPath != null ? Path.GetDirectoryName(Settings.ShellPath) : null);
        if (path is null)
            return;

        Settings.ShellPath = path;
    }
}
