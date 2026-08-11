using System.Diagnostics;
using System.Windows.Controls;
using Flow.Launcher.Plugin.ProcessKiller.ViewModels;
using Flow.Launcher.Plugin.ProcessKiller.Views;
using Flow.Launcher.PluginSDK;
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
    private Settings _settings = null!;
    private SettingsViewModel _viewModel = null!;

    public static PluginInitContext Context { get; private set; } = null!;

    public void Init(PluginInitContext context)
    {
        Context = context;
        _settings = context.API.LoadSettingJsonStorage<Settings>();
        _viewModel = new SettingsViewModel(_settings);
    }

    public List<Result>? Query(Query query)
    {
        if (query.IsHomeQuery)
            return null;

        List<ProcessInfo>? killableProcesses = ProcessUtils.GetKillableProcesses(_settings);
        if (killableProcesses is null)
            return null;

        List<Result> results = [];
        string searchTerm = query.Search;
        foreach (ProcessInfo pr in killableProcesses)
        {
            Process p = pr.Process;
            string processNameIdTitle = p.ProcessName + " - " + p.Id;

            int score = 0;
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                // Get max score from searching window title, process name & ID and process path
                MatchResult windowTitleMatch = pr.WindowTitle is not null
                    ? Context.API.StringMatcher.FuzzyMatch(searchTerm, pr.WindowTitle) : default;
                MatchResult processPathMatch = Context.API.StringMatcher.FuzzyMatch(searchTerm, pr.Path);
                MatchResult processNameIdMatch = Context.API.StringMatcher.FuzzyMatch(searchTerm, processNameIdTitle);

                score = Math.Max(windowTitleMatch.Score, processNameIdMatch.Score);
                score = Math.Max(score, processPathMatch.Score);
                if (score <= 0)
                    continue;
            }

            // Add score to prioritize processes with visible windows
            if (_settings.PutVisibleWindowProcessesTop && pr.AnyWindowVisible)
                score += 200;

            results.Add(new Result()
            {
                IconOrGlyph = pr.Path,
                Title = _settings.ShowWindowTitle && pr.WindowTitle is not null
                    ? pr.WindowTitle : processNameIdTitle,
                ToolTip = $"{processNameIdTitle}\n\n{pr.Path}",
                SubTitle = pr.Path,
                Score = score,
                ContextData = pr.Path,
                AutocompleteText = (true, p.ProcessName),
                Action = c =>
                {
                    ProcessUtils.TryKill(p);
                    // Re-query to refresh process list
                    Context.API.ReQuery();
                    return true;
                }
            });
        }

        // If all results have the same process path and they are ALL the instances
        // of that process, add a result to kill all of them at once
        if (results.Count > 1 && !string.IsNullOrWhiteSpace(searchTerm)
            && results.All(r => r.SubTitle == results[0].SubTitle))
        {
            Result firstResult = results[0];
            List<ProcessInfo> processesWithSamePath =
                [.. killableProcesses.Where(
                    pr => pr.Path.Equals(firstResult.SubTitle, StringComparison.OrdinalIgnoreCase))];

            if (processesWithSamePath.Count == results.Count)
            {
                string processName = processesWithSamePath[0].Process.ProcessName;
                results.Add(new Result()
                {
                    IconOrGlyph = firstResult.IconOrGlyph,
                    Title = $"Kill all instances of \"{processName}\"",
                    SubTitle = $"Kill {processesWithSamePath.Count} processes",
                    Score = 2000,
                    Action = c =>
                    {
                        foreach (ProcessInfo p in processesWithSamePath)
                            ProcessUtils.TryKill(p.Process);

                        // Re-query to refresh process list
                        Context.API.ReQuery();
                        return true;
                    }
                });
            }
        }

        return results;
    }

    public List<Result>? LoadContextMenus(Result result)
    {
        string processPath = (string)result.ContextData!;

        // get all non-system processes whose file path matches that of the given result (processPath)
        List<Process> processes = [.. ProcessUtils.GetProcessesWithPath(processPath)];
        if (processes.Count == 0)
            return null;

        Result res = new()
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
        return [res];
    }

    public Control CreateSettingPanel()
    {
        return new SettingsControl(_viewModel);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
