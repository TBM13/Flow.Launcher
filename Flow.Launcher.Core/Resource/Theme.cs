using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shell;
using System.Windows.Threading;
using System.Xml;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;
using Microsoft.Win32;

namespace Flow.Launcher.Core.Resource
{
    public class Theme
    {
        #region Properties & Fields

        private readonly string ClassName = nameof(Theme);

        private const string ThemeMetadataNamePrefix = "Name:";
        private const string ThemeMetadataIsDarkPrefix = "IsDark:";

        private const int ShadowExtraMargin = 32;

        private readonly IPublicAPI _api;
        private readonly Settings _settings;
        private readonly List<string> _themeDirectories = new();
        private ResourceDictionary _oldResource;
        private string _oldTheme;
        private const string Folder = Constant.Themes;
        private const string Extension = ".xaml";
        private static string DirectoryPath => Path.Combine(Constant.ProgramDirectory, Folder);
        private static string UserDirectoryPath => Path.Combine(DataLocation.DataDirectory(), Folder);

        private Thickness _themeResizeBorderThickness;

        #endregion

        #region Constructor

        public Theme(IPublicAPI publicAPI, Settings settings)
        {
            _api = publicAPI;
            _settings = settings;

            _themeDirectories.Add(DirectoryPath);
            _themeDirectories.Add(UserDirectoryPath);
            MakeSureThemeDirectoriesExist();

            var dicts = Application.Current.Resources.MergedDictionaries;
            _oldResource = dicts.FirstOrDefault(d =>
            {
                if (d.Source == null) return false;

                var p = d.Source.AbsolutePath;
                return p.Contains(Folder) && Path.GetExtension(p) == Extension;
            });

            if (_oldResource != null)
            {
                _oldTheme = Path.GetFileNameWithoutExtension(_oldResource.Source.AbsolutePath);
            }
            else
            {
                _api.LogError(ClassName, "Current theme resource not found. Initializing with default theme.");
                _oldTheme = Constant.DefaultTheme;
            }
        }

        #endregion

        #region Theme Resources

        private void MakeSureThemeDirectoriesExist()
        {
            foreach (var dir in _themeDirectories.Where(dir => !Directory.Exists(dir)))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (Exception e)
                {
                    _api.LogException(ClassName, $"Exception when create directory <{dir}>", e);
                }
            }
        }

        private void UpdateResourceDictionary(ResourceDictionary dictionaryToUpdate)
        {
            // Add new resources
            if (!Application.Current.Resources.MergedDictionaries.Contains(dictionaryToUpdate))
            {
                Application.Current.Resources.MergedDictionaries.Add(dictionaryToUpdate);
            }

            // Remove old resources
            if (_oldResource != null && _oldResource != dictionaryToUpdate &&
                Application.Current.Resources.MergedDictionaries.Contains(_oldResource))
            {
                Application.Current.Resources.MergedDictionaries.Remove(_oldResource);
            }

            _oldResource = dictionaryToUpdate;
        }

        /// <summary>
        /// Updates only the font settings and refreshes the UI.
        /// </summary>
        public void UpdateFonts()
        {
            try
            {
                // Load a ResourceDictionary for the specified theme.
                var themeName = _settings.Theme;
                var dict = GetThemeResourceDictionary(themeName);

                // Apply font settings to the theme resource.
                ApplyFontSettings(dict);
                UpdateResourceDictionary(dict);

                // Must apply drop shadow effects
                _ = RefreshFrameAsync();
            }
            catch (Exception e)
            {
                _api.LogException(ClassName, "Error occurred while updating theme fonts", e);
            }
        }

