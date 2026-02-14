using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Helpers;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Core;

public enum LastQueryModes
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

public class Settings : BaseModel
{
    private FlowLauncherJsonStorage<Settings> _storage = null!;

    public void SetStorage(FlowLauncherJsonStorage<Settings> storage)
    {
        _storage = storage;
    }

    public void Initialize()
    {
        StringMatcher.UserSettingSearchPrecision = QuerySearchPrecision;
    }

    public void Save()
    {
        _storage.Save();
    }

    public string ColorScheme { get; set; } = "System";

    public double WindowSize { get; set; } = 580;
    public Dictionary<string, string> Hotkeys { get; set; } = [];

    public bool UseDropShadowEffect { get; set; } = true;

    /* Appearance Settings. It should be separated from the setting later.*/
    public double WindowHeightSize { get; set; } = 42;
    public double ItemHeightSize { get; set; } = 58;
    public double QueryBoxFontSize { get; set; } = 16;
    public double ResultItemFontSize { get; set; } = 16;
    public double ResultSubItemFontSize { get; set; } = 13;
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
                StringMatcher.UserSettingSearchPrecision = value;
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
    public bool AlwaysRunAsAdministrator { get; set; } = true;
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
    public LastQueryModes LastQueryMode { get; set; } = LastQueryModes.Selected;

    // This needs to be loaded last by staying at the bottom
    public PluginsSettings PluginSettings { get; set; } = new PluginsSettings();
}
