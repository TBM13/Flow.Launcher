using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Effects;
using System.Windows.Shell;
using System.Windows.Threading;
using Flow.Launcher.Infrastructure;

namespace Flow.Launcher.Core.Resource
{
    public class Theme(Settings settings)
    {
        private const int ShadowExtraMargin = 32;
        private static readonly string DefaultThemePath = $@"{Constant.ProgramDirectory}\{Constant.Themes}\{Constant.DefaultTheme}.xaml";

        private readonly Settings _settings = settings;
        private ResourceDictionary? _oldResource;
        private Thickness _themeResizeBorderThickness;

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

        private ResourceDictionary GetResourceDictionary()
        {
            if (!File.Exists(DefaultThemePath))
                throw new FileNotFoundException($"Theme can't be found <{DefaultThemePath}>");

            var dict = new ResourceDictionary
            {
                Source = new Uri(DefaultThemePath, UriKind.Absolute)
            };

            /* Ignore Theme Window Width and use setting */
            Style windowStyle = dict["WindowStyle"] as Style ?? throw new NullReferenceException("WindowStyle not found in resource dictionary.");
            double width = _settings.WindowSize;
            windowStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty, width));
            return dict;
        }

        public bool ChangeTheme()
        {
            // Retrieve theme resource – always use the resource with font settings applied.
            var resourceDict = GetResourceDictionary();
            UpdateResourceDictionary(resourceDict);

            // Apply drop shadow effect so that we do not need to call it again
            _ = RefreshFrameAsync();

            return true;
        }

        #region Shadow Effect

        public void AddDropShadowEffectToCurrentTheme()
        {
            var dict = GetResourceDictionary();

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
            var dict = GetResourceDictionary();
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

        #endregion
    }
}
