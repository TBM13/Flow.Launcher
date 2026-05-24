using System.IO;
using Flow.Launcher.Infrastructure.Plugins.Interfaces;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Infrastructure.Storage;

// TODO: Fix logging

// Expose ISaveable interface in derived class to make sure we are calling the new version of Save method
public class FlowLauncherJsonStorage<T> : JsonStorage<T>, ISavable where T : new()
{
    public FlowLauncherJsonStorage()
    {
        DirectoryPath = Path.Combine(DataLocation.DataDirectory, DirectoryName);
        if (!Directory.Exists(DirectoryPath))
            Directory.CreateDirectory(DirectoryPath);

        var filename = typeof(T).Name;
        FilePath = Path.Combine(DirectoryPath, $"{filename}{FileSuffix}");
    }

    public new void Save()
    {
        try
        {
            base.Save();
        }
        catch (Exception e)
        {
            // Logger.ZLogError(e, $"Failed to save FL settings to path: {FilePath}");
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
            // Logger.ZLogError(e, $"Failed to save FL settings to path: {FilePath}");
        }
    }
}
