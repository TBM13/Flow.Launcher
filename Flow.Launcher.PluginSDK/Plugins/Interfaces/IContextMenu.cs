namespace Flow.Launcher.PluginSDK.Plugins.Interfaces;

/// <summary>
/// Adds support for presenting additional options for a given <see cref="Result"/> from a context menu.
/// </summary>
public interface IContextMenu
{
    /// <summary>
    /// Gets the context menu items for the given result.
    /// </summary>
    IAsyncEnumerable<Result> LoadContextMenusAsync(
        Result selectedResult,
        ActionContext context,
        CancellationToken token = default);
}
