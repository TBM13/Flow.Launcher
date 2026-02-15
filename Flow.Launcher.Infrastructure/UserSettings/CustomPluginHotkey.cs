using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flow.Launcher.Infrastructure.UserSettings;

public partial class CustomPluginHotkey(string hotkey, string actionKeyword) : ObservableObject
{
    [ObservableProperty]
    public partial string Hotkey { get; set; } = hotkey;
    [ObservableProperty]
    public partial string ActionKeyword { get; set; } = actionKeyword;

    public override bool Equals(object? other)
    {
        if (other is CustomPluginHotkey otherHotkey)
        {
            return Hotkey == otherHotkey.Hotkey && ActionKeyword == otherHotkey.ActionKeyword;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Hotkey, ActionKeyword);
    }
}
