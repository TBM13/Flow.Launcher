using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flow.Launcher.Infrastructure.Hotkeys;

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
    public Hotkey Hotkey
    {
        get;
        internal set
        {
            field = value;
            if (value.IsValid)
                Gesture = new KeyGesture(Hotkey.MainKey, Hotkey.Modifiers);
            else
                Gesture = null;
        }
    }

    /// <summary>
    /// Represents <see cref="Hotkey"/>. Helper for WPF bindings.
    /// </summary>
    [ObservableProperty]
    public partial KeyGesture? Gesture { get; private set; }

    /// <summary>
    /// An user-friendly description of the hotkey's action.
    /// </summary>
    public string Description { get; init; }

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
