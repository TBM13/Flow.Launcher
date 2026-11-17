using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Launcher.Interop.Input;

namespace Flow.Launcher.AdminHelper.IPC;

public enum AdminHelperMessageType
{
    HotkeyTriggered,
    GetPressedKeys,
    PressedKeysResult,

    BlockWindowsKey,
    DontBlockWindowsKey,

    RegisterScheduledTask,
    UnregisterScheduledTask
}

public sealed record AdminHelperMessage
{
    public AdminHelperMessageType Type { get; init; }
    public int RequestId { get; init; }
    public int MainKey { get; init; }
    public int Modifiers { get; init; }
    public ulong[]? PressedKeys { get; init; }
}

public static class AdminHelperProtocol
{
    public static string Serialize(AdminHelperMessage message) =>
        JsonSerializer.Serialize(message, AdminHelperJsonContext.Default.AdminHelperMessage);

    public static AdminHelperMessage? Deserialize(string json) =>
        JsonSerializer.Deserialize(json, AdminHelperJsonContext.Default.AdminHelperMessage);

    public static AdminHelperMessage CreatePressedKeysResult(int requestId, PressedKeys pressedKeys) => new()
    {
        Type = AdminHelperMessageType.PressedKeysResult,
        RequestId = requestId,
        PressedKeys = pressedKeys.ToBits()
    };

    public static PressedKeys GetPressedKeys(AdminHelperMessage message)
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

[JsonSerializable(typeof(AdminHelperMessage))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
internal partial class AdminHelperJsonContext : JsonSerializerContext
{
}
