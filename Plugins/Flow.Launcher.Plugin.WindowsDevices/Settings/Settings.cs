using CommunityToolkit.Mvvm.ComponentModel;

namespace Flow.Launcher.Plugin.WindowsDevices.Settings;

public partial class Settings : ObservableObject
{
    [ObservableProperty]
    public partial bool ShowHiddenDevices { get; set; } = false;
}
