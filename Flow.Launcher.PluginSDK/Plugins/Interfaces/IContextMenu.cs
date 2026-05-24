using Flow.Launcher.Infrastructure.Results;

namespace Flow.Launcher.Infrastructure.Plugins.Interfaces;

/// <summary>
/// Adds support for presenting additional options for a given <see cref="Result"/> from a context menu.
/// </summary>
public interface IContextMenu
{
    /// <summary>
    /// Load context menu items for the given result.
    /// </summary>
    /// <param name="selectedResult">
    /// The <see cref="Result"/> for which the user has activated the context menu.
    /// </param>
    List<Result> LoadContextMenus(Result selectedResult);
}
