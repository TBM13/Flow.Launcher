using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Microsoft.Extensions.Logging;

namespace Flow.Launcher.Core.Settings;

public static class SettingsFactory
{
    /// <summary>
    /// Loads the settings from its JSON file.
    /// </summary>
    public static ISettingsAPI LoadSettings(ILoggerFactory loggerFactory)
    {
        JsonStorage<Settings> storage = new(
            loggerFactory, Path.Combine(DataLocation.SettingsDirectory, "Settings.json"));

        Settings settings = storage.TryLoad();
        settings.SetStorage(storage);
        return settings;
    }
}
