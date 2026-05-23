using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Infrastructure.WPF;

namespace Flow.Launcher.Plugin.Calculator.ViewModels;

public class SettingsViewModel(Settings settings)
{
    public Settings Settings { get; } = settings;

    public static IEnumerable<int> MaxDecimalPlacesRange => Enumerable.Range(1, 20);

    public static IEnumerable<LocalizedEnumItem<DecimalSeparator>> AllDecimalSeparator
        => EnumLocalization<DecimalSeparator>.Items;
}
