using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;
using Microsoft.Win32.TaskScheduler;
using Task = Microsoft.Win32.TaskScheduler.Task;

namespace Flow.Launcher.Plugin.WindowsTasks;

public static class PluginMetadataDefinition
{
    public static readonly PluginMetadata Metadata = new()
    {
        ID = "239acbf0-8488-44ef-9a98-43025a3886ca",
        ActionKeywords = ["tsk"],
        Name = "Windows Tasks",
        Description = "Manage Windows tasks from Flow Launcher",
        Author = "TBM13",
        Version = "1.0.0",
        IcoPath = "Images/Plugin.WindowsTasks.png",

        Plugin = new Main()
    };
}

public class Main : IPlugin, IContextMenu
{
    public static readonly string PLUGIN_ICON = PluginMetadataDefinition.Metadata.IcoPath;
    public const string TASK_DISABLED_ICON = "Images\\Plugin.WindowsTasks.TaskDisabled.png";

    private PluginInitContext _context = null!;

    public ValueTask InitAsync(PluginInitContext context)
    {
        _context = context;
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<Result> QueryAsync(
        Query query,
        [EnumeratorCancellation] CancellationToken token)
    {
        if (query.IsHomeQuery)
            yield break;

        string search = query.Search.Replace('/', '\\');

        // TODO: Support global searches
        // TODO: Support recursive and wildcard searching
        // TODO: Figure out how to edit tasks since it requires admin privileges

        // On empty queries we should show all the folders and tasks in the root folder
        if (string.IsNullOrWhiteSpace(search))
        {
            TaskFolder root = TaskService.Instance.RootFolder;

            foreach (TaskFolder subFolder in root.EnumerateFolders())
            {
                token.ThrowIfCancellationRequested();
                yield return CreateResult(subFolder, addSelectedCount: false);
            }

            foreach (Task task in root.EnumerateTasks())
            {
                token.ThrowIfCancellationRequested();
                yield return CreateResult(task, addSelectedCount: false);
            }

            yield break;
        }

        // If the query is the path of a folder, lets show all its subfolders and tasks
        if (search.EndsWith('\\'))
        {
            TaskFolder? folder = TaskService.Instance.GetFolder(search[..^1]);
            if (folder is null)
                yield break;

            foreach (TaskFolder subFolder in folder.EnumerateFolders())
            {
                token.ThrowIfCancellationRequested();
                yield return CreateResult(subFolder, addSelectedCount: false);
            }

            foreach (Task task in folder.EnumerateTasks())
            {
                token.ThrowIfCancellationRequested();
                yield return CreateResult(task, addSelectedCount: false);
            }

            yield break;
        }

        // Lets treat this query as a simple StartsWith search inside a folder or root folder
        TaskFolder? searchFolder;
        string mustStartWith = search;

        int separatorIndex = search.LastIndexOf('\\');
        if (separatorIndex == -1)
        {
            searchFolder = TaskService.Instance.RootFolder;
        }
        else
        {
            string folderPath = search[..separatorIndex];
            searchFolder = TaskService.Instance.GetFolder(folderPath);
            if (searchFolder is null)
                yield break;

            mustStartWith = search[(separatorIndex + 1)..];
        }

        foreach (TaskFolder subFolder in searchFolder.EnumerateFolders())
        {
            token.ThrowIfCancellationRequested();
            if (subFolder.Name.StartsWith(mustStartWith, StringComparison.CurrentCultureIgnoreCase))
                yield return CreateResult(subFolder, addSelectedCount: true);
        }

        foreach (Task task in searchFolder.EnumerateTasks())
        {
            token.ThrowIfCancellationRequested();
            if (task.Name.StartsWith(mustStartWith, StringComparison.CurrentCultureIgnoreCase))
                yield return CreateResult(task, addSelectedCount: true);
        }
    }

    public async IAsyncEnumerable<Result> LoadContextMenusAsync(
        Result selectedResult,
        ActionContext context,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        if (selectedResult.ContextData is Task task)
        {
            if (task.Enabled)
            {
                yield return new Result
                {
                    Title = "Disable",
                    IconOrGlyph = "\xEB4A",
                    Action = _ => SetTaskEnabled(task.Path, enabled: false)
                };
            }
            else
            {
                yield return new Result
                {
                    Title = "Enable",
                    IconOrGlyph = "\xEB49",
                    Action = _ => SetTaskEnabled(task.Path, enabled: true)
                };
            }
        }
    }

    private static Result CreateResult(TaskFolder folder, bool addSelectedCount)
    {
        return new Result
        {
            Title = folder.Name,
            AutocompleteText = (true, folder.Path + '\\'),
            IconOrGlyph = "\uF12B",
            ContextData = folder,
            CopyText = folder.Path,
            AddSelectedCount = addSelectedCount,
            Action = c =>
            {
                // Context.API.ChangeQuery(navigateQuery);
                return false;
            }
        };
    }

    private static Result CreateResult(Task task, bool addSelectedCount)
    {
        return new Result
        {
            Title = task.Name,
            SubTitle = GetLocalizedSubtitle(task),
            AutocompleteText = (true, task.Path),
            IconOrGlyph = task.Enabled ? PLUGIN_ICON : TASK_DISABLED_ICON,
            ContextData = task,
            CopyText = task.Path,
            AddSelectedCount = addSelectedCount
        };
    }

    private bool SetTaskEnabled(string taskPath, bool enabled)
    {
        string actionName = enabled ? "enable" : "disable";
        ProcessStartInfo startInfo = new()
        {
            FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add("/Change");
        startInfo.ArgumentList.Add("/TN");
        startInfo.ArgumentList.Add(taskPath);
        startInfo.ArgumentList.Add(enabled ? "/Enable" : "/Disable");

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                _context.API.ShowMsgError($"Failed to {actionName} task", "Failed to start schtasks process.");
                return false;
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                _context.Logger.LogError($"schtasks exited with code {process.ExitCode} while trying to {actionName} task '{taskPath}'");
                _context.API.ShowMsgError($"Failed to {actionName} task", $"schtasks returned exit code {process.ExitCode}.");
                return false;
            }
        }
        catch (Win32Exception ex)
        {
            // User cancelled the UAC prompt (ERROR_CANCELLED = 1223)
            if (ex.NativeErrorCode == 1223)
                return false;

            _context.Logger.LogError(ex, $"Failed to {actionName} task '{taskPath}'");
            _context.API.ShowMsgError($"Failed to {actionName} task", ex.Message);
            return false;
        }

        _context.API.ReQuery();
        return true;
    }

    private static string GetLocalizedSubtitle(Task task)
    {
        StringBuilder sb = new();
        string localizedState = task.State switch
        {
            TaskState.Disabled => "Disabled",
            TaskState.Queued => "Queued",
            TaskState.Ready => "Ready",
            TaskState.Running => "Running",
            TaskState.Unknown or _ => "Unknown",
        };
        sb.Append($"State: {localizedState}");

        // A 1999 date usually means the task was never run
        if (task.LastRunTime.Year >= 2000)
        {
            sb.Append(" - ");

            string lastRunTime = task.LastRunTime.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);
            sb.Append($"Last run: {lastRunTime}");
        }

        // A 0001 date usually means there is no next run time scheduled
        if (task.NextRunTime.Year != 1)
        {
            // It doesn't make sense to show the next run for disabled tasks
            if (task.Enabled)
            {
                sb.Append(" - ");

                string nextRunTime = task.NextRunTime.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);
                sb.Append($"Next run: {nextRunTime}");
            }
        }

        return sb.ToString();
    }
}
