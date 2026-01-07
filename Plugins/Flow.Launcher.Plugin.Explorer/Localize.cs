namespace Flow.Launcher.Plugin.Explorer;

public static class Localize
{
    public const string PluginName = "Explorer";

    // Errors
    public const string Error_OpenDir = "Could not open folder";
    public const string Error_OpenFile = "Could not open file";
    public static string Error_FileNotFound(string file) => $"File not found: {file}";
    public static string Error_OpenWithShell(string filePath, string shellName, string shellPath)
        => $"Failed to open folder {filePath} with Shell {shellName} at {shellPath}";

    // General Results
    public const string GeneralResult_CopyPath = "Copy path";
    public const string GeneralResult_CopyPath_Subtitle = "You can open the containing dir. with CTRL + Click on the result";
    public const string GeneralResult_ShowWindowsMenu = "Show Windows Context Menu";
    public const string GeneralResult_ShowWindowsMenu_Subtitle = "You can also open it with Alt + Click on the result";

    // Disk Results
    public static string DiskResult_FreeSpace(string free, string total) => $"{free} free of {total}";

    // Folder Results
    public const string FolderResult_OpenResultFolder = "Open in Default File Manager";
    public const string FolderResult_OpenResultFolder_Subtitle = "Use '*' as a search wildcard, '>' to include subdirectories.";
    public const string FolderResult_OpenDirectoryTooltip = "Ctrl + Enter to open the directory";
    public const string FolderResult_OpenWithShell = "Open With Shell:";

    // File Results
    public const string FileResult_RunAsDifferentUser = "Run as different user";
    public const string FileResult_RunAsDifferentUser_Subtitle = "Run the selected file using a different user account";
    public const string FileResult_OpenContainingFolderTooltip = "Ctrl + Enter to open the containing folder";
    public const string FileResult_OpenWith = "Open With";
    public const string FileResult_OpenWith_Subtitle = "Select a program to open with";

    // Settings
    public const string Settings_General = "General Settings";
    public const string Settings_General_ShellPath = "Shell Path";
    public const string Settings_General_UseLocationAsWorkingDir = "Use search result's location as the working directory of the executable";
    public const string Settings_General_ExcludedFileTypes = "Excluded File Types (comma seperated)";
    public const string Settings_General_ExcludedFileTypes_Tooltip = "Example: exe,jpg,png";
    public const string Settings_General_MaxResults = "Maximum number of results";
    public const string Settings_Preview = "Preview Panel";
    public const string Settings_Preview_Size = "Size";
    public const string Settings_Preview_DateCreated = "Date Created";
    public const string Settings_Preview_DateModified = "Date Modified";
    public const string Settings_Preview_FileAge = "File Age";
    public const string Settings_Preview_DisplayFileInfo = "Display File Info";
    public const string Settings_Preview_DateAndTimeFormat = "Date and time format";

    // Preview
    public const string Preview_UnknownValue = "Unknown";
    public const string Preview_Today = "Today";
    public static string Preview_DaysAgo(int days) => $"{days} days ago";
    public const string Preview_OneMonthAgo = "1 month ago";
    public static string Preview_MonthsAgo(int months) => $"{months} months ago";
    public const string Preview_OneYearAgo = "1 year ago";
    public static string Preview_YearsAgo(int years) => $"{years} years ago";
}
