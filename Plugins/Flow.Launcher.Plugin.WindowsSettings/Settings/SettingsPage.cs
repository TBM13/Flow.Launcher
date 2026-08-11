namespace Flow.Launcher.Plugin.WindowsSettings.Settings;

public record SettingsPage : Setting
{
    public required IReadOnlyList<Setting> Settings { get; init; }

    public string LocalPath => Name + "/";
}
