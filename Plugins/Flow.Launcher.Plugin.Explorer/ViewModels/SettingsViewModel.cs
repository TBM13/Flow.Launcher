#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Plugin.Explorer.Helper;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Flow.Launcher.Plugin.Explorer.Views;

namespace Flow.Launcher.Plugin.Explorer.ViewModels
{
    public partial class SettingsViewModel : BaseModel
    {
        public Settings Settings { get; set; }

        internal PluginInitContext Context { get; set; }

        public SettingsViewModel(PluginInitContext context, Settings settings)
        {
            Context = context;
            Settings = settings;

            InitializeActionKeywordModels();
        }

        public void Save()
        {
            Context.API.SaveSettingJsonStorage<Settings>();
        }

        #region Native Context Menu

        public bool ShowWindowsContextMenu
        {
            get => Settings.ShowInlinedWindowsContextMenu;
            set
            {
                Settings.ShowInlinedWindowsContextMenu = value;
                OnPropertyChanged();
            }
        }

        public string WindowsContextMenuIncludedItems
        {
            get => Settings.WindowsContextMenuIncludedItems;
            set
            {
                Settings.WindowsContextMenuIncludedItems = value;
                OnPropertyChanged();
            }
        }

        public string WindowsContextMenuExcludedItems
        {
            get => Settings.WindowsContextMenuExcludedItems;
            set
            {
                Settings.WindowsContextMenuExcludedItems = value;
                OnPropertyChanged();
            }
        }

        #endregion

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


        public List<string> TimeFormatList { get; } = new()
        {
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
        };


        public List<string> DateFormatList { get; } = new()
        {
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
        };

        #endregion

        #region ActionKeyword

        [MemberNotNull(nameof(ActionKeywordsModels))]
        private void InitializeActionKeywordModels()
        {
            ActionKeywordsModels = new List<ActionKeywordModel>
            {
                new(Settings.ActionKeyword.SearchActionKeyword,
                    "plugin_explorer_actionkeywordview_search"),
                new(Settings.ActionKeyword.PathSearchActionKeyword,
                    "plugin_explorer_actionkeywordview_pathsearch"),
                new(Settings.ActionKeyword.QuickAccessActionKeyword,
                    "plugin_explorer_actionkeywordview_quickaccess")
            };
        }

        public IReadOnlyList<ActionKeywordModel> ActionKeywordsModels { get; set; }

        public ActionKeywordModel? SelectedActionKeyword { get; set; }

        [RelayCommand]
        private void EditActionKeyword(object obj)
        {
            if (SelectedActionKeyword is not { } actionKeyword)
            {
                ShowUnselectedMessage();
                return;
            }

            var actionKeywordWindow = new ActionKeywordSetting(actionKeyword);

            if (!(actionKeywordWindow.ShowDialog() ?? false))
            {
                return;
            }

            switch (actionKeyword.Enabled, actionKeywordWindow.KeywordEnabled)
            {
                case (true, false):
                    Context.API.RemoveActionKeyword(Context.CurrentPluginMetadata.ID, actionKeyword.Keyword);
                    break;
                case (true, true):
                    // same keyword will have dialog result false
                    Context.API.RemoveActionKeyword(Context.CurrentPluginMetadata.ID, actionKeyword.Keyword);
                    Context.API.AddActionKeyword(Context.CurrentPluginMetadata.ID, actionKeywordWindow.ActionKeyword);
                    break;
                case (false, true):
                    Context.API.AddActionKeyword(Context.CurrentPluginMetadata.ID, actionKeywordWindow.ActionKeyword);
                    break;
                case (false, false):
                    throw new ArgumentException(
                        $"Both false in {nameof(actionKeyword)}.{nameof(actionKeyword.Enabled)} and {nameof(actionKeywordWindow)}.{nameof(actionKeywordWindow.KeywordEnabled)} should suggest that the ShowDialog() result is false");
            }

            (actionKeyword.Keyword, actionKeyword.Enabled) = (actionKeywordWindow.ActionKeyword, actionKeywordWindow.KeywordEnabled);

        }

        #endregion

        #region AccessLinks

        public AccessLink? SelectedQuickAccessLink { get; set; }

        public void AppendLink(string containerName, AccessLink link)
        {
            var container = containerName switch
            {
                "QuickAccessLink" => Settings.QuickAccessLinks,
                _ => throw new ArgumentException($"Unknown container name: {containerName}")
            };
            container.Add(link);
        }

        [RelayCommand]
        private void EditQuickAccessLink()
        {
            var selectedLink = SelectedQuickAccessLink;
            var collection = Settings.QuickAccessLinks;

            if (selectedLink is null)
            {
                ShowUnselectedMessage();
                return;
            }

            var quickAccessLinkSettings = new QuickAccessLinkSettings(collection, SelectedQuickAccessLink);
            if (quickAccessLinkSettings.ShowDialog() == true)
            {
                Save();
            }
        }

        [RelayCommand]
        private void AddQuickAccessLink()
        {
            var quickAccessLinkSettings = new QuickAccessLinkSettings(Settings.QuickAccessLinks);
            if (quickAccessLinkSettings.ShowDialog() == true)
            {
                Save();
            }
        }

        [RelayCommand]
        private void RemoveLink(object commandParameter)
        {
            if (commandParameter is not string container) return;

            switch (container)
            {
                case "QuickAccessLink":
                    if (SelectedQuickAccessLink == null) return;
                    if (Context.API.ShowMsgBox(
                            Localize.plugin_explorer_delete_quick_access_link(),
                            Localize.plugin_explorer_delete(),
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Warning)
                        == MessageBoxResult.Cancel)
                        return;
                    Settings.QuickAccessLinks.Remove(SelectedQuickAccessLink);
                    break;
            }
            Save();
        }

        private void ShowUnselectedMessage()
        {
            var warning = Localize.plugin_explorer_make_selection_warning();
            Context.API.ShowMsgBox(warning);
        }

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
        private void OpenFileEditorPath()
        {
            var path = PromptUserSelectPath(ResultType.File, Settings.EditorPath != null ? Path.GetDirectoryName(Settings.EditorPath) : null);
            if (path is null)
                return;

            FileEditorPath = path;
        }

        [RelayCommand]
        private void OpenFolderEditorPath()
        {
            var path = PromptUserSelectPath(ResultType.File, Settings.FolderEditorPath != null ? Path.GetDirectoryName(Settings.FolderEditorPath) : null);
            if (path is null)
                return;

            FolderEditorPath = path;
        }

        [RelayCommand]
        private void OpenShellPath()
        {
            var path = PromptUserSelectPath(ResultType.File, Settings.EditorPath != null ? Path.GetDirectoryName(Settings.EditorPath) : null);
            if (path is null)
                return;

            ShellPath = path;
        }

        public string FileEditorPath
        {
            get => Settings.EditorPath;
            set
            {
                Settings.EditorPath = value;
                OnPropertyChanged();
            }
        }

        public string FolderEditorPath
        {
            get => Settings.FolderEditorPath;
            set
            {
                Settings.FolderEditorPath = value;
                OnPropertyChanged();
            }
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
