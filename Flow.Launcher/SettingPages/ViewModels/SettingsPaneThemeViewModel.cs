using System.Collections.Generic;
using System.IO;
using System.Linq;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.ViewModel;
using iNKORE.UI.WPF.Modern;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneThemeViewModel : BaseModel
{
    public Settings Settings { get; }

    private readonly Theme _theme;


    private List<ThemeData> _themes;
    public List<ThemeData> Themes => _themes ??= App.API.GetAvailableThemes();

    private ThemeData _selectedTheme;
    public ThemeData SelectedTheme
    {
        get => _selectedTheme ??= Themes.Find(v => v == App.API.GetCurrentTheme());
        set
        {
            _selectedTheme = value;
            if (!App.API.SetCurrentTheme(value))
            {
                // Revert selection if failed to set theme
                OnPropertyChanged();
            }

            // Update UI state
            OnPropertyChanged(nameof(DropShadowEffect));
        }
    }

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
            OnPropertyChanged(nameof(DropShadowEffect));
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

    public bool UseGlyphIcons
    {
        get => Settings.UseGlyphIcons;
        set => Settings.UseGlyphIcons = value;
    }

    public ResultsViewModel PreviewResults { get; }

    public SettingsPaneThemeViewModel(Settings settings, Theme theme)
    {
        Settings = settings;
        _theme = theme;
        var results = new List<Result>
            {
                new()
                {
                    Title = Localize.SampleTitleExplorer(),
                    SubTitle = Localize.SampleSubTitleExplorer(),
                    IcoPath = Path.Combine(
                        Constant.ProgramDirectory,
                        @"Plugins\Flow.Launcher.Plugin.Explorer\Images\explorer.png"
                    )
                },
                new()
                {
                    Title = Localize.SampleTitleProgram(),
                    SubTitle = Localize.SampleSubTitleProgram(),
                    IcoPath = Path.Combine(
                        Constant.ProgramDirectory,
                        @"Plugins\Flow.Launcher.Plugin.Program\Images\program.png"
                    )
                },
                new()
                {
                    Title = Localize.SampleTitleProcessKiller(),
                    SubTitle = Localize.SampleSubTitleProcessKiller(),
                    IcoPath = Path.Combine(
                        Constant.ProgramDirectory,
                        @"Plugins\Flow.Launcher.Plugin.ProcessKiller\Images\app.png"
                    )
                }
            };
        // Set main view model to null because the results are for preview only
        var vm = new ResultsViewModel(Settings, null);
        vm.AddResults(results, "PREVIEW");
        PreviewResults = vm;
    }
}
