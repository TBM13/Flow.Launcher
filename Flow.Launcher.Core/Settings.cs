using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Helpers;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Core;

public enum LastQueryModes
{
    [Description("Select last Query")]
    Selected,
    [Description("Empty last Query")]
    Empty,
    [Description("Preserve Last Query")]
    Preserved,
    [Description("Preserve Last Action Keyword")]
    ActionKeywordPreserved,
    [Description("Select Last Action Keyword")]
    ActionKeywordSelected
}

public enum ColorSchemes
{
    [Description("System Default")]
    System,
    [Description("Light")]
    Light,
    [Description("Dark")]
    Dark
}

public enum SearchWindowScreens
{
    [Description("Remember Last Position")]
    RememberLastLaunchLocation,
    [Description("Monitor with Mouse Cursor")]
    Cursor,
    [Description("Monitor with Focused Window")]
    Focus,
    [Description("Primary Monitor")]
    Primary,
    [Description("Custom Monitor")]
    Custom
}

public enum SearchWindowAligns
{
    [Description("Center")]
    Center,
    [Description("Center Top")]
    CenterTop,
    [Description("Left Top")]
    LeftTop,
    [Description("Right Top")]
    RightTop,
    [Description("Custom Position")]
    Custom
}

public partial class Settings : ObservableObject
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

    [ObservableProperty]
    public partial string ColorScheme { get; set; } = "System";

    [ObservableProperty]
    public partial double WindowSize { get; set; } = 580;

    public Dictionary<string, string> Hotkeys { get; init; } = [];

    [ObservableProperty]
    public partial bool UseDropShadowEffect { get; set; } = true;

    /* Appearance Settings. It should be separated from the setting later.*/
    [ObservableProperty]
    public partial double WindowHeightSize { get; set; } = 42;
    [ObservableProperty]
    public partial double ItemHeightSize { get; set; } = 58;
    [ObservableProperty]
    public partial double QueryBoxFontSize { get; set; } = 16;
    [ObservableProperty]
    public partial double ResultItemFontSize { get; set; } = 16;
    [ObservableProperty]
    public partial double ResultSubItemFontSize { get; set; } = 13;
    [ObservableProperty]
    public partial bool FirstLaunch { get; set; } = true;

    [ObservableProperty]
    public partial double SettingWindowWidth { get; set; } = 1000;
    [ObservableProperty]
    public partial double SettingWindowHeight { get; set; } = 700;
    [ObservableProperty]
    public partial double? SettingWindowTop { get; set; } = null;
    [ObservableProperty]
    public partial double? SettingWindowLeft { get; set; } = null;
    [ObservableProperty]
    public partial WindowState SettingWindowState { get; set; } = WindowState.Normal;

    [ObservableProperty]
    public partial bool ShowHomePage { get; set; } = false;

    [ObservableProperty]
    public partial bool AlwaysPreview { get; set; } = false;

    [JsonInclude, JsonConverter(typeof(JsonStringEnumConverter))]
    public SearchPrecisionScore QuerySearchPrecision
    {
        get => field;
        set
        {
            SetProperty(ref field, value);
            StringMatcher.UserSettingSearchPrecision = value;
        }
    } = SearchPrecisionScore.Regular;

    [ObservableProperty]
    public partial double WindowLeft { get; set; }
    [ObservableProperty]
    public partial double WindowTop { get; set; }
    [ObservableProperty]
    public partial double PreviousScreenWidth { get; set; }
    [ObservableProperty]
    public partial double PreviousScreenHeight { get; set; }

    /// <summary>
    /// Custom left position on selected monitor
    /// </summary>
    [ObservableProperty]
    public partial double CustomWindowLeft { get; set; } = 0;

    /// <summary>
    /// Custom top position on selected monitor
    /// </summary>
    [ObservableProperty]
    public partial double CustomWindowTop { get; set; } = 0;

    /// <summary>
    /// Fixed window size
    /// </summary>
    [ObservableProperty]
    public partial bool KeepMaxResults { get; set; }

    [ObservableProperty]
    public partial int MaxResultsToShow { get; set; } = 5;

    public ObservableCollection<CustomPluginHotkey> CustomPluginHotkeys { get; init; } = [];

    public ObservableCollection<CustomShortcutModel> CustomShortcuts { get; init; } = [];

    [JsonIgnore]
    public ObservableCollection<BaseBuiltinShortcutModel> BuiltinShortcuts { get; } =
    [
        new AsyncBuiltinShortcutModel("{clipboard}", "shortcut_clipboard_description", () => Win32Helper.StartSTATaskAsync(Clipboard.GetText)),
        new BuiltinShortcutModel("{active_explorer_path}", "shortcut_active_explorer_path", () => FileExplorerHelper.GetForegroundExplorerPath() ?? "<error>")
    ];

    [ObservableProperty]
    public partial bool HideOnStartup { get; set; } = true;
    [ObservableProperty]
    public partial bool HideWhenDeactivated { get; set; } = true;
    [ObservableProperty]
    public partial bool AlwaysRunAsAdministrator { get; set; } = true;
    [ObservableProperty]
    public partial bool ShowTaskbarWhenInvoked { get; set; } = false;

    [ObservableProperty]
    public partial bool ShowAtTopmost { get; set; }


    [JsonConverter(typeof(JsonStringEnumConverter))]
    [ObservableProperty]
    public partial SearchWindowScreens SearchWindowScreen { get; set; } = SearchWindowScreens.Cursor;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    [ObservableProperty]
    public partial SearchWindowAligns SearchWindowAlign { get; set; } = SearchWindowAligns.Center;

    [ObservableProperty]
    public partial int CustomScreenNumber { get; set; } = 1;

    [ObservableProperty]
    public partial bool IgnoreHotkeysOnFullscreen { get; set; }


    [JsonConverter(typeof(JsonStringEnumConverter))]
    [ObservableProperty]
    public partial LastQueryModes LastQueryMode { get; set; } = LastQueryModes.Selected;

    // This needs to be loaded last by staying at the bottom
    [ObservableProperty]
    public partial PluginsSettings PluginSettings { get; set; } = new PluginsSettings();
}
