using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Plugins;

namespace Flow.Launcher.Plugin.Program.Programs;

public interface IProgram
{
    string Name { get; }
    string UniqueIdentifier { get; }
    string Location { get; }
    Result? CreateResult(ReadOnlySpan<char> query, Settings.Settings settings, PluginInitContext context);
    List<Result> CreateContextMenus(PluginInitContext context);
}
