using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.WPF;
using Flow.Launcher.Interop;
using Flow.Launcher.PluginSDK.API;
using iNKORE.UI.WPF.Modern;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneThemeViewModel(Settings settings, Theme theme) : ObservableObject
{
    public Settings Settings { get; } = settings;

    private readonly Theme _theme = theme;

    public bool DropShadowEffect
    {
        get => Settings.UseDropShadowEffect;
        set
        {
            if (value)
            {
                _theme.AddDropShadowEffectToCurrentTheme();
            }
            else
            {
                _theme.RemoveDropShadowEffectFromCurrentTheme();
            }

            Settings.UseDropShadowEffect = value;
            OnPropertyChanged();
        }
    }

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
            _ = _theme.RefreshFrameAsync();
            ApplicationHelper.SetWin32DarkMode(value);
        }
    }

    public IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);

    public bool FixedWindowSize
    {
        get => Settings.FixedWindowSize;
        set => Settings.FixedWindowSize = value;
    }
}
