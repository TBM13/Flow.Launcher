using Flow.Launcher.Infrastructure.API;
using Flow.Launcher.PluginSDK.Logging;

namespace Flow.Launcher.Infrastructure.Plugins;

/// <summary>
/// Carries data passed to a plugin when it gets initialized.
/// </summary>
public record PluginInitContext
{
    public required PluginMetadata CurrentPluginMetadata { get; init; }
    public required IPublicAPI API { get; init; }
    public required Logger Logger { get; init; }
}
