using CommunityToolkit.Mvvm.ComponentModel;

namespace Flow.Launcher.Plugin.ProcessKiller.Settings;

public partial class Settings : ObservableObject
{
    [ObservableProperty]
    public partial bool ShowWindowTitle { get; set; } = true;

    [ObservableProperty]
    public partial bool PutVisibleWindowProcessesTop { get; set; } = false;

    [ObservableProperty]
    public partial bool FilterSvchostProcesses { get; set; } = true;
}
