using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Infrastructure.Results;

namespace Flow.Launcher.Infrastructure.Hotkeys;

/// <summary>
/// Represents the information of a hotkey that can be triggered even when
/// Flow Launcher is not focused and not visible.
/// </summary>
public class GlobalHotkeyInfo : HotkeyInfo
{
    /// <summary>
    /// The action that will be executed when the hotkey is triggered.
    /// </summary>
    public required Action OnHotkeyTriggered { get; init; }
}

/// <summary>
/// Represents the information of a hotkey that can be triggered when Flow Launcher is focused.
/// </summary>
public class AppHotkeyInfo : HotkeyInfo
{
    /// <summary>
    /// The action that will be executed when the hotkey is triggered.
    /// </summary>
    // public required Action OnHotkeyTriggered { get; init; }

    // TODO: Properly integrate WPF hotkeys into HotkeyManager (and use OnHotkeyTriggered)
}

/// <summary>
/// Represents the information of a hotkey tied to the <see cref="Result"/>s of a plugin.
/// </summary>
/// <remarks>
/// The hotkey will only be triggerable when one of the plugin's results is selected.
/// </remarks>
public class ResultHotkeyInfo : HotkeyInfo
{
    /// <summary>
    /// The function that will be executed when the hotkey is triggered.
    /// <para/>
    /// If the function returns true, Flow Launcher's window will be hidden.
    /// </summary>
    public required Func<Result, ActionContext, bool> OnHotkeyTriggered { get; init; }
}

/// <summary>
/// Represents the information of a hotkey.
/// </summary>
public abstract partial class HotkeyInfo : ObservableObject
{
    /// <summary>
    /// An unique ID that identifies this hotkey.
    /// </summary>
    /// <remarks>
    /// Must not collide with any other hotkey's ID, so it is suggested
    /// to include an unique string like the plugin ID.
    /// </remarks>
    public required string Id { get; init; }

    /// <summary>
    /// An user-friendly name of the hotkey.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// An optional user-friendly description of the hotkey.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The default hotkey.
    /// </summary>
    public required Hotkey DefaultHotkey { get; init; }

    /// <summary>
    /// Whether the user can disable the hotkey.
    /// </summary>
    public bool CanBeDisabled { get; internal init; } = true;

    /// <summary>
    /// The hotkey that is currently assigned. This can be changed by the user.
    /// </summary>
    /// <remarks>If the hotkey is disabled, the value of this will be <see langword="default"/></remarks>
    [ObservableProperty]
    public partial Hotkey Hotkey { get; internal set; }

    public override string ToString()
    {
        return Id + (Hotkey == default ? string.Empty : $" ({Hotkey})");
    }
}
