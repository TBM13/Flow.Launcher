using Flow.Launcher.Infrastructure.Storage;

namespace Flow.Launcher.Core.Settings;

public static class SettingsFactory
{
    /// <summary>
    /// Loads the settings from its JSON file.
    /// </summary>
    public static ISettingsAPI LoadSettings()
    {
        FlowLauncherJsonStorage<Settings> storage = new();

        Settings settings = storage.Load();
        settings.SetStorage(storage);
        return settings;
    }
}
