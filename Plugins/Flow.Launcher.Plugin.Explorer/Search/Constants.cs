using System.IO;
using System.Reflection;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    internal static class Constants
    {
        internal const string ExplorerIconImagePath = "Images\\explorer.png";

        internal const char AllFilesFolderSearchWildcard = '>';

        internal const char UnixDirectorySeparator = '/';

        internal const char DirectorySeparator = '\\';

        internal static string ExplorerIconImageFullPath
            => Directory.GetParent(Assembly.GetExecutingAssembly().Location.ToString()) + "\\" + ExplorerIconImagePath;
    }
}
