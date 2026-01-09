using System.Collections.Generic;
using Flow.Launcher.Infrastructure;

namespace Flow.Launcher.Plugin.Program.Programs
{
    public interface IProgram
    {
        List<Result> ContextMenus(IPublicAPI api);
        Result Result(string query, IPublicAPI api);
        string UniqueIdentifier { get; set; }
        string Name { get; }
        string Location { get; }
        bool Enabled { get; }
    }
}
