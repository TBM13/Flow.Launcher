namespace Flow.Launcher.Infrastructure.Plugins.Interfaces;

/// <summary>
/// Implemented by plugins that can generate a "magic query"
/// when Flow Launcher is opened using the "Magic Query" hotkey.
/// </summary>
public interface IMagicQueryProvider
{
    /// <returns>A query that Flow Launcher will open to, or null to let another plugin generate it.</returns>
    string? GenerateMagicQuery();
}
