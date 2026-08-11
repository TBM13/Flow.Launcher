using Flow.Launcher.Interop.Programs;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Plugins;

namespace Flow.Launcher.Plugin.WindowsSettings.Settings;

public record Setting
{
    public required SettingType Type { get; init; }
    public required string Name { get; init; }
    public required string Command { get; init; }

    public string? Glyph { get; init; }
    public IReadOnlyList<string> AlternativeNames { get; init; } = [];

    public Result ToResult(PluginInitContext ctx)
    {
        string? iconPath = null;
        string command = Command;
        if (Type == SettingType.System32Exe)
        {
            // Ensure the executable is launched from system32
            command = Path.Combine(Environment.SystemDirectory, Command);

            // Discard arguments (if any)
            int firstSpaceIndex = command.IndexOf(' ');
            iconPath = firstSpaceIndex != -1
                ? command[..firstSpaceIndex] : command;
        }
        else
            iconPath = PluginMetadataDefinition.Metadata.IcoPath;

        string tooltip = $"{Name}\n{Command}";
        return new()
        {
            Title = Name,
            IconOrGlyph = Glyph ?? iconPath,

            ToolTip = tooltip,
            CopyText = Command,

            Action = _ =>
            {
                try
                {
                    ProcessHelper.StartProcess(command);
                    return true;
                }
                catch (Exception e)
                {
                    ctx.Logger.LogError(e, $"Failed to start process: {command}");
                    ctx.API.ShowMsgError("Failed to open setting", e.Message);
                    return false;
                }
            }
        };
    }
}
