using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Win32.TaskScheduler;

namespace Flow.Launcher.Plugin.WindowsTasks;

public class Main : IPlugin, IContextMenu, IPluginI18n
{
    public const string PLUGIN_ICON = "Images\\app.png";
    public const string TASK_DISABLED_ICON = "Images\\task_disabled.png";
    public const string GLYPH_FONT = "/Resources/#Segoe Fluent Icons";

    internal static PluginInitContext Context { get; private set; } = null!;

    public string GetTranslatedPluginTitle() => Localize.plugin_windowstasks_plugin_name();
    public string GetTranslatedPluginDescription() => Localize.plugin_windowstasks_plugin_description();
    public void Init(PluginInitContext context)
    {
        Context = context;
    }

    public List<Result> Query(Query query)
    {
        query = query with { Search = query.Search.Replace('/', '\\') };

        // TODO: Support global searches
        // TODO: Support recursive and wildcard searching
        // TODO: Figure out how to edit tasks since it requires admin privileges

        // On empty queries we should show all the folders and tasks in the root folder
        if (string.IsNullOrWhiteSpace(query.Search))
        {
            TaskFolder folder = TaskService.Instance.RootFolder;

            IEnumerable<Result> folders = folder.EnumerateFolders().Select(f => CreateResult(query, f));
            IEnumerable<Result> tasks = folder.EnumerateTasks().Select(t => CreateResult(query, t));

            return [.. folders, .. tasks];
        }

        // If the query is the path of a folder, lets show all its subfolders and tasks
        if (query.Search.EndsWith('\\'))
        {
            TaskFolder? folder = TaskService.Instance.GetFolder(query.Search[..^1]);
            if (folder is null)
                return [];

            IEnumerable<Result> folders = folder.EnumerateFolders().Select(f => CreateResult(query, f));
            IEnumerable<Result> tasks = folder.EnumerateTasks().Select(t => CreateResult(query, t));

            return [.. folders, .. tasks];
        }

        // Lets treat this query as a simple StartsWith search inside a folder or root folder
        TaskFolder? searchFolder = null;
        string mustStartWith = query.Search;

        int separatorIndex = query.Search.LastIndexOf('\\');
        if (separatorIndex == -1)
            searchFolder = TaskService.Instance.RootFolder;
        else
        {
            string folderPath = query.Search[..separatorIndex];
            searchFolder = TaskService.Instance.GetFolder(folderPath);
            if (searchFolder is null)
                return [];

            mustStartWith = query.Search[(separatorIndex + 1)..];
        }

        IEnumerable<Result> matchedFolders = searchFolder
            .EnumerateFolders()
            .Where(f => f.Name.StartsWith(mustStartWith, StringComparison.CurrentCultureIgnoreCase))
            .Select(f => CreateResult(query, f));
        IEnumerable<Result> matchedTasks = searchFolder
            .EnumerateTasks()
            .Where(t => t.Name.StartsWith(mustStartWith, StringComparison.CurrentCultureIgnoreCase))
            .Select(t => CreateResult(query, t));

        return [.. matchedFolders, .. matchedTasks];
    }

    public List<Result> LoadContextMenus(Result result)
    {
        List<Result> res = [];
        if (result.ContextData is Task task)
        {
            if (task.Enabled)
            {
                res.Add(new()
                {
                    Title = Localize.plugin_windowstasks_taskAction_disable(),
                    Glyph = new GlyphInfo(FontFamily: GLYPH_FONT, Glyph: "\xEB4A"),
                    Action = c =>
                    {
                        task.Enabled = false;
                        return true;
                    }
                });
            }
            else
            {
                res.Add(new()
                {
                    Title = Localize.plugin_windowstasks_taskAction_enable(),
                    Glyph = new GlyphInfo(FontFamily: GLYPH_FONT, Glyph: "\xEB49"),
                    Action = c =>
                    {
                        task.Enabled = true;
                        return true;
                    }
                });
            }
        }

        return res;
    }

    private static Result CreateResult(Query query, TaskFolder folder)
    {
        string navigateQuery = AddActionKeyword(query, folder.Path + '\\');

        return new Result
        {
            Title = folder.Name,
            AutoCompleteText = navigateQuery,
            Glyph = new GlyphInfo(FontFamily: GLYPH_FONT, Glyph: "\uF12B"),
            IcoPath = PLUGIN_ICON,
            ContextData = folder,
            CopyText = folder.Path,
            Action = c =>
            {
                Context.API.ChangeQuery(navigateQuery);
                return false;
            }
        };
    }

    private static Result CreateResult(Query query, Task task)
    {
        return new Result
        {
            Title = task.Name,
            SubTitle = GetLocalizedSubtitle(task),
            AutoCompleteText = AddActionKeyword(query, task.Path),
            IcoPath = task.Enabled ? PLUGIN_ICON : TASK_DISABLED_ICON,
            ContextData = task,
            CopyText = task.Path,
            Action = c =>
            {
                Thread staThread = new Thread(() =>
                {
                    // Re-fetch task since we are on a different thread
                    Task refetchedTask = TaskService.Instance.GetTask(task.Path);

                    using TaskEditDialog dialog = new TaskEditDialog(refetchedTask);
                    dialog.ShowDialog();
                });

                // TaskEditDialog needs to be run in STA
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
                return true;
            }
        };
    }

    private static string AddActionKeyword(Query query, string s)
    {
        if (string.IsNullOrEmpty(query.ActionKeyword))
            return s;

        return $"{query.ActionKeyword} {s}";
    }

    private static string GetLocalizedSubtitle(Task task)
    {
        StringBuilder sb = new();
        string localizedState = task.State switch
        {
            TaskState.Disabled => Localize.plugin_windowstasks_taskState_disabled(),
            TaskState.Queued => Localize.plugin_windowstasks_taskState_queued(),
            TaskState.Ready => Localize.plugin_windowstasks_taskState_ready(),
            TaskState.Running => Localize.plugin_windowstasks_taskState_running(),
            TaskState.Unknown or _ => Localize.plugin_windowstasks_taskState_unknown(),
        };
        sb.Append(Localize.plugin_windowstasks_taskState(localizedState));

        // A 1999 date usually means the task was never run
        if (task.LastRunTime.Year >= 2000)
        {
            sb.Append(" - ");

            string lastRunTime = task.LastRunTime.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);
            sb.Append(Localize.plugin_windowstasks_lastRunTime(lastRunTime));
        }

        // A 0001 date usually means there is no next run time scheduled
        if (task.NextRunTime.Year != 1)
        {
            // It doesn't make sense to show the next run for disabled tasks
            if (task.Enabled)
            {
                sb.Append(" - ");

                string nextRunTime = task.NextRunTime.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);
                sb.Append(Localize.plugin_windowstasks_nextRunTime(nextRunTime));
            }
        }

        return sb.ToString();
    }
}
