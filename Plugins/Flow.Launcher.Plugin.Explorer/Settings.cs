using CommunityToolkit.Mvvm.ComponentModel;

namespace Flow.Launcher.Plugin.Explorer;

public partial class Settings : ObservableObject
{
    [ObservableProperty]
    public partial bool UseLocationAsWorkingDir { get; set; } = false;

    [ObservableProperty]
    public partial bool ShowFileSizeInPreviewPanel { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowCreatedDateInPreviewPanel { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowModifiedDateInPreviewPanel { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowFileAgeInPreviewPanel { get; set; } = false;

    [ObservableProperty]
    public partial string PreviewPanelDateFormat { get; set; } = "yyyy-MM-dd";

    [ObservableProperty]
    public partial string PreviewPanelTimeFormat { get; set; } = "HH:mm";
}
