using System;

namespace Flow.Launcher.Infrastructure;

/// <summary>
/// A delegate for when the visibility is changed
/// </summary>
/// <param name="sender"></param>
/// <param name="args"></param>
public delegate void VisibilityChangedEventHandler(object sender, VisibilityChangedEventArgs args);

/// <summary>
/// A delegate for when the actual application theme is changed
/// </summary>
/// <param name="sender"></param>
/// <param name="args"></param>
public delegate void ActualApplicationThemeChangedEventHandler(object sender, ActualApplicationThemeChangedEventArgs args);

/// <summary>
/// The event args for <see cref="VisibilityChangedEventHandler"/>
/// </summary>
public class VisibilityChangedEventArgs : EventArgs
{
    /// <summary>
    /// <see langword="true"/> if the main window has become visible
    /// </summary>
    public bool IsVisible { get; init; }
}

/// <summary>
/// The event args for <see cref="ActualApplicationThemeChangedEventHandler"/>
/// </summary>
public class ActualApplicationThemeChangedEventArgs : EventArgs
{
    /// <summary>
    /// <see langword="true"/> if the application has changed actual theme
    /// </summary>
    public bool IsDark { get; init; }
}
