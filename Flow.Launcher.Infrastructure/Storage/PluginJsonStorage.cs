using System;
using System.IO;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Plugins.Interfaces;
using Flow.Launcher.Infrastructure.UserSettings;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Flow.Launcher.Infrastructure.Storage;

// Expose ISaveable interface in derived class to make sure we are calling the new version of Save method
public class PluginJsonStorage<T> : JsonStorage<T>, ISavable where T : new()
{
    // Use assembly name to check which plugin is using this storage
    public readonly string AssemblyName;

    private static readonly ILogger<PluginJsonStorage<T>> Logger = LogManager.GetLogger<PluginJsonStorage<T>>();

    public PluginJsonStorage()
    {
        // C# related, add python related below
        var dataType = typeof(T);
        AssemblyName = dataType.Assembly.GetName().Name ?? throw new NullReferenceException("Plugin's assembly name was null");
        DirectoryPath = Path.Combine(DataLocation.PluginSettingsDirectory, AssemblyName);
        if (!Directory.Exists(DirectoryPath))
            Directory.CreateDirectory(DirectoryPath);

        FilePath = Path.Combine(DirectoryPath, $"{dataType.Name}{FileSuffix}");
    }

    public PluginJsonStorage(T data) : this()
    {
        Data = data;
    }

    public new void Save()
    {
        try
        {
            base.Save();
        }
        catch (Exception e)
        {
            Logger.ZLogError(e, $"Failed to save plugin settings to path: {FilePath}");
        }
    }

    public new async Task SaveAsync()
    {
        try
        {
            await base.SaveAsync();
        }
        catch (Exception e)
        {
            Logger.ZLogError(e, $"Failed to save plugin settings to path: {FilePath}");
        }
    }
}
