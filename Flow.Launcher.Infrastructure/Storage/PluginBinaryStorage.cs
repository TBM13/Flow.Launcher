using System.IO;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logging;
using Flow.Launcher.Infrastructure.Plugins.Interfaces;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Flow.Launcher.Infrastructure.Storage;

// Expose ISaveable interface in derived class to make sure we are calling the new version of Save method
public class PluginBinaryStorage<T> : BinaryStorage<T>, ISavable where T : new()
{
    private static readonly ILogger<PluginBinaryStorage<T>> Logger = LogManager.GetLogger<PluginBinaryStorage<T>>();

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
        catch (System.Exception e)
        {
            Logger.ZLogError(e, $"Failed to save plugin caches to path: {FilePath}");
        }
    }

    public new async Task SaveAsync()
    {
        try
        {
            await base.SaveAsync();
        }
        catch (System.Exception e)
        {
            Logger.ZLogError(e, $"Failed to save plugin caches to path: {FilePath}");
        }
    }
}
