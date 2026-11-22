namespace Flow.Launcher.PluginSDK.Plugins.Interfaces;

public interface IPlugin : IAsyncDisposable
{
    /// <summary>
    /// Initializes the plugin asynchronously.
    /// </summary>
    ValueTask InitAsync(PluginInitContext context);

    /// <summary>
    /// Called when a query or home query is performed.
    /// </summary>
    /// <returns>An async stream of results that should be displayed for this query.</returns>
    IAsyncEnumerable<Result> QueryAsync(Query query, CancellationToken token)
        => AsyncEnumerable.Empty<Result>();

    /// <summary>
    /// Called when the plugin is being disposed (app is shutting down or the plugin was disabled).
    /// </summary>
    /// <remarks>This is called even if the plugin failed to initialize.</remarks>
    ValueTask IAsyncDisposable.DisposeAsync() => ValueTask.CompletedTask;
}
