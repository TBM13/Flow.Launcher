using System.IO;

namespace Flow.Launcher.Interop;

internal static class Constant
{
    public static readonly string ProgramDirectory = AppContext.BaseDirectory;
    public static readonly string CommandExecutablePath =
        Path.Combine(ProgramDirectory, "Command", "Flow.Launcher.Command.exe");
}
