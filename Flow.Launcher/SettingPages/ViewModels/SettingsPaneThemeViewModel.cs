using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure;
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

    public class ColorSchemeData : DropdownDataGeneric<ColorSchemes> { }

    public List<ColorSchemeData> ColorSchemes { get; } = DropdownDataGeneric<ColorSchemes>.GetValues<ColorSchemeData>("ColorScheme");
    public string ColorScheme
    {
        get => Settings.ColorScheme;
        set
        {
            ThemeManager.Current.ApplicationTheme = value switch
            {
                Constant.Light => ApplicationTheme.Light,
                Constant.Dark => ApplicationTheme.Dark,
                Constant.System => null,
                _ => ThemeManager.Current.ApplicationTheme
            };
            Settings.ColorScheme = value;
            _ = _theme.RefreshFrameAsync();
            Win32Helper.EnableWin32DarkMode(value);
        }
    }

    public IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);

    public bool KeepMaxResults
    {
        get => Settings.KeepMaxResults;
        set => Settings.KeepMaxResults = value;
    }
}
