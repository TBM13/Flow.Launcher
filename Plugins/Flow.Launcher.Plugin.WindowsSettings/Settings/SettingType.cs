namespace Flow.Launcher.Plugin.WindowsSettings.Settings;

public enum SettingType
{
    /// <summary>
    /// The command of the setting is a URI that launches Windows' modern settings app.
    /// </summary>
    SettingsApp,
    /// <summary>
    /// The command of the setting is an executable located in the System32 directory.
    /// </summary>
    System32Exe,
}
