using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media.Effects;
using System.Windows.Shell;
using System.Windows.Threading;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Core.Resource
{
    public class Theme
    {
        #region Properties & Fields

        private readonly string ClassName = nameof(Theme);

        private const int ShadowExtraMargin = 32;

        private readonly IPublicAPI _api;
        private readonly Settings _settings;
        private ResourceDictionary _oldResource;
        private string _oldTheme;
        private const string Extension = ".xaml";
        private static string DirectoryPath => Path.Combine(Constant.ProgramDirectory, Constant.Themes);

        private Thickness _themeResizeBorderThickness;

        #endregion

        #region Constructor

        public Theme(IPublicAPI publicAPI, Settings settings)
        {
            _api = publicAPI;
            _settings = settings;

            var dicts = Application.Current.Resources.MergedDictionaries;
            _oldResource = dicts.FirstOrDefault(d =>
            {
                if (d.Source == null) return false;

                var p = d.Source.AbsolutePath;
                return p.Contains(Constant.Themes) && Path.GetExtension(p) == Extension;
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

        private string GetThemePath(string themeName)
        {
            string path = Path.Combine(DirectoryPath, themeName + Extension);
            if (File.Exists(path))
            {
                return path;
            }

            return string.Empty;
        }

        #endregion

        #region Change Theme
        public bool ChangeTheme(string? theme = null)
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
                    _api.ShowMsgBox(Localize.Theme_LoadFailure_PathNotExists(theme));
                    ChangeTheme(Constant.DefaultTheme);
                }
                return false;
            }
            catch (XamlParseException e)
            {
                _api.LogException(ClassName, $"Theme <{theme}> fail to parse xaml", e);
                if (theme != Constant.DefaultTheme)
                {
                    _api.ShowMsgBox(Localize.Theme_LoadFailure_ParseError(theme));
                    ChangeTheme(Constant.DefaultTheme);
                }
                return false;
            }
            catch (Exception e)
            {
                _api.LogException(ClassName, $"Theme <{theme}> fail to load", e);
                if (theme != Constant.DefaultTheme)
                {
                    _api.ShowMsgBox(Localize.Theme_LoadFailure_ParseError(theme));
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
