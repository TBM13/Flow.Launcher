using System.IO;
using Flow.Launcher.Infrastructure.Plugins.Interfaces;

namespace Flow.Launcher.Infrastructure.Storage;

// TODO: Fix logging

// Expose ISaveable interface in derived class to make sure we are calling the new version of Save method
public class PluginBinaryStorage<T> : BinaryStorage<T>, ISavable where T : new()
{
    public PluginBinaryStorage(string cacheName, string cacheDirectory)
    {
        DirectoryPath = cacheDirectory;
        if (!Directory.Exists(DirectoryPath))
            Directory.CreateDirectory(DirectoryPath);

        FilePath = Path.Combine(DirectoryPath, $"{cacheName}{FileSuffix}");
    }

    public new void Save()
    {
        try
        {
            base.Save();
        }
        catch (Exception e)
        {
            // Logger.ZLogError(e, $"Failed to save plugin caches to path: {FilePath}");
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
            // Logger.ZLogError(e, $"Failed to save plugin caches to path: {FilePath}");
        }
    }
}
