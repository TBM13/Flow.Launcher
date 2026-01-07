namespace Flow.Launcher.Plugin.WindowsTasks;

public static class Localize
{
    // Task States
    public static string TaskState(string state) => $"State: {state}";
    public const string TaskState_Disabled = "Disabled";
    public const string TaskState_Queued = "Queued";
    public const string TaskState_Ready = "Ready";
    public const string TaskState_Running = "Running";
    public const string TaskState_Unknown = "Unknown";

    // Task Run Times
    public static string LastRunTime(string time) => $"Last run: {time}";
    public static string NextRunTime(string time) => $"Next run: {time}";

    // Task Actions
    public const string TaskAction_Enable = "Enable";
    public const string TaskAction_Disable = "Disable";
}
