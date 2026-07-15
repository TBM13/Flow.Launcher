using System;
using System.IO;

namespace Flow.Launcher.Infrastructure.UserSettings;

public static class DataLocation
{
    public static readonly string RoamingDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlowLauncher");
    public static string DataDirectory => RoamingDataPath;

    public static readonly string CacheDirectory = Path.Combine(DataDirectory, Constant.Cache);
    public static readonly string SettingsDirectory = Path.Combine(DataDirectory, Constant.Settings);
    public static readonly string PluginsDirectory = Path.Combine(DataDirectory, Constant.Plugins);

    public static readonly string PluginSettingsDirectory = Path.Combine(SettingsDirectory, Constant.Plugins);
    public static readonly string PluginCacheDirectory = Path.Combine(DataDirectory, Constant.Cache, Constant.Plugins);
}
