using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class Settings : BaseModel, IHotkeySettings
    {
        private FlowLauncherJsonStorage<Settings> _storage = null!;
        private StringMatcher _stringMatcher = null!;

        public void SetStorage(FlowLauncherJsonStorage<Settings> storage)
        {
            _storage = storage;
        }

        public void Initialize()
        {
            // Initialize dependency injection instances after Ioc.Default is created
            _stringMatcher = Ioc.Default.GetRequiredService<StringMatcher>();
            _stringMatcher.UserSettingSearchPrecision = QuerySearchPrecision;
        }

        public void Save()
        {
            _storage.Save();
        }

        public string Hotkey { get; set; } = $"{KeyConstant.Alt} + {KeyConstant.Space}";

        public string ColorScheme { get; set; } = "System";

        public double WindowSize { get; set; } = 580;
        public string PreviewHotkey { get; set; } = $"F1";
        public string AutoCompleteHotkey { get; set; } = $"Tab";
        public string AutoCompleteHotkey2 { get; set; } = $"";
        public string SelectNextItemHotkey { get; set; } = $"";
        public string SelectNextItemHotkey2 { get; set; } = $"";
        public string SelectPrevItemHotkey { get; set; } = $"";
        public string SelectPrevItemHotkey2 { get; set; } = $"";
        public string SelectNextPageHotkey { get; set; } = $"PageUp";
        public string SelectPrevPageHotkey { get; set; } = $"PageDown";
        public string OpenContextMenuHotkey { get; set; } = $"Ctrl+O";
        public string SettingWindowHotkey { get; set; } = $"Ctrl+I";

        private string _theme = Constant.DefaultTheme;
        public string Theme
        {
            get => _theme;
            set
            {
                if (_theme != value)
                {
                    _theme = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MaxResultsToShow));
                }
            }
        }
        public bool UseDropShadowEffect { get; set; } = true;

        /* Appearance Settings. It should be separated from the setting later.*/
        public double WindowHeightSize { get; set; } = 42;
        public double ItemHeightSize { get; set; } = 58;
        public double QueryBoxFontSize { get; set; } = 16;
        public double ResultItemFontSize { get; set; } = 16;
        public double ResultSubItemFontSize { get; set; } = 13;
        public bool UseGlyphIcons { get; set; } = true;

        public bool FirstLaunch { get; set; } = true;

        public double SettingWindowWidth { get; set; } = 1000;
        public double SettingWindowHeight { get; set; } = 700;
        public double? SettingWindowTop { get; set; } = null;
        public double? SettingWindowLeft { get; set; } = null;
        public WindowState SettingWindowState { get; set; } = WindowState.Normal;

        private bool _showHomePage { get; set; } = false;
        public bool ShowHomePage
        {
            get => _showHomePage;
            set
            {
                if (_showHomePage != value)
                {
                    _showHomePage = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CustomExplorerIndex { get; set; } = 0;

        [JsonIgnore]
        public CustomExplorerViewModel CustomExplorer
        {
            get => CustomExplorerList[CustomExplorerIndex < CustomExplorerList.Count ? CustomExplorerIndex : 0];
            set => CustomExplorerList[CustomExplorerIndex] = value;
        }

        public List<CustomExplorerViewModel> CustomExplorerList { get; set; } =
        [
            new()
            {
                Name = "Explorer",
                Path = "explorer",
                DirectoryArgument = "\"%d\"",
                FileArgument = "/select, \"%f\"",
                Editable = false
            },
            new()
            {
                Name = "Total Commander",
                Path = @"C:\Program Files\totalcmd\TOTALCMD64.exe",
                DirectoryArgument = "/O /A /S /T \"%d\"",
                FileArgument = "/O /A /S /T \"%f\""
            },
            new()
            {
                Name = "Directory Opus",
                Path = @"C:\Program Files\GPSoftware\Directory Opus\dopusrt.exe",
                DirectoryArgument = "/cmd Go \"%d\" NEW",
                FileArgument = "/cmd Go \"%f\" NEW"

            },
            new()
            {
                Name = "Files",
                Path = "Files-Stable",
                DirectoryArgument = "\"%d\"",
                FileArgument = "-select \"%f\""
            }
        ];

        public bool AlwaysPreview { get; set; } = false;

        private SearchPrecisionScore _querySearchPrecision = SearchPrecisionScore.Regular;
        [JsonInclude, JsonConverter(typeof(JsonStringEnumConverter))]
        public SearchPrecisionScore QuerySearchPrecision
        {
            get => _querySearchPrecision;
            set
            {
                if (_querySearchPrecision != value)
                {
                    _querySearchPrecision = value;
                    _stringMatcher?.UserSettingSearchPrecision = value;
                }
            }
        }

        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }
        public double PreviousScreenWidth { get; set; }
        public double PreviousScreenHeight { get; set; }

        /// <summary>
        /// Custom left position on selected monitor
        /// </summary>
        public double CustomWindowLeft { get; set; } = 0;

        /// <summary>
        /// Custom top position on selected monitor
        /// </summary>
        public double CustomWindowTop { get; set; } = 0;

        /// <summary>
        /// Fixed window size
        /// </summary>
        private bool _keepMaxResults { get; set; } = false;
        public bool KeepMaxResults
        {
            get => _keepMaxResults;
            set
            {
                if (_keepMaxResults != value)
                {
                    _keepMaxResults = value;
                    OnPropertyChanged();
                }
            }
        }

        public int MaxResultsToShow { get; set; } = 5;

        public ObservableCollection<CustomPluginHotkey> CustomPluginHotkeys { get; set; } = [];

        public ObservableCollection<CustomShortcutModel> CustomShortcuts { get; set; } = [];

        [JsonIgnore]
        public ObservableCollection<BaseBuiltinShortcutModel> BuiltinShortcuts { get; set; } =
        [
            new AsyncBuiltinShortcutModel("{clipboard}", "shortcut_clipboard_description", () => Win32Helper.StartSTATaskAsync(Clipboard.GetText)),
            new BuiltinShortcutModel("{active_explorer_path}", "shortcut_active_explorer_path", () => FileExplorerHelper.GetActiveExplorerPath() ?? "<error>")
        ];

        public bool HideOnStartup { get; set; } = true;
        public bool HideWhenDeactivated { get; set; } = true;
        public bool ShowTaskbarWhenInvoked { get; set; } = false;

        private bool _showAtTopmost = false;
        public bool ShowAtTopmost
        {
            get => _showAtTopmost;
            set
            {
                if (_showAtTopmost != value)
                {
                    _showAtTopmost = value;
                    OnPropertyChanged();
                }
            }
        }


        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SearchWindowScreens SearchWindowScreen { get; set; } = SearchWindowScreens.Cursor;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SearchWindowAligns SearchWindowAlign { get; set; } = SearchWindowAligns.Center;

        public int CustomScreenNumber { get; set; } = 1;

        public bool IgnoreHotkeysOnFullscreen { get; set; }


        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LastQueryMode LastQueryMode { get; set; } = LastQueryMode.Selected;

        // This needs to be loaded last by staying at the bottom
        public PluginsSettings PluginSettings { get; set; } = new PluginsSettings();

        [JsonIgnore]
        public List<RegisteredHotkeyData> RegisteredHotkeys
        {
            get
            {
                var list = FixedHotkeys();

                // Customizeable hotkeys
                if (!string.IsNullOrEmpty(Hotkey))
                    list.Add(new(Hotkey, "flowlauncherHotkey", () => Hotkey = ""));
                if (!string.IsNullOrEmpty(PreviewHotkey))
                    list.Add(new(PreviewHotkey, "previewHotkey", () => PreviewHotkey = ""));
                if (!string.IsNullOrEmpty(AutoCompleteHotkey))
                    list.Add(new(AutoCompleteHotkey, "autoCompleteHotkey", () => AutoCompleteHotkey = ""));
                if (!string.IsNullOrEmpty(AutoCompleteHotkey2))
                    list.Add(new(AutoCompleteHotkey2, "autoCompleteHotkey", () => AutoCompleteHotkey2 = ""));
                if (!string.IsNullOrEmpty(SelectNextItemHotkey))
                    list.Add(new(SelectNextItemHotkey, "SelectNextItemHotkey", () => SelectNextItemHotkey = ""));
                if (!string.IsNullOrEmpty(SelectNextItemHotkey2))
                    list.Add(new(SelectNextItemHotkey2, "SelectNextItemHotkey", () => SelectNextItemHotkey2 = ""));
                if (!string.IsNullOrEmpty(SelectPrevItemHotkey))
                    list.Add(new(SelectPrevItemHotkey, "SelectPrevItemHotkey", () => SelectPrevItemHotkey = ""));
                if (!string.IsNullOrEmpty(SelectPrevItemHotkey2))
                    list.Add(new(SelectPrevItemHotkey2, "SelectPrevItemHotkey", () => SelectPrevItemHotkey2 = ""));
                if (!string.IsNullOrEmpty(SettingWindowHotkey))
                    list.Add(new(SettingWindowHotkey, "SettingWindowHotkey", () => SettingWindowHotkey = ""));
                if (!string.IsNullOrEmpty(OpenContextMenuHotkey))
                    list.Add(new(OpenContextMenuHotkey, "OpenContextMenuHotkey", () => OpenContextMenuHotkey = ""));
                if (!string.IsNullOrEmpty(SelectNextPageHotkey))
                    list.Add(new(SelectNextPageHotkey, "SelectNextPageHotkey", () => SelectNextPageHotkey = ""));
                if (!string.IsNullOrEmpty(SelectPrevPageHotkey))
                    list.Add(new(SelectPrevPageHotkey, "SelectPrevPageHotkey", () => SelectPrevPageHotkey = ""));

                // Custom Query Hotkeys
                foreach (var customPluginHotkey in CustomPluginHotkeys)
                {
                    if (!string.IsNullOrEmpty(customPluginHotkey.Hotkey))
                        list.Add(new(customPluginHotkey.Hotkey, "customQueryHotkey", () => customPluginHotkey.Hotkey = ""));
                }

                return list;
            }
        }

        private List<RegisteredHotkeyData> FixedHotkeys()
        {
            return
            [
                new("Up", "HotkeyLeftRightDesc"),
                new("Down", "HotkeyLeftRightDesc"),
                new("Left", "HotkeyUpDownDesc"),
                new("Right", "HotkeyUpDownDesc"),
                new("Escape", "HotkeyESCDesc"),
                new("F5", "ReloadPluginHotkey"),
                new("Alt+Home", "HotkeySelectFirstResult"),
                new("Alt+End", "HotkeySelectLastResult"),
                new("Ctrl+R", "HotkeyRequery"),
                new("Ctrl+OemCloseBrackets", "QuickWidthHotkey"),
                new("Ctrl+OemOpenBrackets", "QuickWidthHotkey"),
                new("Ctrl+OemPlus", "QuickHeightHotkey"),
                new("Ctrl+OemMinus", "QuickHeightHotkey"),
                new("Ctrl+Shift+Enter", "HotkeyCtrlShiftEnterDesc"),
                new("Shift+Enter", "OpenContextMenuHotkey"),
                new("Enter", "HotkeyRunDesc"),
                new("Ctrl+Enter", "OpenContainFolderHotkey"),
                new("Alt+Enter", "HotkeyOpenResult"),
                new("Ctrl+F12", "ToggleGameModeHotkey"),
                new("Ctrl+Shift+C", "CopyFilePathHotkey")
            ];
        }
    }

    public enum LastQueryMode
    {
        Selected,
        Empty,
        Preserved,
        ActionKeywordPreserved,
        ActionKeywordSelected
    }

    public enum ColorSchemes
    {
        System,
        Light,
        Dark
    }

    public enum SearchWindowScreens
    {
        RememberLastLaunchLocation,
        Cursor,
        Focus,
        Primary,
        Custom
    }

    public enum SearchWindowAligns
    {
        Center,
        CenterTop,
        LeftTop,
        RightTop,
        Custom
    }
}
