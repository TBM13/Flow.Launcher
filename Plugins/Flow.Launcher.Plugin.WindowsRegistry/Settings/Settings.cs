using CommunityToolkit.Mvvm.ComponentModel;

namespace Flow.Launcher.Plugin.WindowsRegistry.Settings;

public partial class Settings : ObservableObject
{
    [ObservableProperty]
    public partial string RegJumpPath { get; set; } = string.Empty;
}
