using System.ComponentModel;
using Flow.Launcher.Infrastructure.Helpers;

namespace Flow.Launcher.PluginSDK.API;

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
    #region General
    /// <summary>
    /// When true, Flow Launcher will always run with administrator privileges.
    /// </summary>
    /// <remarks>Don't use this to determine whether we are running as admin or not.</remarks>
    public bool AlwaysRunAsAdmin { get; set; }
    /// <summary>
    /// Indicates whether the Windows taskbar should be shown when Flow Launcher is shown.
    /// </summary>
    /// <remarks>Only has an effect when the taskbar is configured to auto-hide.</remarks>
    public bool ShowTaskbarWhenOpened { get; set; }

    #region Display
    /// <summary>
    /// Indicates which display Flow Launcher should be shown on.
    /// </summary>
    public DisplayType Display { get; set; }
    /// <summary>
    /// The number of the display where Flow Launcher should be shown when 
    /// <see cref="Display"/> is set to <see cref="DisplayType.Custom"/>.
    /// </summary>
    public int DisplayNumber { get; set; }
    /// <summary>
    /// Used to remember the last display Flow Launcher was shown on when 
    /// <see cref="Display"/> is set to <see cref="DisplayType.RememberLastDisplay"/>.
    /// </summary>
    public double LastDisplayWidth { get; set; }
    /// <summary>
    /// Used to remember the last display Flow Launcher was shown on when 
    /// <see cref="Display"/> is set to <see cref="DisplayType.RememberLastDisplay"/>.
    /// </summary>
    public double LastDisplayHeight { get; set; }

    /// <summary>
    /// Indicates where on the display Flow Launcher should be shown.
    /// </summary>
    public DisplayPosition DisplayPosition { get; set; }
    /// <summary>
    /// The custom position of Flow Launcher on the screen when 
    /// <see cref="DisplayPosition"/> is set to <see cref="DisplayPosition.Custom"/>.
    /// </summary>
    public double CustomDisplayPositionLeft { get; set; }
    /// <summary>
    /// The custom position of Flow Launcher on the screen when 
    /// <see cref="DisplayPosition"/> is set to <see cref="DisplayPosition.Custom"/>.
    /// </summary>
    public double CustomDisplayPositionTop { get; set; }

    /// <summary>
    /// Indicates whether Flow Launcher should always be shown on top of all other windows.
    /// </summary>
    public bool ShowAtTopmost { get; set; }

    /// <summary>
    /// When true, Flow Launcher will not show itself on startup.
    /// </summary>
    /// <remarks>
    /// Useful when Flow Launcher is configured to start on system startup.
    /// </remarks>
    public bool HideOnStartup { get; set; }
    /// <summary>
    /// Indicates whether Flow Launcher should hide when it loses focus.
    /// </summary>
    public bool HideOnLostFocus { get; set; }
    #endregion

    #region Query & Results
    /// <summary>
    /// If true, plugins will be able to provide results when the query is empty.
    /// </summary>
    public bool ShowHomePage { get; set; }
    /// <summary>
    /// Indicates whether the preview panel is automatically shown.
    /// </summary>
    public bool AlwaysPreview { get; set; }

    /// <summary>
    /// Indicates how identical the query and the result should be for the result to be shown.
    /// </summary>
    public SearchPrecision QuerySearchPrecision { get; set; }
    /// <summary>
    /// Indicates what should happen to the last query and its results when Flow Launcher is shown.
    /// </summary>
    public LastQueryMode LastQueryMode { get; set; }
    #endregion
    #endregion

    #region Appearance
    /// <summary>
    /// Indicates the color scheme of Flow Launcher.
    /// </summary>
    public ColorScheme ColorScheme { get; set; }
    /// <summary>
    /// Indicates whether Flow Launcher should use a drop shadow effect for its window.
    /// </summary>
    public bool UseDropShadowEffect { get; set; }

    /// <summary>
    /// Determines the height of the Flow Launcher window based on the number of results that should be shown.
    /// </summary>
    public int MaxResultsToShow { get; set; }
    /// <summary>
    /// Indicates whether the window size is fixed (not adjustable by dragging).
    /// </summary>
    public bool FixedWindowSize { get; set; }

    /// <summary>
    /// The current width of the Flow Launcher window.
    /// </summary>
    public double WindowWidth { get; set; }
    public double WindowLeft { get; set; }
    public double WindowTop { get; set; }
    public double WindowHeightSize { get; set; }
    public double ItemHeightSize { get; set; }
    public double QueryBoxFontSize { get; set; }
    public double ResultItemFontSize { get; set; }
    public double ResultSubItemFontSize { get; set; }
    #endregion

    #region Hotkeys
    /// <summary>
    /// Indicates whether Flow Launcher should ignore hotkeys when a fullscreen application is running.
    /// </summary>
    public bool IgnoreHotkeysOnFullscreen { get; set; }
    #endregion
}
