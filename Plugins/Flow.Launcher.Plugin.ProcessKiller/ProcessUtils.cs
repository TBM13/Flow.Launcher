using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Flow.Launcher.Interop;
using Flow.Launcher.Interop.Programs;

namespace Flow.Launcher.Plugin.ProcessKiller;

internal record ProcessInfo(Process Process, string Path, string? WindowTitle, bool AnyWindowVisible);

internal static class ProcessUtils
{
    private static readonly string SvchostPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "svchost.exe");

    public static List<ProcessInfo>? GetKillableProcesses(Settings settings)
    {
        // Get all non-system processes
        Process[] allProcesses = Process.GetProcesses();
        if (allProcesses.Length == 0)
            return null;

        List<ProcessInfo> killableProcesses = [];
        var processWindowData =
            settings.ShowWindowTitle || settings.PutVisibleWindowProcessesTop
            ? GetWindowsInfo() : [];

        foreach (Process p in allProcesses)
        {
            // Filter system processes
            if (p.Id is 0 or 4)
                continue;

            string path = TryGetProcessFileName(p);
            if (string.IsNullOrEmpty(path))
                // Processes without a file path are usually system processes
                continue;

            // Skip svchost.exe services
            if (settings.FilterSvchostProcesses
                && path.Equals(SvchostPath, StringComparison.OrdinalIgnoreCase))
                continue;

            string? windowTitle;
            bool anyWindowVisible;
            if (processWindowData.TryGetValue(p.Id, out var data))
            {
                // TODO: Do something with other window titles
                windowTitle = data.WindowTitles.FirstOrDefault();
                anyWindowVisible = data.IsVisible;
            }
            else
            {
                windowTitle = null;
                anyWindowVisible = false;
            }

            killableProcesses.Add(new(p, path, windowTitle, anyWindowVisible));
        }

        return killableProcesses;
    }

    /// <summary>
    /// Retrieves all the non-empty window titles of processes and whether at least one window is visible.
    /// <para/>
    /// The first elements of the window titles list are the titles of visible windows (if any).
    /// </summary>
    /// <remarks>Key is the process ID.</remarks>
    private static Dictionary<int, (List<string> WindowTitles, bool IsVisible)> GetWindowsInfo()
    {
        List<nint> windowHandles = WindowHelper.GetAllWindows();

        Dictionary<int, (List<string> Titles, bool IsVisible)> result = [];
        foreach (nint hwnd in windowHandles)
        {
            WindowHelper.GetWindowThreadProcessId(hwnd, out uint processId);
            int pid = (int)processId;
            bool isVisible = WindowHelper.IsWindowVisible(hwnd);
            string title = WindowHelper.GetWindowTitle(hwnd);

            if (!result.TryGetValue(pid, out var data))
                data = ([], false);
            if (!string.IsNullOrWhiteSpace(title))
            {
                if (isVisible)
                    data.Titles.Insert(0, title); // Visible windows first
                else
                    data.Titles.Add(title);
            }

            if (isVisible)
                data.IsVisible = true;

            result[pid] = data;
        }

        return result;
    }

    /// <summary>
    /// Returns all processes whose path match the given one.
    /// </summary>
    public static IEnumerable<Process> GetProcessesWithPath(string processPath)
    {
        return Process.GetProcesses()
            .Where(p => TryGetProcessFileName(p).Equals(processPath, StringComparison.OrdinalIgnoreCase));
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
            Main.Context.Logger.LogError(e, $"Failed to kill process {p.ProcessName} ({p.Id})");
            Main.Context.API.ShowMsgError($"Failed to kill {p.ProcessName} ({p.Id}): {e.Message}");
        }
    }

    public static string TryGetProcessFileName(Process p)
    {
        string path;
        try
        {
            path = ProcessHelper.GetProcessFileName((uint)p.Id);
        }
        catch (Exception e)
        {
            // It is common to get access denied errors
            if (e is not Win32Exception win32Ex || win32Ex.NativeErrorCode != 5)
                Main.Context.Logger.LogError(e, $"Failed to get file name for process {p.ProcessName} ({p.Id})");

            path = string.Empty;
        }

        return path;
    }
}
