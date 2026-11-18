using Flow.Launcher.Core.Settings;
using Flow.Launcher.PluginSDK.UI;

namespace Flow.Launcher.Settings.Pages;

public partial class SettingsThemeViewModel(ISettingsAPI settings) : BaseSettingsPageViewModel
{
    public ISettingsAPI Settings { get; } = settings;

    public override string Title => "Appearance";
    public override string IconPath => "pack://application:,,,/Images/theme.png";

    public static IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);

    public IReadOnlyList<LocalizedEnumItem<ColorScheme>> ColorSchemeItems => EnumLocalization<ColorScheme>.Items;

    public void ChangeScheme(ColorScheme scheme)
    {
        ((App.App)System.Windows.Application.Current).ApplyTheme(scheme);
    }
}
