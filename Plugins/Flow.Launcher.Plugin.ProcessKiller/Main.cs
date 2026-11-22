using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using Flow.Launcher.Plugin.ProcessKiller.Settings;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;

namespace Flow.Launcher.Plugin.ProcessKiller;

public static class PluginMetadataDefinition
{
    public static readonly PluginMetadata Metadata = new()
    {
        ID = "b64d0a79-329a-48b0-b53f-d658318a1bf6",
        ActionKeywords = ["kill"],
        Name = "Process Killer",
        Description = "Kill running processes from Flow",
        Author = "Flow-Launcher",
        Version = "1.0.0",
        IcoPath = "Images/Plugin.ProcessKiller.png",

        Plugin = new Main()
    };
}

public class Main : IPlugin, IContextMenu, ISettingProvider
{
    private Settings.Settings _settings = null!;
    private SettingsViewModel _viewModel = null!;

    public static PluginInitContext Context { get; private set; } = null!;

    public ValueTask InitAsync(PluginInitContext context)
    {
        Context = context;
        _settings = context.API.LoadSettingJsonStorage<Settings.Settings>();
        _viewModel = new SettingsViewModel(_settings);
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<Result> QueryAsync(
        Query query,
        [EnumeratorCancellation] CancellationToken token)
    {
        // We do not participate on the home query
        if (query.IsHomeQuery)
            yield break;

        List<ProcessInfo>? killableProcesses = ProcessUtils.GetKillableProcesses(_settings);
        if (killableProcesses is null)
            yield break;

        string searchTerm = query.Search;
        bool hasSearchTerm = !string.IsNullOrWhiteSpace(searchTerm);
        int matchedCount = 0;
        Result? firstResult = null;
        bool allSamePath = true;

        foreach (ProcessInfo pr in killableProcesses)
        {
            token.ThrowIfCancellationRequested();

            Process p = pr.Process;
            string processNameIdTitle = p.ProcessName + " - " + p.Id;

            int score = 0;
            if (hasSearchTerm)
            {
                MatchResult match = Context.API.StringMatcher.FuzzySearchBest(
                    searchTerm, pr.WindowTitle, pr.Path, processNameIdTitle);

                if (!match.IsThresholdMet)
                    continue;

                score = match.Score;
            }

            // Add score to prioritize processes with visible windows
            if (_settings.PutVisibleWindowProcessesTop && pr.AnyWindowVisible)
                score += 200;

            Result result = new()
            {
                IconOrGlyph = pr.Path,
                Title = _settings.ShowWindowTitle && pr.WindowTitle is not null
                    ? pr.WindowTitle : processNameIdTitle,
                ToolTip = $"{processNameIdTitle}\n\n{pr.Path}",
                SubTitle = pr.Path,
                Score = score,
                ContextData = pr.Path,
                AutocompleteText = (true, p.ProcessName),
                Action = _ =>
                {
                    ProcessUtils.TryKill(p);
                    // Re-query to refresh process list
                    Context.API.ReQuery();
                    return true;
                }
            };

            if (firstResult is null)
                firstResult = result;
            else if (allSamePath && !string.Equals(firstResult.SubTitle, result.SubTitle, StringComparison.Ordinal))
                allSamePath = false;

            matchedCount++;
            yield return result;
        }

        // If all results have the same process path and they are ALL the instances
        // of that process, add a result to kill all of them at once
        if (matchedCount > 1 && hasSearchTerm && allSamePath && firstResult is not null)
        {
            List<ProcessInfo> processesWithSamePath =
                [.. killableProcesses.Where(
                    pr => pr.Path.Equals(firstResult.SubTitle, StringComparison.OrdinalIgnoreCase))];

            if (processesWithSamePath.Count == matchedCount)
            {
                string processName = processesWithSamePath[0].Process.ProcessName;
                yield return new Result
                {
                    IconOrGlyph = firstResult.IconOrGlyph,
                    Title = $"Kill all instances of \"{processName}\"",
                    SubTitle = $"Kill {processesWithSamePath.Count} processes",
                    Score = 2000,
                    Action = _ =>
                    {
                        foreach (ProcessInfo p in processesWithSamePath)
                            ProcessUtils.TryKill(p.Process);

                        // Re-query to refresh process list
                        Context.API.ReQuery();
                        return true;
                    }
                };
            }
        }
    }

    public async IAsyncEnumerable<Result> LoadContextMenusAsync(
        Result selectedResult,
        ActionContext context,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        if (selectedResult.ContextData is not string processPath)
            yield break;

        // get all non-system processes whose file path matches that of the given result (processPath)
        List<Process> processes = [.. ProcessUtils.GetProcessesWithPath(processPath)];
        if (processes.Count == 0)
            yield break;

        yield return new Result
        {
            Title = $"Kill all instances ({processes.Count})",
            SubTitle = $"Kill all instances of {processPath}",
            Action = _ =>
            {
                foreach (Process p in processes)
                    ProcessUtils.TryKill(p);

                return true;
            },
            IconOrGlyph = processPath
        };
    }

    public Control CreateSettingPanel()
    {
        return new SettingsControl(_viewModel);
    }
}
