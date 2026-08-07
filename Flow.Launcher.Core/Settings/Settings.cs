using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Core.Storage;
using Flow.Launcher.Core.UserSettings;
using Flow.Launcher.Interop;
using Flow.Launcher.Interop.Shell;
using Flow.Launcher.PluginSDK;

namespace Flow.Launcher.Core.Settings;

internal partial class Settings : ObservableObject, ISettingsAPI
{
    private JsonStorage<Settings> _storage = null!;

    internal void SetStorage(JsonStorage<Settings> storage)
        => _storage = storage;

    public void Save()
        => _storage.TrySave();

    #region General
    [ObservableProperty]
    public partial bool AlwaysRunAsAdmin { get; set; } = false;
    [ObservableProperty]
    public partial bool ShowTaskbarWhenOpened { get; set; } = false;

    #region Display
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [ObservableProperty]
    public partial DisplayType Display { get; set; } = DisplayType.Cursor;
    [ObservableProperty]
    public partial int DisplayNumber { get; set; } = 1;
    [ObservableProperty]
    public partial double LastDisplayWidth { get; set; }
    [ObservableProperty]
    public partial double LastDisplayHeight { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    [ObservableProperty]
    public partial DisplayPosition DisplayPosition { get; set; } = DisplayPosition.Center;
    [ObservableProperty]
    public partial double CustomDisplayPositionLeft { get; set; } = 0;
    [ObservableProperty]
    public partial double CustomDisplayPositionTop { get; set; } = 0;

    [ObservableProperty]
    public partial bool HideOnStartup { get; set; } = true;
    [ObservableProperty]
    public partial bool HideOnLostFocus { get; set; } = true;
    #endregion

    #region Query & Results
    [ObservableProperty]
    public partial bool ShowHomePage { get; set; } = false;

    [ObservableProperty]
    public partial bool AlwaysPreview { get; set; } = false;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    [ObservableProperty]
    public partial SearchPrecision QuerySearchPrecision { get; set; } = SearchPrecision.Regular;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    [ObservableProperty]
    public partial LastQueryMode LastQueryMode { get; set; } = LastQueryMode.Selected;
    #endregion
    #endregion

    #region Appearance
    [ObservableProperty]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public partial ColorScheme ColorScheme { get; set; } = ColorScheme.System;


    [ObservableProperty]
    public partial int MaxResultsToShow { get; set; } = 5;

    [ObservableProperty]
    public partial bool FixedWindowSize { get; set; }

    [ObservableProperty]
    public partial double WindowWidth { get; set; } = 785;

    [ObservableProperty]
    public partial double SettingWindowWidth { get; set; } = 1000;
    [ObservableProperty]
    public partial double SettingWindowHeight { get; set; } = 700;
    [ObservableProperty]
    public partial bool SettingWindowMaximized { get; set; } = false;
    #endregion

    #region Hotkeys / Shortcuts
    [ObservableProperty]
    public partial bool IgnoreHotkeysOnFullscreen { get; set; }

    public Dictionary<string, string> Hotkeys { get; init; } = [];
    #endregion

    // This needs to be loaded last by staying at the bottom
    [ObservableProperty]
    public partial PluginsSettings PluginSettings { get; set; } = new PluginsSettings();
}
