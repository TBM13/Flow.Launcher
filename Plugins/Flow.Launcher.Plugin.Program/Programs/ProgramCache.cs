using MemoryPack;

namespace Flow.Launcher.Plugin.Program.Programs;

[MemoryPackable]
public sealed partial class Win32ProgramCache
{
    public Win32Program[] Programs { get; set; } = [];
}

[MemoryPackable]
public sealed partial class UwpProgramCache
{
    public UwpProgram[] Programs { get; set; } = [];
}
