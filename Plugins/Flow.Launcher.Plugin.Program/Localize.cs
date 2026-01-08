namespace Flow.Launcher.Plugin.Program;

public static class Localize
{
    // Settings
    public const string Settings_ResetDefault = "Reset Default";
    public const string Settings_Delete = "Delete";
    public const string Settings_Add = "Add";
    public const string Settings_Edit = "Edit";
    public const string Settings_ProgramName = "Name";
    public const string Settings_ProgramEnable = "Enable";
    public const string Settings_ProgramEnabled = "Enabled";
    public const string Settings_ProgramDisable = "Disable";
    public const string Settings_ProgramDisabled = "Disabled";
    public const string Settings_ProgramStatus = "Status";
    public const string Settings_ProgramLocation = "Location";
    public const string Settings_AllPrograms = "All Programs";
    public const string Settings_ProgramSuffixes = "File Type";
    public const string Settings_Reindex = "Reindex";
    public const string Settings_Indexing = "Indexing";
    public const string Settings_IndexSources = "Index Sources";
    public const string Settings_IndexOptions = "Options";
    public const string Settings_IndexUwp = "UWP Apps";
    public const string Settings_IndexUwp_Tooltip = "When enabled, Flow will load UWP Applications";
    public const string Settings_IndexStart = "Start Menu";
    public const string Settings_IndexStart_Tooltip = "When enabled, Flow will load programs from the start menu";
    public const string Settings_IndexRegistry = "Registry";
    public const string Settings_IndexRegistry_Tooltip = "When enabled, Flow will load programs from the registry";
    public const string Settings_IndexPATH = "PATH";
    public const string Settings_IndexPATH_Tooltip = "When enabled, Flow will load programs from the PATH environment variable";
    public const string Settings_EnableHideLnkPath = "Hide app path";
    public const string Settings_EnableHideLnkPath_Tooltip = "For executable files such as UWP or lnk, hide the file path from being visible";
    public const string Settings_EnableHideUninstallers = "Hide uninstallers";
    public const string Settings_EnableHideUninstallers_Tooltip = "Hides programs with common uninstaller names, such as unins000.exe";
    public const string Settings_EnableDescription = "Search in Program Description";
    public const string Settings_EnableDescription_Tooltip = "Flow will search program's description";
    public const string Settings_EnableHideDuplicatedWindowsApp = "Hide duplicated apps";
    public const string Settings_EnableHideDuplicatedWindowsApp_Tooltip = "Hide duplicated Win32 programs that are already in the UWP list";
    public const string Settings_MaxDepthHeader = "Max Depth";
    public const string Settings_Directory = "Directory";
    public const string Settings_Browse = "Browse";
    public const string Settings_FileSuffixes = "File Suffixes:";
    public const string Settings_MaxSearchDepth = "Maximum Search Depth (-1 is unlimited):";
    public const string Settings_Update = "Update";
    public const string Settings_OnlyIndexTip = "Program Plugin will only index files with selected suffixes and .url files with selected protocols.";
    public const string Settings_UpdateFileSuffixes = "Successfully updated file suffixes";
    public const string Settings_SuffixesCannotEmpty = "File suffixes can't be empty";
    public const string Settings_ProtocolsCannotEmpty = "Protocols can't be empty";
    public const string Settings_SuffixesExecutableTypes = "File Suffixes";
    public const string Settings_SuffixesURLTypes = "URL Protocols";
    public const string Settings_SuffixesURLSteam = "Steam Games";
    public const string Settings_SuffixesURLEpic = "Epic Games";
    public const string Settings_SuffixesURLHttp = "Http/Https";
    public const string Settings_SuffixesCustomUrls = "Custom URL Protocols";
    public const string Settings_SuffixesCustomFileTypes = "Custom File Suffixes";
    public const string Settings_SuffixesTooltip = "Insert file suffixes you want to index. Suffixes should be separated by ';'. (ex>bat;py)";
    public const string Settings_ProtocolTooltip = "Insert protocols of .url files you want to index. Protocols should be separated by ';', and should end with \"://\". (ex>ftp://;mailto://)";

    // Program Source
    public const string ProgramSource_PleaseSelect = "Please select a program source";
    public const string ProgramSource_DeleteConfirm = "Are you sure you want to delete the selected program sources?";
    public const string ProgramSource_DeleteSelectNotUserAdded = "Please select program sources that are not added by you";
    public const string ProgramSource_DeleteSelectUserAdded = "Please select program sources that are added by you";
    public const string ProgramSource_Duplicate = "Another program source with the same location already exists.";
    public const string ProgramSource_EditTitle = "Program Source";
    public const string ProgramSource_EditTips = "Edit directory and status of this program source.";

    // Actions
    public const string Action_RunAsDifferentUser = "Run As Different User";
    public const string Action_RunAsAdministrator = "Run As Administrator";
    public const string Action_OpenContainingFolder = "Open containing folder";
    public const string Action_DisableProgram = "Hide";
    public const string Action_OpenTargetFolder = "Open target folder";

    // Errors
    public const string Error_Title = "Error";
    public static string Error_UnableToRun(string program) => $"Unable to run {program}";
    public const string Error_UnableToRunAsAdmin = "This app is not intended to be run as administrator";
    public const string Error_InvalidPath = "Invalid Path";

    // Dialogs
    public const string Dialog_DisableSuccess_Title = "Success";
    public const string Dialog_DisableSuccess_Message = "Successfully disabled this program from displaying in your query";
}
