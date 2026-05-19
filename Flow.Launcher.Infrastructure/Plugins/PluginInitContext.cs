using Flow.Launcher.Infrastructure.API;

namespace Flow.Launcher.Infrastructure.Plugins;

/// <summary>
/// Carries data passed to a plugin when it gets initialized.
/// </summary>
public record PluginInitContext(PluginMetadata CurrentPluginMetadata, IPublicAPI API);
