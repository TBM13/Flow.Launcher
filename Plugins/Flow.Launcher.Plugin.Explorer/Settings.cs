using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flow.Launcher.Plugin.Explorer
{
    public partial class Settings : ObservableObject
    {
        public int MaxResult
        {
            get => field;
            set => SetProperty(ref field, Math.Clamp(value, 1, 100000));
        } = 100;

        [ObservableProperty]
        public partial string ShellPath { get; set; } = "cmd";

        public string ExcludedFileTypes
        {
            get => field;
            set => SetProperty(ref field, value.Replace(" ", "").Replace(".", ""));
        } = string.Empty;

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
}
