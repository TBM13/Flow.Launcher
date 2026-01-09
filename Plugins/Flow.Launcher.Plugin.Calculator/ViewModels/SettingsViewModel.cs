using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UI;

namespace Flow.Launcher.Plugin.Calculator.ViewModels;

public class SettingsViewModel(Settings settings) : BaseModel
{
    public Settings Settings { get; } = settings;

    public static IEnumerable<int> MaxDecimalPlacesRange => Enumerable.Range(1, 20);

    public static IEnumerable<LocalizedEnumItem<DecimalSeparator>> AllDecimalSeparator
        => EnumLocalization.GetLocalizedEnumItems<DecimalSeparator>();

    public DecimalSeparator SelectedDecimalSeparator
    {
        get => Settings.DecimalSeparator;
        set
        {
            if (Settings.DecimalSeparator != value)
            {
                Settings.DecimalSeparator = value;
                OnPropertyChanged();
            }
        }
    }
}