        /// <summary>
        /// Loads and applies font settings to the theme resource.
        /// </summary>
        private void ApplyFontSettings(ResourceDictionary dict)
        {
            if (dict["QueryBoxStyle"] is Style queryBoxStyle)
            {
                var fontFamily = new FontFamily(_settings.QueryBoxFont);
                var fontStyle = FontHelper.GetFontStyleFromInvariantStringOrNormal(_settings.QueryBoxFontStyle);
                var fontWeight = FontHelper.GetFontWeightFromInvariantStringOrNormal(_settings.QueryBoxFontWeight);
                var fontStretch = FontHelper.GetFontStretchFromInvariantStringOrNormal(_settings.QueryBoxFontStretch);

                SetFontProperties(queryBoxStyle, fontFamily, fontStyle, fontWeight, fontStretch, true);
            }

            if (dict["ItemTitleStyle"] is Style resultItemStyle &&
                dict["ItemTitleSelectedStyle"] is Style resultItemSelectedStyle)
            {
                var fontFamily = new FontFamily(_settings.ResultFont);
                var fontStyle = FontHelper.GetFontStyleFromInvariantStringOrNormal(_settings.ResultFontStyle);
                var fontWeight = FontHelper.GetFontWeightFromInvariantStringOrNormal(_settings.ResultFontWeight);
                var fontStretch = FontHelper.GetFontStretchFromInvariantStringOrNormal(_settings.ResultFontStretch);

                SetFontProperties(resultItemStyle, fontFamily, fontStyle, fontWeight, fontStretch, false);
                SetFontProperties(resultItemSelectedStyle, fontFamily, fontStyle, fontWeight, fontStretch, false);
            }

            if (dict["ItemSubTitleStyle"] is Style resultSubItemStyle &&
                dict["ItemSubTitleSelectedStyle"] is Style resultSubItemSelectedStyle)
            {
                var fontFamily = new FontFamily(_settings.ResultSubFont);
                var fontStyle = FontHelper.GetFontStyleFromInvariantStringOrNormal(_settings.ResultSubFontStyle);
                var fontWeight = FontHelper.GetFontWeightFromInvariantStringOrNormal(_settings.ResultSubFontWeight);
                var fontStretch = FontHelper.GetFontStretchFromInvariantStringOrNormal(_settings.ResultSubFontStretch);

                SetFontProperties(resultSubItemStyle, fontFamily, fontStyle, fontWeight, fontStretch, false);
                SetFontProperties(resultSubItemSelectedStyle, fontFamily, fontStyle, fontWeight, fontStretch, false);
            }
        }

