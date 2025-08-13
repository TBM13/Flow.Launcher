using System.ComponentModel;
using Flow.Launcher.Core.Resource;

namespace Flow.Launcher.Plugin.Calculator
{
    [TypeConverter(typeof(LocalizationConverter))]
    public enum DecimalSeparator
    {
        [LocalizedDescription("flowlauncher_plugin_calculator_decimal_separator_use_system_locale")]
        UseSystemLocale,

        [LocalizedDescription("flowlauncher_plugin_calculator_decimal_separator_dot")]
        Dot,

        [LocalizedDescription("flowlauncher_plugin_calculator_decimal_separator_comma")]
        Comma
    }
}
