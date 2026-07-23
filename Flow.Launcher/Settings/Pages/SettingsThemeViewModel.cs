using Flow.Launcher.Core.Settings;
using Flow.Launcher.Interop;
using Flow.Launcher.PluginSDK.WPF;
using iNKORE.UI.WPF.Modern;

namespace Flow.Launcher.Settings.Pages;

public partial class SettingsThemeViewModel(ISettingsAPI settings) : BaseSettingsPageViewModel
{
    public ISettingsAPI Settings { get; } = settings;

    public override string Title => "Appearance";
    public override string IconPath => "pack://application:,,,/Images/theme.png";

    public IReadOnlyList<LocalizedEnumItem<ColorScheme>> ColorSchemes { get; }
        = EnumLocalization<ColorScheme>.Items;
    public ColorScheme ColorScheme
    {
        get => Settings.ColorScheme;
        set
        {
            ThemeManager.Current.ApplicationTheme = value switch
            {
                ColorScheme.Light => ApplicationTheme.Light,
                ColorScheme.Dark => ApplicationTheme.Dark,
                ColorScheme.System => null,
                _ => ThemeManager.Current.ApplicationTheme
            };

            Settings.ColorScheme = value;
            ApplicationHelper.SetAppMode(value switch
            {
                ColorScheme.System => AppMode.AllowDark,
                ColorScheme.Light => AppMode.ForceLight,
                ColorScheme.Dark => AppMode.ForceDark,
                _ => throw new InvalidOperationException($"Unexpected color scheme: {value}")
            });
        }
    }

    public IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);

    public bool FixedWindowSize
    {
        get => Settings.FixedWindowSize;
        set => Settings.FixedWindowSize = value;
    }
}
