namespace Flow.Launcher.PluginSDK.Plugins.Interfaces;

public interface IAsyncPlugin
{
    /// <summary>
    /// Called when a query or home query is performed.
    /// </summary>
    /// <returns>The list of results that should be displayed for this query.</returns>
    Task<List<Result>?> QueryAsync(Query query, CancellationToken token);

    /// <summary>
    /// Initialize plugin asynchrously (will still wait finish to continue)
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task InitAsync(PluginInitContext context);

    /// <summary>
    /// Called when the plugin is being disposed (app is shutting down or the plugin was disabled).
    /// </summary>
    /// <remarks>This is called even if the plugin failed to initialize.</remarks>
    ValueTask DisposeAsync();
}

