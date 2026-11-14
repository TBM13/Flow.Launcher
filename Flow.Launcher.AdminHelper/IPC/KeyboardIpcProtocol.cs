using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Launcher.Interop.Input;

namespace Flow.Launcher.AdminHelper.IPC;

public enum KeyboardIpcMessageType
{
    HotkeyTriggered,
    GetPressedKeys,
    PressedKeysResult,

    BlockWindowsKey,
    DontBlockWindowsKey
}

public sealed record KeyboardIpcMessage
{
    public KeyboardIpcMessageType Type { get; init; }
    public int RequestId { get; init; }
    public int MainKey { get; init; }
    public int Modifiers { get; init; }
    public ulong[]? PressedKeys { get; init; }
}

public static class KeyboardIpcProtocol
{
    public static string Serialize(KeyboardIpcMessage message) =>
        JsonSerializer.Serialize(message, KeyboardIpcJsonContext.Default.KeyboardIpcMessage);

    public static KeyboardIpcMessage? Deserialize(string json) =>
        JsonSerializer.Deserialize(json, KeyboardIpcJsonContext.Default.KeyboardIpcMessage);

    public static KeyboardIpcMessage CreatePressedKeysResult(int requestId, PressedKeys pressedKeys) => new()
    {
        Type = KeyboardIpcMessageType.PressedKeysResult,
        RequestId = requestId,
        PressedKeys = pressedKeys.ToBits()
    };

    public static PressedKeys GetPressedKeys(KeyboardIpcMessage message)
    {
        ulong[] bits = message.PressedKeys ?? throw new InvalidDataException("Pressed-key state is missing");
        if (bits.Length != 4)
            throw new InvalidDataException("Pressed-key state must contain four bitmap segments");

        return PressedKeys.FromBits(bits);
    }

    public static string GetCurrentUserPipeName()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        using Process process = Process.GetCurrentProcess();
        string sid = identity.User?.Value ?? Environment.UserName;
        return GetPipeName(process.SessionId, sid);
    }

    public static string GetPipeName(int sessionId, string userSid) =>
        $"Flow.Launcher.AdminHelper.{sessionId}.{userSid}";
}

[JsonSerializable(typeof(KeyboardIpcMessage))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
internal partial class KeyboardIpcJsonContext : JsonSerializerContext
{
}
