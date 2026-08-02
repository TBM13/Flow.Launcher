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
}

