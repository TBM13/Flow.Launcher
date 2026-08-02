namespace Flow.Launcher.PluginSDK.Plugins.Interfaces;

public interface IPlugin : IAsyncPlugin
{
    /// <summary>
    /// Called when a query or home query is performed.
    /// </summary>
    /// <returns>The list of results that should be displayed for this query.</returns>
    List<Result>? Query(Query query);

    /// <summary>
    /// Initialize plugin
    /// </summary>
    /// <param name="context"></param>
    void Init(PluginInitContext context);

    Task IAsyncPlugin.InitAsync(PluginInitContext context) => Task.Run(() => Init(context));

    Task<List<Result>?> IAsyncPlugin.QueryAsync(Query query, CancellationToken token) => Task.Run(() => Query(query));
}
