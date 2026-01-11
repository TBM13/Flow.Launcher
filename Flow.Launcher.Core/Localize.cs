namespace Flow.Launcher.Core;

public static class Localize
{
    // Plugins
    public const string Plugins_FailToInit = "Fail to Init Plugins";
    public static string Plugins_FailToInit_Message(string failedList)
        => $"Plugins: {failedList} - fail to load and would be disabled, please contact plugin creator for help";
    public static string Plugin_StillInitializing(string name) => name + ": This plugin is still initializing...";
    public const string Plugin_StillInitializing_Subtitle = "Select this result to requery";
    public static string Plugin_FailedToRespond(string name) => name + ": Failed to respond!";
    public const string Plugin_FailedToRespond_Subtitle = "Select this result for more info";
}
