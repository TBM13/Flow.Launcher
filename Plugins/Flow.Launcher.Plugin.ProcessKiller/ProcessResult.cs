using System.Diagnostics;
using Flow.Launcher.Infrastructure.Helpers;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    internal class ProcessResult(Process process, int score, string title, string tooltip)
    {
        public Process Process { get; } = process;

        public int Score { get; } = score;

        public string Title { get; } = title;

        public string Tooltip { get; } = tooltip;
    }
}
