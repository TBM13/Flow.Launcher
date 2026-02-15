using CommunityToolkit.Mvvm.ComponentModel;

namespace Flow.Launcher.Plugin.Calculator;

public partial class Settings : ObservableObject
{
    [ObservableProperty]
    public partial DecimalSeparator DecimalSeparator { get; set; } = DecimalSeparator.UseSystemLocale;

    [ObservableProperty]
    public partial int MaxDecimalPlaces { get; set; } = 10;

    [ObservableProperty]
    public partial bool ShowErrorMessage { get; set; } = false;
}
