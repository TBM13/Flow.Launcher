using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Flow.Launcher.Core.Storage;

/// <summary>
/// Serialize and deserialize an object to and from a JSON file.
/// </summary>
public class JsonStorage<T>(ILoggerFactory loggerFactory, string filePath)
    : Storage<T>(loggerFactory, filePath, true) where T : class, new()
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    protected override T? Deserialize(FileStream stream)
    {
        return JsonSerializer.Deserialize<T>(stream, SerializerOptions);
    }

    protected override async Task<T?> DeserializeAsync(FileStream stream)
    {
        return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions);
    }

    protected override void Serialize(Stream stream, T data)
    {
        JsonSerializer.Serialize(stream, data, SerializerOptions);
    }
}
