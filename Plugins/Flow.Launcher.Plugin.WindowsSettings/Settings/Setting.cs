using Flow.Launcher.Interop.Programs;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Plugins;

namespace Flow.Launcher.Plugin.WindowsSettings.Settings;

public record Setting
{
    public required SettingType Type { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<string> Command { get; init; }

    public string? Glyph { get; init; }
    public IReadOnlyList<string> AlternativeNames { get; init; } = [];

    public Result ToResult(PluginInitContext ctx)
    {
        string? iconPath = null;
        string[] command = [.. Command];
        if (Type == SettingType.System32Exe)
        {
            // Ensure the executable is launched from system32
            command[0] = Path.Combine(Environment.SystemDirectory, command[0]);

            iconPath = command[0];
        }
        else
            iconPath = PluginMetadataDefinition.Metadata.IcoPath;

        string tooltip = $"{Name}\n{string.Join(" ", Command)}";
        return new()
        {
            Title = Name,
            IconOrGlyph = Glyph ?? iconPath,

            ToolTip = tooltip,
            CopyText = string.Join(" ", Command),

            Action = _ =>
            {
                try
                {
                    ProcessHelper.StartProcess(
                        command[0],
                        arguments: command[1..],
                        // Use shell execute even for executables to avoid access denied errors
                        useShellExecute: true);

                    return true;
                }
                catch (Exception e)
                {
                    ctx.Logger.LogError(e, $"Failed to start process: {string.Join(" ", command)}");
                    ctx.API.ShowMsgError("Failed to open setting", e.Message);
                    return false;
                }
            }
        };
    }
}
