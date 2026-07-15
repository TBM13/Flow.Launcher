using MemoryPack;
using Microsoft.Extensions.Logging;

namespace Flow.Launcher.Infrastructure.Storage;

/// <summary>
/// Serialize and deserialize an object to and from a binary file.
/// </summary>
/// <remarks>
/// The object must be MemoryPackSerializable (see <see href="https://github.com/Cysharp/MemoryPack"/>).
/// </remarks>
public class BinaryStorage<T>(ILoggerFactory loggerFactory, string filePath)
    : Storage<T>(loggerFactory, filePath, false) where T : class, new()
{
    protected override T? Deserialize(FileStream stream)
    {
        byte[] data = new byte[stream.Length];
        stream.ReadExactly(data, 0, data.Length);
        return MemoryPackSerializer.Deserialize<T>(data);
    }

    protected override async Task<T?> DeserializeAsync(FileStream stream)
    {
        return await MemoryPackSerializer.DeserializeAsync<T>(stream);
    }

    protected override void Serialize(Stream stream, T data)
    {
        byte[] bytes = MemoryPackSerializer.Serialize(data);
        stream.Write(bytes, 0, bytes.Length);
    }
}
