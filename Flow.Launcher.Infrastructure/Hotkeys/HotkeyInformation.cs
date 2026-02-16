using CommunityToolkit.Mvvm.ComponentModel;

namespace Flow.Launcher.Infrastructure.Hotkeys;

public partial class GlobalHotkeyInformation(string id, string defaultHotkey, string description)
    : HotkeyInformation(id, defaultHotkey, description);

public partial class HotkeyInformation : ObservableObject
{
    /// <summary>
    /// An unique ID that identifies the hotkey.
    /// </summary>
    public string Id { get; init; }

    public Hotkey DefaultHotkey { get; init; }
    /// <summary>
    /// The hotkey that is currently assigned. This can be changed by the user.
    /// </summary>
    [ObservableProperty]
    public partial Hotkey Hotkey { get; internal set; }

    /// <summary>
    /// An user-friendly description of the hotkey's action.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// If true, the user can disable the hotkey.
    /// <para/>
    /// When disabled, <see cref="Hotkey"/> will be set to <c>default</c>.
    /// </summary>
    public bool CanBeDisabled { get; init; } = true;

    internal HotkeyInformation(string id, string defaultHotkey, string description)
    {
        Id = id;
        DefaultHotkey = Hotkey.FromString(defaultHotkey);
        Hotkey = DefaultHotkey;
        Description = description;
    }

    public override string ToString()
    {
        return Hotkey.ToString() + $" ({Description})";
    }
}
