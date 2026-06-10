namespace Flow.Launcher.Plugin.WindowsServices;

public static class Localize
{
    // Info
    public const string Info_Name = "Name";
    public const string Info_Status = "Status";
    public const string Info_Status_StartPending = "Starting";
    public const string Info_Status_Running = "Running";
    public const string Info_Status_StopPending = "Stopping";
    public const string Info_Status_Stopped = "Stopped";
    public const string Info_Status_PausePending = "Pausing";
    public const string Info_Status_Paused = "Paused";
    public const string Info_Status_ContinuePending = "Resuming";
    public const string Info_StartupType = "Startup Type";
    public const string Info_StartupType_Boot = "Boot";
    public const string Info_StartupType_System = "System";
    public const string Info_StartupType_Automatic = "Automatic";
    public const string Info_StartupType_AutomaticDelayed = "Automatic (Delayed Start)";
    public const string Info_StartupType_Manual = "Manual";
    public const string Info_StartupType_Disabled = "Disabled";

    // Actions
    public const string Action_StartService = "Start";
    public const string Action_StopService = "Stop";
    public const string Action_RestartService = "Restart";
    public const string Action_Disable = "Disable";
    public const string Action_Disable_Description = "Set startup type to disabled";
    public const string Action_DisableAndStop_Description = "Set startup type to disabled & stop the service";
    public const string Action_EnableManual = "Enable (manual)";
    public const string Action_EnableManual_Description = "Set startup type to manual";
    public const string Action_EnableManualAndStart_Description = "Set startup type to manual & start the service";
    public const string Action_EnableAutomatic = "Enable (automatic)";
    public const string Action_EnableAutomatic_Description = "Set startup type to automatic";
    public const string Action_EnableAutomaticAndStart_Description = "Set startup type to automatic & start the service";
    public const string Action_EnableAutomaticDelayed = "Enable (automatic delayed)";
    public const string Action_EnableAutomaticDelayed_Description = "Set startup type to automatic delayed";
    public const string Action_EnableAutomaticDelayedAndStart_Description = "Set startup type to automatic delayed & start the service";

    // Errors
    public static string Error_GetInformationFail(int count) => $"Failed to get information from {count} service(s)";
    public const string Error_StartServiceFail = "Failed to start service";
    public const string Error_StopServiceFail = "Failed to stop service";
    public const string Error_RestartServiceFail = "Failed to restart service";
    public const string Error_ChangeStartupTypeFail = "Failed to change startup type";
    public const string Error_NoDescription = "The service does not have a description.";
    public const string Error_GetPathFail = "Failed to get the service's path";
}
