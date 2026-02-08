using Flow.Launcher.Infrastructure.Plugins;
using Flow.Launcher.Infrastructure.Results;
using System.Collections.Generic;
using System.Threading;

namespace Flow.Launcher.ViewModel
{
    public record struct ResultsForUpdate(
        IReadOnlyList<Result> Results,
        PluginMetadata Metadata,
        Query Query,
        CancellationToken Token,
        bool ReSelectFirstResult = true,
        bool ShouldClearExistingResults = false)
    {
        public string ID { get; } = Metadata.ID;
    }
}
