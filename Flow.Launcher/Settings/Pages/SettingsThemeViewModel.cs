using Flow.Launcher.Core.Settings;
using Flow.Launcher.Interop;
using iNKORE.UI.WPF.Modern;

namespace Flow.Launcher.Settings.Pages;

public partial class SettingsThemeViewModel(ISettingsAPI settings) : BaseSettingsPageViewModel
{
    public ISettingsAPI Settings { get; } = settings;

    public override string Title => "Appearance";
    public override string IconPath => "pack://application:,,,/Images/theme.png";

    public static IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);

    public void ChangeScheme(ColorScheme scheme)
    {
        ThemeManager.Current.ApplicationTheme = scheme switch
        {
            ColorScheme.Light => ApplicationTheme.Light,
            ColorScheme.Dark => ApplicationTheme.Dark,
            ColorScheme.System => null,
            _ => throw new InvalidOperationException($"Unexpected color scheme: {scheme}")
        };
        ApplicationHelper.SetAppMode(scheme switch
        {
            ColorScheme.System => AppMode.AllowDark,
            ColorScheme.Light => AppMode.ForceLight,
            ColorScheme.Dark => AppMode.ForceDark,
            _ => throw new InvalidOperationException($"Unexpected color scheme: {scheme}")
        });
    }
}
