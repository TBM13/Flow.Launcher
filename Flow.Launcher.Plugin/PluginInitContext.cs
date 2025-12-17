namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Carries data passed to a plugin when it gets initialized.
    /// </summary>
    public record PluginInitContext(PluginMetadata CurrentPluginMetadata, IPublicAPI API);
}
