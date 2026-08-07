using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using Flow.Launcher.Core.UserSettings;
using Flow.Launcher.PluginSDK;

namespace Flow.Launcher.Core.Settings;

public enum LastQueryMode
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

public enum ColorScheme
{
    [Description("System Default")]
    System,
    [Description("Light")]
    Light,
    [Description("Dark")]
    Dark
}

public enum DisplayType
{
    [Description("Remember Last Display")]
    RememberLastDisplay,
    [Description("Display with Mouse Cursor")]
    Cursor,
    [Description("Display with Focused Window")]
    Focus,
    [Description("Primary Display")]
    Primary,
    [Description("Custom Display")]
    Custom
}

public enum DisplayPosition
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

/// <summary>
/// Contains methods and properties for managing Flow Launcher's settings.
/// </summary>
public interface ISettingsAPI : INotifyPropertyChanged, INotifyPropertyChanging
{
    void Save();

    #region General
    /// <summary>
    /// When true, Flow Launcher will always run with administrator privileges.
    /// </summary>
    /// <remarks>Don't use this to determine whether we are running as admin or not.</remarks>
    bool AlwaysRunAsAdmin { get; set; }
    /// <summary>
    /// Indicates whether the Windows taskbar should be shown when Flow Launcher is shown.
    /// </summary>
    /// <remarks>Only has an effect when the taskbar is configured to auto-hide.</remarks>
    bool ShowTaskbarWhenOpened { get; set; }

    #region Display
    /// <summary>
    /// Indicates which display Flow Launcher should be shown on.
    /// </summary>
    DisplayType Display { get; set; }
    /// <summary>
    /// The number of the display where Flow Launcher should be shown when 
    /// <see cref="Display"/> is set to <see cref="DisplayType.Custom"/>.
    /// </summary>
    int DisplayNumber { get; set; }
    /// <summary>
    /// Used to remember the last display Flow Launcher was shown on when 
    /// <see cref="Display"/> is set to <see cref="DisplayType.RememberLastDisplay"/>.
    /// </summary>
    double LastDisplayWidth { get; set; }
    /// <summary>
    /// Used to remember the last display Flow Launcher was shown on when 
    /// <see cref="Display"/> is set to <see cref="DisplayType.RememberLastDisplay"/>.
    /// </summary>
    double LastDisplayHeight { get; set; }

    /// <summary>
    /// Indicates where on the display Flow Launcher should be shown.
    /// </summary>
    DisplayPosition DisplayPosition { get; set; }
    /// <summary>
    /// The custom position of Flow Launcher on the screen when 
    /// <see cref="DisplayPosition"/> is set to <see cref="DisplayPosition.Custom"/>.
    /// </summary>
    double CustomDisplayPositionLeft { get; set; }
    /// <summary>
    /// The custom position of Flow Launcher on the screen when 
    /// <see cref="DisplayPosition"/> is set to <see cref="DisplayPosition.Custom"/>.
    /// </summary>
    double CustomDisplayPositionTop { get; set; }

    /// <summary>
    /// When true, Flow Launcher will not show itself on startup.
    /// </summary>
    /// <remarks>
    /// Useful when Flow Launcher is configured to start on system startup.
    /// </remarks>
    bool HideOnStartup { get; set; }
    /// <summary>
    /// Indicates whether Flow Launcher should hide when it loses focus.
    /// </summary>
    bool HideOnLostFocus { get; set; }
    #endregion

    #region Query & Results
    /// <summary>
    /// If true, plugins will be able to provide results when the query is empty.
    /// </summary>
    bool ShowHomePage { get; set; }
    /// <summary>
    /// Indicates whether the preview panel is automatically shown.
    /// </summary>
    bool AlwaysPreview { get; set; }

    /// <summary>
    /// Indicates how identical the query and the result should be for the result to be shown.
    /// </summary>
    SearchPrecision QuerySearchPrecision { get; set; }
    /// <summary>
    /// Indicates what should happen to the last query and its results when Flow Launcher is shown.
    /// </summary>
    LastQueryMode LastQueryMode { get; set; }
    #endregion
    #endregion

    #region Appearance
    /// <summary>
    /// Indicates the color scheme of Flow Launcher.
    /// </summary>
    ColorScheme ColorScheme { get; set; }

    /// <summary>
    /// Determines the height of the Flow Launcher window based on the number of results that should be shown.
    /// </summary>
    int MaxResultsToShow { get; set; }
    /// <summary>
    /// Indicates whether the window size is fixed (not adjustable by dragging).
    /// </summary>
    bool FixedWindowSize { get; set; }

    /// <summary>
    /// The current width of the Flow Launcher window.
    /// </summary>
    double WindowWidth { get; set; }

    double SettingWindowWidth { get; set; }
    double SettingWindowHeight { get; set; }
    bool SettingWindowMaximized { get; set; }
    #endregion

    #region Hotkeys
    /// <summary>
    /// Indicates whether Flow Launcher should ignore hotkeys when a fullscreen application is running.
    /// </summary>
    bool IgnoreHotkeysOnFullscreen { get; set; }

    Dictionary<string, string> Hotkeys { get; }
    #endregion

    PluginsSettings PluginSettings { get; }
}
