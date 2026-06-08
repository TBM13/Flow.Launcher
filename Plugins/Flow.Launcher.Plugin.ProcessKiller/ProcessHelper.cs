using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using Flow.Launcher.Interop;

namespace Flow.Launcher.Plugin.ProcessKiller;

internal class ProcessHelper
{
    private readonly HashSet<string> _systemProcessList =
    [
        "conhost",
        "svchost",
        "idle",
        "system",
        "rundll32",
        "csrss",
        "lsass",
        "lsm",
        "smss",
        "wininit",
        "winlogon",
        "services",
        "spoolsv"
    ];

    private const string FlowLauncherProcessName = "Flow.Launcher";

    private bool IsSystemProcessOrFlowLauncher(Process p) =>
        _systemProcessList.Contains(p.ProcessName.ToLower()) ||
        string.Equals(p.ProcessName, FlowLauncherProcessName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Get title based on process name and id
    /// </summary>
    public static string GetProcessNameIdTitle(Process p)
        => p.ProcessName + " - " + p.Id;

    /// <summary>
    /// Returns a Process for evey running non-system process
    /// </summary>
    public List<Process> GetMatchingProcesses()
    {
        List<Process> processlist = [];
        foreach (Process p in Process.GetProcesses())
        {
            if (IsSystemProcessOrFlowLauncher(p))
                continue;

            processlist.Add(p);
        }

        return processlist;
    }

    /// <summary>
    /// Returns a dictionary of process IDs and their window titles for processes that have a visible main window with a non-empty title.
    /// </summary>
    public static Dictionary<int, string> GetProcessesWithNonEmptyWindowTitle()
    {
        // Collect all window handles
        List<nint> visibleWindows = WindowHelper.GetAllWindows();

        // Concurrently process each window handle
        var processDict = new ConcurrentDictionary<int, string>();
        var processedProcessIds = new ConcurrentDictionary<int, byte>();
        Parallel.ForEach(visibleWindows, hwnd =>
        {
            string windowTitle = WindowHelper.GetWindowTitle(hwnd);
            if (!string.IsNullOrWhiteSpace(windowTitle))
            {
                uint threadId = WindowHelper.GetWindowThreadProcessId(hwnd, out uint processId);
                if (threadId == 0u || processId == 0u)
                    return;

                // Ensure each process ID is processed only once
                if (processedProcessIds.TryAdd((int)processId, 0))
                {
                    try
                    {
                        var process = Process.GetProcessById((int)processId);
                        processDict.TryAdd((int)processId, windowTitle);
                    }
                    catch
                    {
                        // Handle exceptions (e.g., process exited)
                    }
                }
            }
        });

        return [with(processDict)];
    }

    /// <summary>
    /// Returns all non-system processes whose file path matches the given processPath
    /// </summary>
    public IEnumerable<Process> GetSimilarProcesses(string processPath)
    {
        return Process.GetProcesses()
            .Where(p => !IsSystemProcessOrFlowLauncher(p) && TryGetProcessFileName(p) == processPath);
    }

    public static void TryKill(Process p)
    {
        try
        {
            if (!p.HasExited)
            {
                p.Kill();
                p.WaitForExit(50);
            }
        }
        catch (Exception e)
        {
            Main.Context.Logger.LogError(e, $"Failed to kill process {p.ProcessName}");
        }
    }

    public static string TryGetProcessFileName(Process p)
    {
        string path;
        try
        {
            path = Interop.Programs.ProcessHelper.GetProcessFileName((uint)p.Id);
        }
        catch (Exception e)
        {
            // It is common to get access denied errors
            if (e is not Win32Exception win32Ex || win32Ex.NativeErrorCode != 5)
                Main.Context.Logger.LogError(e, $"Failed to get file name for process {p.ProcessName}");

            path = string.Empty;
        }

        return path;
    }
}
