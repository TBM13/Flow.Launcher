namespace Flow.Launcher.Plugin.ProcessKiller;

public static class Localize
{
    // Actions
    public const string Action_KillAllInstances = "Kill all instances";
    public static string Action_KillAll(int count) => $"Kill {count} processes";
    public static string Action_KillAllInstancesOf(string processName) => $"Kill all instances of \"{processName}\"";

    // Settings
    public const string Settings_ShowWindowTitle = "Show title for processes with visible windows";
    public const string Settings_PutVisibleWindowsOnTop = "Put processes with visible windows on top of the list";
}
