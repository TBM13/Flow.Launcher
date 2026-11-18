using System.ComponentModel;

namespace Flow.Launcher.Plugin.MagicQuery.Settings;

public enum MatchType
{
    [Description("Exact")]
    Exact,

    [Description("Partial")]
    Partial,

    [Description("Regex")]
    Regex
}