        /// <summary>
        /// Applies font properties to a Style.
        /// </summary>
        private static void SetFontProperties(Style style, FontFamily fontFamily, FontStyle fontStyle, FontWeight fontWeight, FontStretch fontStretch, bool isTextBox)
        {
            // Remove existing font-related setters  
            if (isTextBox)
            {
                //  First, find the setters to remove and store them in a list  
                var settersToRemove = style.Setters
                    .OfType<Setter>()
                    .Where(setter =>
                        setter.Property == Control.FontFamilyProperty ||
                        setter.Property == Control.FontStyleProperty ||
                        setter.Property == Control.FontWeightProperty ||
                        setter.Property == Control.FontStretchProperty)
                    .ToList();

                // Remove each found setter one by one  
                foreach (var setter in settersToRemove)
                {
                    style.Setters.Remove(setter);
                }

                // Add New font setter
                style.Setters.Add(new Setter(Control.FontFamilyProperty, fontFamily));
                style.Setters.Add(new Setter(Control.FontStyleProperty, fontStyle));
                style.Setters.Add(new Setter(Control.FontWeightProperty, fontWeight));
                style.Setters.Add(new Setter(Control.FontStretchProperty, fontStretch));

                //  Set caret brush (retain existing logic)
                var caretBrushPropertyValue = style.Setters.OfType<Setter>().Any(x => x.Property.Name == "CaretBrush");
                var foregroundPropertyValue = style.Setters.OfType<Setter>().Where(x => x.Property.Name == "Foreground")
                    .Select(x => x.Value).FirstOrDefault();
                if (!caretBrushPropertyValue && foregroundPropertyValue != null)
                    style.Setters.Add(new Setter(TextBoxBase.CaretBrushProperty, foregroundPropertyValue));
            }
            else
            {
                var settersToRemove = style.Setters
                    .OfType<Setter>()
                    .Where(setter =>
                        setter.Property == TextBlock.FontFamilyProperty ||
                        setter.Property == TextBlock.FontStyleProperty ||
                        setter.Property == TextBlock.FontWeightProperty ||
                        setter.Property == TextBlock.FontStretchProperty)
                    .ToList();

                foreach (var setter in settersToRemove)
                {
                    style.Setters.Remove(setter);
                }

                style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, fontFamily));
                style.Setters.Add(new Setter(TextBlock.FontStyleProperty, fontStyle));
                style.Setters.Add(new Setter(TextBlock.FontWeightProperty, fontWeight));
                style.Setters.Add(new Setter(TextBlock.FontStretchProperty, fontStretch));
            }
        }

        private ResourceDictionary GetThemeResourceDictionary(string theme)
        {
            var uri = GetThemePath(theme);
            var dict = new ResourceDictionary
            {
                Source = new Uri(uri, UriKind.Absolute)
            };

            return dict;
        }

        private ResourceDictionary GetResourceDictionary(string theme)
        {
            var dict = GetThemeResourceDictionary(theme);

            if (dict["QueryBoxStyle"] is Style queryBoxStyle)
            {
                var fontFamily = new FontFamily(_settings.QueryBoxFont);
                var fontStyle = FontHelper.GetFontStyleFromInvariantStringOrNormal(_settings.QueryBoxFontStyle);
                var fontWeight = FontHelper.GetFontWeightFromInvariantStringOrNormal(_settings.QueryBoxFontWeight);
                var fontStretch = FontHelper.GetFontStretchFromInvariantStringOrNormal(_settings.QueryBoxFontStretch);

                queryBoxStyle.Setters.Add(new Setter(Control.FontFamilyProperty, fontFamily));
                queryBoxStyle.Setters.Add(new Setter(Control.FontStyleProperty, fontStyle));
                queryBoxStyle.Setters.Add(new Setter(Control.FontWeightProperty, fontWeight));
                queryBoxStyle.Setters.Add(new Setter(Control.FontStretchProperty, fontStretch));

                var caretBrushPropertyValue = queryBoxStyle.Setters.OfType<Setter>().Any(x => x.Property.Name == "CaretBrush");
                var foregroundPropertyValue = queryBoxStyle.Setters.OfType<Setter>().Where(x => x.Property.Name == "Foreground")
                    .Select(x => x.Value).FirstOrDefault();
                if (!caretBrushPropertyValue && foregroundPropertyValue != null) //otherwise BaseQueryBoxStyle will handle styling
                    queryBoxStyle.Setters.Add(new Setter(TextBoxBase.CaretBrushProperty, foregroundPropertyValue));
            }

            if (dict["ItemTitleStyle"] is Style resultItemStyle &&
                dict["ItemTitleSelectedStyle"] is Style resultItemSelectedStyle)
            {
                Setter fontFamily = new Setter(TextBlock.FontFamilyProperty, new FontFamily(_settings.ResultFont));
                Setter fontStyle = new Setter(TextBlock.FontStyleProperty, FontHelper.GetFontStyleFromInvariantStringOrNormal(_settings.ResultFontStyle));
                Setter fontWeight = new Setter(TextBlock.FontWeightProperty, FontHelper.GetFontWeightFromInvariantStringOrNormal(_settings.ResultFontWeight));
                Setter fontStretch = new Setter(TextBlock.FontStretchProperty, FontHelper.GetFontStretchFromInvariantStringOrNormal(_settings.ResultFontStretch));

                Setter[] setters = { fontFamily, fontStyle, fontWeight, fontStretch };
                Array.ForEach(
                    new[] { resultItemStyle, resultItemSelectedStyle }, o
                    => Array.ForEach(setters, p => o.Setters.Add(p)));
            }

            if (
                dict["ItemSubTitleStyle"] is Style resultSubItemStyle &&
                dict["ItemSubTitleSelectedStyle"] is Style resultSubItemSelectedStyle)
            {
                Setter fontFamily = new Setter(TextBlock.FontFamilyProperty, new FontFamily(_settings.ResultSubFont));
                Setter fontStyle = new Setter(TextBlock.FontStyleProperty, FontHelper.GetFontStyleFromInvariantStringOrNormal(_settings.ResultSubFontStyle));
                Setter fontWeight = new Setter(TextBlock.FontWeightProperty, FontHelper.GetFontWeightFromInvariantStringOrNormal(_settings.ResultSubFontWeight));
                Setter fontStretch = new Setter(TextBlock.FontStretchProperty, FontHelper.GetFontStretchFromInvariantStringOrNormal(_settings.ResultSubFontStretch));

                Setter[] setters = { fontFamily, fontStyle, fontWeight, fontStretch };
                Array.ForEach(
                    new[] { resultSubItemStyle, resultSubItemSelectedStyle }, o
                    => Array.ForEach(setters, p => o.Setters.Add(p)));
            }

            /* Ignore Theme Window Width and use setting */
            var windowStyle = dict["WindowStyle"] as Style;
            var width = _settings.WindowSize;
            windowStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty, width));
            return dict;
        }

        public ResourceDictionary GetCurrentResourceDictionary()
        {
            return GetResourceDictionary(_settings.Theme);
        }

        private ThemeData GetThemeDataFromPath(string path)
        {
            using var reader = XmlReader.Create(path);
            reader.Read();

            var extensionlessName = Path.GetFileNameWithoutExtension(path);

            if (reader.NodeType is not XmlNodeType.Comment)
                return new ThemeData(extensionlessName, extensionlessName);

            var commentLines = reader.Value.Trim().Split('\n').Select(v => v.Trim());

            var name = extensionlessName;
            bool? isDark = null;
            foreach (var line in commentLines)
            {
                if (line.StartsWith(ThemeMetadataNamePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    name = line[ThemeMetadataNamePrefix.Length..].Trim();
                }
                else if (line.StartsWith(ThemeMetadataIsDarkPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    isDark = bool.Parse(line[ThemeMetadataIsDarkPrefix.Length..].Trim());
                }
            }

            return new ThemeData(extensionlessName, name, isDark);
        }

        private string GetThemePath(string themeName)
        {
            foreach (string themeDirectory in _themeDirectories)
            {
                string path = Path.Combine(themeDirectory, themeName + Extension);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }

        #endregion

        #region Get & Change Theme

        public ThemeData GetCurrentTheme()
        {
            var themes = GetAvailableThemes();
            var matchingTheme = themes.FirstOrDefault(t => t.FileNameWithoutExtension == _settings.Theme);
            if (matchingTheme == null)
            {
                _api.LogWarn(ClassName, $"No matching theme found for '{_settings.Theme}'. Falling back to the first available theme.");
            }
            return matchingTheme ?? themes.FirstOrDefault();
        }

        public List<ThemeData> GetAvailableThemes()
        {
            List<ThemeData> themes = new List<ThemeData>();
            foreach (var themeDirectory in _themeDirectories)
            {
                var filePaths = Directory
                    .GetFiles(themeDirectory)
                    .Where(filePath => filePath.EndsWith(Extension) && !filePath.EndsWith("Base.xaml"))
                    .Select(GetThemeDataFromPath);
                themes.AddRange(filePaths);
            }

            return themes.OrderBy(o => o.Name).ToList();
        }

        public bool ChangeTheme(string theme = null)
        {
            if (string.IsNullOrEmpty(theme))
                theme = _settings.Theme;

            string path = GetThemePath(theme);
            try
            {
                if (string.IsNullOrEmpty(path))
                    throw new DirectoryNotFoundException($"Theme path can't be found <{path}>");

                // Retrieve theme resource – always use the resource with font settings applied.
                var resourceDict = GetResourceDictionary(theme);

                UpdateResourceDictionary(resourceDict);

                _settings.Theme = theme;

                //always allow re-loading default theme, in case of failure of switching to a new theme from default theme
                if (_oldTheme != theme || theme == Constant.DefaultTheme)
                {
                    _oldTheme = Path.GetFileNameWithoutExtension(_oldResource.Source.AbsolutePath);
                }

                // Apply drop shadow effect so that we do not need to call it again
                _ = RefreshFrameAsync();

                return true;
            }
            catch (DirectoryNotFoundException)
            {
                _api.LogError(ClassName, $"Theme <{theme}> path can't be found");
                if (theme != Constant.DefaultTheme)
                {
                    _api.ShowMsgBox(Localize.theme_load_failure_path_not_exists(theme));
                    ChangeTheme(Constant.DefaultTheme);
                }
                return false;
            }
            catch (XamlParseException e)
            {
                _api.LogException(ClassName, $"Theme <{theme}> fail to parse xaml", e);
                if (theme != Constant.DefaultTheme)
                {
                    _api.ShowMsgBox(Localize.theme_load_failure_parse_error(theme));
                    ChangeTheme(Constant.DefaultTheme);
                }
                return false;
            }
            catch (Exception e)
            {
                _api.LogException(ClassName, $"Theme <{theme}> fail to load", e);
                if (theme != Constant.DefaultTheme)
                {
                    _api.ShowMsgBox(Localize.theme_load_failure_parse_error(theme));
                    ChangeTheme(Constant.DefaultTheme);
                }
                return false;
            }
        }

        #endregion

        #region Shadow Effect

        public void AddDropShadowEffectToCurrentTheme()
        {
            var dict = GetCurrentResourceDictionary();

            var windowBorderStyle = dict["WindowBorderStyle"] as Style;

            var effectSetter = new Setter
            {
                Property = UIElement.EffectProperty,
                Value = new DropShadowEffect
                {
                    Opacity = 0.3,
                    ShadowDepth = 12,
                    Direction = 270,
                    BlurRadius = 30
                }
            };

            if (windowBorderStyle.Setters.FirstOrDefault(setterBase => setterBase is Setter setter && setter.Property == FrameworkElement.MarginProperty) is not Setter marginSetter)
            {
                var margin = new Thickness(ShadowExtraMargin, 12, ShadowExtraMargin, ShadowExtraMargin);
                marginSetter = new Setter()
                {
                    Property = FrameworkElement.MarginProperty,
                    Value = margin,
                };
                windowBorderStyle.Setters.Add(marginSetter);

                SetResizeBoarderThickness(margin);
            }
            else
            {
                var baseMargin = (Thickness)marginSetter.Value;
                var newMargin = new Thickness(
                    baseMargin.Left + ShadowExtraMargin,
                    baseMargin.Top + ShadowExtraMargin,
                    baseMargin.Right + ShadowExtraMargin,
                    baseMargin.Bottom + ShadowExtraMargin);
                marginSetter.Value = newMargin;

                SetResizeBoarderThickness(newMargin);
            }

            windowBorderStyle.Setters.Add(effectSetter);

            UpdateResourceDictionary(dict);
        }

        public void RemoveDropShadowEffectFromCurrentTheme()
        {
            var dict = GetCurrentResourceDictionary();
            var windowBorderStyle = dict["WindowBorderStyle"] as Style;

            if (windowBorderStyle.Setters.FirstOrDefault(setterBase => setterBase is Setter setter && setter.Property == UIElement.EffectProperty) is Setter effectSetter)
            {
                windowBorderStyle.Setters.Remove(effectSetter);
            }

            if (windowBorderStyle.Setters.FirstOrDefault(setterBase => setterBase is Setter setter && setter.Property == FrameworkElement.MarginProperty) is Setter marginSetter)
            {
                var currentMargin = (Thickness)marginSetter.Value;
                var newMargin = new Thickness(
                    currentMargin.Left - ShadowExtraMargin,
                    currentMargin.Top - ShadowExtraMargin,
                    currentMargin.Right - ShadowExtraMargin,
                    currentMargin.Bottom - ShadowExtraMargin);
                marginSetter.Value = newMargin;
            }

            SetResizeBoarderThickness(null);

            UpdateResourceDictionary(dict);
        }

        public void SetResizeBorderThickness(WindowChrome windowChrome, bool fixedWindowSize)
        {
            if (fixedWindowSize)
            {
                windowChrome.ResizeBorderThickness = new Thickness(0);
            }
            else
            {
                windowChrome.ResizeBorderThickness = _themeResizeBorderThickness;
            }
        }

        // because adding drop shadow effect will change the margin of the window,
        // we need to update the window chrome thickness to correct set the resize border
        private void SetResizeBoarderThickness(Thickness? effectMargin)
        {
            var window = Application.Current.MainWindow;
            if (WindowChrome.GetWindowChrome(window) is WindowChrome windowChrome)
            {
                // Save the theme resize border thickness so that we can restore it if we change ResizeWindow setting
                if (effectMargin == null)
                {
                    _themeResizeBorderThickness = SystemParameters.WindowResizeBorderThickness;
                }
                else
                {
                    _themeResizeBorderThickness = new Thickness(
                        effectMargin.Value.Left + SystemParameters.WindowResizeBorderThickness.Left,
                        effectMargin.Value.Top + SystemParameters.WindowResizeBorderThickness.Top,
                        effectMargin.Value.Right + SystemParameters.WindowResizeBorderThickness.Right,
                        effectMargin.Value.Bottom + SystemParameters.WindowResizeBorderThickness.Bottom);
                }

                // Apply the resize border thickness to the window chrome
                SetResizeBorderThickness(windowChrome, _settings.KeepMaxResults);
            }
        }

        #endregion

        #region Blur Handling

        /// <summary>
        /// Refreshes the frame to apply the current theme settings.
        /// </summary>
        public async Task RefreshFrameAsync()
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Remove OS minimizing/maximizing animation
                // Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED, 3);

                AutoDropShadow(_settings.UseDropShadowEffect);
            }, DispatcherPriority.Render);
        }

        private void AutoDropShadow(bool useDropShadowEffect)
        {
            if (useDropShadowEffect)
            {
                AddDropShadowEffectToCurrentTheme();
            }
            else
            {
                RemoveDropShadowEffectFromCurrentTheme();
            }
        }

        private void CopyStyle(Style originalStyle, Style targetStyle)
        {
            // If the style is based on another style, copy the base style first
            if (originalStyle.BasedOn != null)
            {
                CopyStyle(originalStyle.BasedOn, targetStyle);
            }

            // Copy the setters from the original style
            foreach (var setter in originalStyle.Setters.OfType<Setter>())
            {
                targetStyle.Setters.Add(new Setter(setter.Property, setter.Value));
            }
        }

        #endregion
    }
}
