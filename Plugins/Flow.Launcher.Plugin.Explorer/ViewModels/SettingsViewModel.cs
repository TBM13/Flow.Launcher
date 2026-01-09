#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Plugins;
using Flow.Launcher.Plugin.Explorer.Search;

namespace Flow.Launcher.Plugin.Explorer.ViewModels
{
    public partial class SettingsViewModel(PluginInitContext context, Settings settings) : BaseModel
    {
        public Settings Settings { get; set; } = settings;

        internal PluginInitContext Context { get; set; } = context;

        public void Save()
        {
            Context.API.SaveSettingJsonStorage<Settings>();
        }

        #region Preview Panel

        public bool ShowFileSizeInPreviewPanel
        {
            get => Settings.ShowFileSizeInPreviewPanel;
            set
            {
                Settings.ShowFileSizeInPreviewPanel = value;
                OnPropertyChanged();
            }
        }

        public bool ShowCreatedDateInPreviewPanel
        {
            get => Settings.ShowCreatedDateInPreviewPanel;
            set
            {
                Settings.ShowCreatedDateInPreviewPanel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowPreviewPanelDateTimeChoices));
                OnPropertyChanged(nameof(PreviewPanelDateTimeChoicesVisibility));
            }
        }

        public bool ShowModifiedDateInPreviewPanel
        {
            get => Settings.ShowModifiedDateInPreviewPanel;
            set
            {
                Settings.ShowModifiedDateInPreviewPanel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowPreviewPanelDateTimeChoices));
                OnPropertyChanged(nameof(PreviewPanelDateTimeChoicesVisibility));
            }
        }

        public bool ShowFileAgeInPreviewPanel
        {
            get => Settings.ShowFileAgeInPreviewPanel;
            set
            {
                Settings.ShowFileAgeInPreviewPanel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowPreviewPanelDateTimeChoices));
                OnPropertyChanged(nameof(PreviewPanelDateTimeChoicesVisibility));
            }
        }

        public string PreviewPanelDateFormat
        {
            get => Settings.PreviewPanelDateFormat;
            set
            {
                Settings.PreviewPanelDateFormat = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreviewPanelDateFormatDemo));
            }
        }

        public string PreviewPanelTimeFormat
        {
            get => Settings.PreviewPanelTimeFormat;
            set
            {
                Settings.PreviewPanelTimeFormat = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreviewPanelTimeFormatDemo));
            }
        }

        public string PreviewPanelDateFormatDemo => DateTime.Now.ToString(PreviewPanelDateFormat, CultureInfo.CurrentCulture);
        public string PreviewPanelTimeFormatDemo => DateTime.Now.ToString(PreviewPanelTimeFormat, CultureInfo.CurrentCulture);

        public bool ShowPreviewPanelDateTimeChoices => ShowCreatedDateInPreviewPanel || ShowModifiedDateInPreviewPanel;

        public Visibility PreviewPanelDateTimeChoicesVisibility => ShowCreatedDateInPreviewPanel || ShowModifiedDateInPreviewPanel ? Visibility.Visible : Visibility.Collapsed;


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
                var folderBrowserDialog = new FolderBrowserDialog();

                if (initialDirectory is not null)
                    folderBrowserDialog.InitialDirectory = initialDirectory;

                if (folderBrowserDialog.ShowDialog() != DialogResult.OK)
                    return path;

                path = folderBrowserDialog.SelectedPath;
            }
            else if (type is ResultType.File)
            {
                var openFileDialog = new OpenFileDialog();
                if (initialDirectory is not null)
                    openFileDialog.InitialDirectory = initialDirectory;

                if (openFileDialog.ShowDialog() != DialogResult.OK)
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

            ShellPath = path;
        }

        public string ShellPath
        {
            get => Settings.ShellPath;
            set
            {
                Settings.ShellPath = value;
                OnPropertyChanged();
            }
        }

        public string ExcludedFileTypes
        {
            get => Settings.ExcludedFileTypes;
            set
            {
                // remove spaces and dots from the string before saving
                string sanitized = string.IsNullOrEmpty(value) ? "" : value.Replace(" ", "").Replace(".", "");
                Settings.ExcludedFileTypes = sanitized;
                OnPropertyChanged();
            }
        }

        public int MaxResultLowerLimit { get; } = 1;
        public int MaxResultUpperLimit { get; } = 100000;

        public int MaxResult
        {
            get => Settings.MaxResult;
            set
            {
                Settings.MaxResult = Math.Clamp(value, MaxResultLowerLimit, MaxResultUpperLimit);
                OnPropertyChanged();
            }
        }
    }
}
