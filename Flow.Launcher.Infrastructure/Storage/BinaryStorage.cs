using System;
using System.IO;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Plugins.Interfaces;
using Flow.Launcher.Infrastructure.UserSettings;
using MemoryPack;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Flow.Launcher.Infrastructure.Storage;

/// <summary>
/// Storage object using binary data
/// Normally, it has better performance, but not readable
/// </summary>
/// <remarks>
/// It utilizes MemoryPack, which means the object must be MemoryPackSerializable <see href="https://github.com/Cysharp/MemoryPack"/>
/// </remarks>
public class BinaryStorage<T> : ISavable
{
    private static readonly ILogger<BinaryStorage<T>> Logger = LogManager.GetLogger<BinaryStorage<T>>();

    protected T? Data;

    public const string FileSuffix = ".cache";

    protected string FilePath { get; init; } = null!;

    protected string DirectoryPath { get; init; } = null!;

    // Let the derived class to set the file path
    protected BinaryStorage()
    {
    }

    public BinaryStorage(string filename)
    {
        DirectoryPath = DataLocation.CacheDirectory;
        if (!Directory.Exists(DirectoryPath))
            Directory.CreateDirectory(DirectoryPath);

        FilePath = Path.Combine(DirectoryPath, $"{filename}{FileSuffix}");
    }

    public T TryLoad(T defaultData)
    {
        if (Data != null) return Data;

        if (File.Exists(FilePath))
        {
            if (new FileInfo(FilePath).Length == 0)
            {
                Logger.ZLogError($"Zero length cache file <{FilePath}>");
                Data = defaultData;
                Save();
            }

            var bytes = File.ReadAllBytes(FilePath);
            Data = Deserialize(bytes, defaultData);
        }
        else
        {
            Logger.ZLogInformation($"Cache file not exist, load default data");
            Data = defaultData;
            Save();
        }
        return Data;
    }

    private T Deserialize(ReadOnlySpan<byte> bytes, T defaultData)
    {
        try
        {
            var t = MemoryPackSerializer.Deserialize<T>(bytes);
            return t ?? defaultData;
        }
        catch (Exception e)
        {
            Logger.ZLogError(e, $"Deserialize error for file <{FilePath}>");
            return defaultData;
        }
    }

    public async ValueTask<T> TryLoadAsync(T defaultData)
    {
        if (Data != null) return Data;

        if (File.Exists(FilePath))
        {
            if (new FileInfo(FilePath).Length == 0)
            {
                Logger.ZLogError($"Zero length cache file <{FilePath}>");
                Data = defaultData;
                await SaveAsync();
            }

            await using var stream = new FileStream(FilePath, FileMode.Open);
            Data = await DeserializeAsync(stream, defaultData);
        }
        else
        {
            Logger.ZLogInformation($"Cache file not exist, load default data");
            Data = defaultData;
            await SaveAsync();
        }

        return Data;
    }

    private async ValueTask<T> DeserializeAsync(Stream stream, T defaultData)
    {
        try
        {
            var t = await MemoryPackSerializer.DeserializeAsync<T>(stream);
            return t ?? defaultData;
        }
        catch (Exception e)
        {
            Logger.ZLogError(e, $"Deserialize error for file <{FilePath}>");
            return defaultData;
        }
    }

    public void Save()
    {
        Save(Data ?? throw new NullReferenceException());
    }

    public void Save(T data)
    {
        // User may delete the directory, so we need to check it
        if (!Directory.Exists(DirectoryPath))
            Directory.CreateDirectory(DirectoryPath);

        var serialized = MemoryPackSerializer.Serialize(data);
        File.WriteAllBytes(FilePath, serialized);
    }

    public async ValueTask SaveAsync()
    {
        await SaveAsync(Data ?? throw new NullReferenceException());
    }

    public async ValueTask SaveAsync(T data)
    {
        // User may delete the directory, so we need to check it
        if (!Directory.Exists(DirectoryPath))
            Directory.CreateDirectory(DirectoryPath);

        await using var stream = new FileStream(FilePath, FileMode.Create);
        await MemoryPackSerializer.SerializeAsync(stream, data);
    }

    // ImageCache need to convert data into concurrent dictionary for usage,
    // so we would better to clear the data
    public void ClearData()
    {
        Data = default;
    }
}
