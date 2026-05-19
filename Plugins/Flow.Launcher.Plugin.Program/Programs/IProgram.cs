using System.Collections.Generic;
using Flow.Launcher.Infrastructure.API;
using Flow.Launcher.Infrastructure.Results;

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
