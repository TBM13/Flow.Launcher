using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
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

    private readonly string DefaultFont = Win32Helper.GetSystemDefaultFont();

    public static string LinkHowToCreateTheme => @"https://www.flowlauncher.com/theme-builder/";
    public static string LinkThemeGallery => "https://github.com/Flow-Launcher/Flow.Launcher/discussions/1438";

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

    public double WindowHeightSize
    {
        get => Settings.WindowHeightSize;
        set => Settings.WindowHeightSize = value;
    }

    public double ItemHeightSize
    {
        get => Settings.ItemHeightSize;
        set => Settings.ItemHeightSize = value;
    }

    public double QueryBoxFontSize
    {
        get => Settings.QueryBoxFontSize;
        set => Settings.QueryBoxFontSize = value;
    }

    public double ResultItemFontSize
    {
        get => Settings.ResultItemFontSize;
        set => Settings.ResultItemFontSize = value;
    }

    public double ResultSubItemFontSize
    {
        get => Settings.ResultSubItemFontSize;
        set => Settings.ResultSubItemFontSize = value;
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

    public FontFamily SelectedQueryBoxFont
    {
        get
        {
            var fontExists = Fonts.SystemFontFamilies.Any(
                fontFamily =>
                    fontFamily.FamilyNames.Values != null &&
                    fontFamily.FamilyNames.Values.Contains(Settings.QueryBoxFont)
            );

            return fontExists switch
            {
                true => new FontFamily(Settings.QueryBoxFont),
                _ => new FontFamily(DefaultFont)
            };
        }
        set
        {
            Settings.QueryBoxFont = value.ToString();
            _theme.UpdateFonts();
        }
    }

    public FamilyTypeface SelectedQueryBoxFontFaces
    {
        get
        {
            var typeface = SyntaxSugars.CallOrRescueDefault(
                () => SelectedQueryBoxFont.ConvertFromInvariantStringsOrNormal(
                    Settings.QueryBoxFontStyle,
                    Settings.QueryBoxFontWeight,
                    Settings.QueryBoxFontStretch
                )
            );
            return typeface;
        }
        set
        {
            Settings.QueryBoxFontStretch = value.Stretch.ToString();
            Settings.QueryBoxFontWeight = value.Weight.ToString();
            Settings.QueryBoxFontStyle = value.Style.ToString();
            _theme.UpdateFonts();
        }
    }

    public FontFamily SelectedResultFont
    {
        get
        {
            var fontExists = Fonts.SystemFontFamilies.Any(
                fontFamily =>
                    fontFamily.FamilyNames.Values != null &&
                    fontFamily.FamilyNames.Values.Contains(Settings.ResultFont)
            );
            return fontExists switch
            {
                true => new FontFamily(Settings.ResultFont),
                _ => new FontFamily(DefaultFont)
            };
        }
        set
        {
            Settings.ResultFont = value.ToString();
            _theme.UpdateFonts();
        }
    }

    public FamilyTypeface SelectedResultFontFaces
    {
        get
        {
            var typeface = SyntaxSugars.CallOrRescueDefault(
                () => SelectedResultFont.ConvertFromInvariantStringsOrNormal(
                    Settings.ResultFontStyle,
                    Settings.ResultFontWeight,
                    Settings.ResultFontStretch
                )
            );
            return typeface;
        }
        set
        {
            Settings.ResultFontStretch = value.Stretch.ToString();
            Settings.ResultFontWeight = value.Weight.ToString();
            Settings.ResultFontStyle = value.Style.ToString();
            _theme.UpdateFonts();
        }
    }

    public FontFamily SelectedResultSubFont
    {
        get
        {
            if (Fonts.SystemFontFamilies.Any(o =>
                    o.FamilyNames.Values != null &&
                    o.FamilyNames.Values.Contains(Settings.ResultSubFont)))
            {
                var font = new FontFamily(Settings.ResultSubFont);
                return font;
            }
            else
            {
                var font = new FontFamily(DefaultFont);
                return font;
            }
        }
        set
        {
            Settings.ResultSubFont = value.ToString();
            _theme.UpdateFonts();
        }
    }

    public FamilyTypeface SelectedResultSubFontFaces
    {
        get
        {
            var typeface = SyntaxSugars.CallOrRescueDefault(
                () => SelectedResultSubFont.ConvertFromInvariantStringsOrNormal(
                    Settings.ResultSubFontStyle,
                    Settings.ResultSubFontWeight,
                    Settings.ResultSubFontStretch
                ));
            return typeface;
        }
        set
        {
            Settings.ResultSubFontStretch = value.Stretch.ToString();
            Settings.ResultSubFontWeight = value.Weight.ToString();
            Settings.ResultSubFontStyle = value.Style.ToString();
            _theme.UpdateFonts();
        }
    }

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

    [RelayCommand]
    private void OpenThemesFolder()
    {
        App.API.OpenDirectory(DataLocation.ThemesDirectory);
    }

    [RelayCommand]
    public void Reset()
    {
        SelectedQueryBoxFont = new FontFamily(DefaultFont);
        SelectedQueryBoxFontFaces = new FamilyTypeface { Stretch = FontStretches.Normal, Weight = FontWeights.Normal, Style = FontStyles.Normal };
        QueryBoxFontSize = 16;

        SelectedResultFont = new FontFamily(DefaultFont);
        SelectedResultFontFaces = new FamilyTypeface { Stretch = FontStretches.Normal, Weight = FontWeights.Normal, Style = FontStyles.Normal };
        ResultItemFontSize = 16;

        SelectedResultSubFont = new FontFamily(DefaultFont);
        SelectedResultSubFontFaces = new FamilyTypeface { Stretch = FontStretches.Normal, Weight = FontWeights.Normal, Style = FontStyles.Normal };
        ResultSubItemFontSize = 13;

        WindowHeightSize = 42;
        ItemHeightSize = 58;
    }

    [RelayCommand]
    private void Import()
    {
        var resourceDictionary = _theme.GetCurrentResourceDictionary();

        if (resourceDictionary["QueryBoxStyle"] is Style queryBoxStyle)
        {
            var fontSizeSetter = queryBoxStyle.Setters
                .OfType<Setter>()
                .FirstOrDefault(setter => setter.Property == TextBox.FontSizeProperty);
            if (fontSizeSetter?.Value is double fontSize)
            {
                QueryBoxFontSize = fontSize;
            }

            var heightSetter = queryBoxStyle.Setters
                .OfType<Setter>()
                .FirstOrDefault(setter => setter.Property == FrameworkElement.HeightProperty);
            if (heightSetter?.Value is double height)
            {
                WindowHeightSize = height;
            }
        }

        if (resourceDictionary["ResultItemHeight"] is double resultItemHeight)
        {
            ItemHeightSize = resultItemHeight;
        }

        if (resourceDictionary["ItemTitleStyle"] is Style itemTitleStyle)
        {
            var fontSizeSetter = itemTitleStyle.Setters
                .OfType<Setter>()
                .FirstOrDefault(setter => setter.Property == TextBlock.FontSizeProperty);
            if (fontSizeSetter?.Value is double fontSize)
            {
                ResultItemFontSize = fontSize;
            }
        }

        if (resourceDictionary["ItemSubTitleStyle"] is Style itemSubTitleStyle)
        {
            var fontSizeSetter = itemSubTitleStyle.Setters
                .OfType<Setter>()
                .FirstOrDefault(setter => setter.Property == TextBlock.FontSizeProperty);
            if (fontSizeSetter?.Value is double fontSize)
            {
                ResultSubItemFontSize = fontSize;
            }
        }
    }
}
