namespace Flow.Launcher.Plugin.Explorer
{
    public class Settings
    {
        public int MaxResult { get; set; } = 100;
        public string EditorPath { get; set; } = "";

        public string FolderEditorPath { get; set; } = "";

        public string ShellPath { get; set; } = "cmd";

        public string ExcludedFileTypes { get; set; } = "";

        public bool UseLocationAsWorkingDir { get; set; } = false;

        public bool ShowInlinedWindowsContextMenu { get; set; } = false;

        public string WindowsContextMenuIncludedItems { get; set; } = string.Empty;

        public string WindowsContextMenuExcludedItems { get; set; } = string.Empty;

        public bool DisplayMoreInformationInToolTip { get; set; } = false;

        public bool ShowFileSizeInPreviewPanel { get; set; } = true;

        public bool ShowCreatedDateInPreviewPanel { get; set; } = true;

        public bool ShowModifiedDateInPreviewPanel { get; set; } = true;

        public bool ShowFileAgeInPreviewPanel { get; set; } = false;

        public string PreviewPanelDateFormat { get; set; } = "yyyy-MM-dd";

        public string PreviewPanelTimeFormat { get; set; } = "HH:mm";
    }
}
