using System.ComponentModel;

namespace Flow.Launcher.Plugin.Calculator
{
    public enum DecimalSeparator
    {
        [Description(Localize.Setting_DecimalSeparator_UseSystemLocale)]
        UseSystemLocale,
        [Description(Localize.Setting_DecimalSeparator_Dot)]
        Dot,
        [Description(Localize.Setting_DecimalSeparator_Comma)]
        Comma
    }
}
