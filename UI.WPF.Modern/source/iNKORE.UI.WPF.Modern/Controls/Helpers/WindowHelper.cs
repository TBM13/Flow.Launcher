using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;
using iNKORE.UI.WPF.Helpers;
using iNKORE.UI.WPF.Modern.Controls.Primitives;
using iNKORE.UI.WPF.Modern.Helpers.Styles;

namespace iNKORE.UI.WPF.Modern.Controls.Helpers;

public static class WindowHelper
{
    //private const string DefaultWindowStyleKey = "DefaultWindowStyle";
    private const string TheWindowStyleKey = "TheWindowStyle";
    //private const string SnapWindowStyleKey = "SnapWindowStyle";

    #region UseModernWindowStyle

    public static readonly DependencyProperty UseModernWindowStyleProperty =
        DependencyProperty.RegisterAttached(
            "UseModernWindowStyle",
            typeof(bool),
            typeof(WindowHelper),
            new PropertyMetadata(OnUseModernWindowStyleChanged));

    public static bool GetUseModernWindowStyle(Window window)
    {
        return (bool)window.GetValue(UseModernWindowStyleProperty);
    }

    public static void SetUseModernWindowStyle(Window window, bool value)
    {
        window.SetValue(UseModernWindowStyleProperty, value);
    }

    private static void OnUseModernWindowStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        bool newValue = (bool)e.NewValue;

        if (DesignerProperties.GetIsInDesignMode(d))
        {
            if (d is Control control)
            {
                if (newValue)
                {
                    if (control.TryFindResource(TheWindowStyleKey) is Style style)
                    {
                        var dStyle = new Style();

                        foreach (Setter setter in style.Setters)
                        {
                            if (setter.Property == Control.BackgroundProperty ||
                                setter.Property == Control.ForegroundProperty)
                            {
                                dStyle.Setters.Add(setter);
                            }
                        }

                        control.Style = dStyle;
                    }
                }
                else
                {
                    control.ClearValue(FrameworkElement.StyleProperty);
                }
            }
        }
        else
        {
            var window = (Window)d;
            SetWindowStyle(window);
        }

    }

    #endregion



    #region CornerStyle

    public static readonly DependencyProperty CornerStyleProperty =
        DependencyProperty.RegisterAttached(
            "CornerStyle",
            typeof(WindowCornerStyle),
            typeof(WindowHelper),
            new PropertyMetadata(WindowCornerStyle.Default, OnCornerStyleChanged));

    private static void OnCornerStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Window window)
        {
            CornerHelper.SetWindowCorners(window, (WindowCornerStyle)e.NewValue);
            UpdateShouldDisplayManualBorder(window);

        }
    }

    public static WindowCornerStyle GetCornerStyle(Window window)
    {
        return (WindowCornerStyle)window.GetValue(CornerStyleProperty);
    }

    public static void SetCornerStyle(Window window, WindowCornerStyle value)
    {
        window.SetValue(CornerStyleProperty, value);
    }


    #endregion

    #region ApplyBackground

    public static readonly DependencyProperty ApplyBackgroundProperty =
        DependencyProperty.RegisterAttached(
            "ApplyBackground",
            typeof(bool),
            typeof(WindowHelper),
            new PropertyMetadata(true));

    public static bool GetApplyBackground(Window window)
    {
        return (bool)window.GetValue(ApplyBackgroundProperty);
    }

    public static void SetApplyBackground(Window window, bool value)
    {
        window.SetValue(ApplyBackgroundProperty, value);
    }


    #endregion

    #region ApplyNoise

    public static readonly DependencyProperty ApplyNoiseProperty =
        DependencyProperty.RegisterAttached(
            "ApplyNoise",
            typeof(bool),
            typeof(WindowHelper),
            new PropertyMetadata(false));

    public static bool GetApplyNoise(Window window)
    {
        return (bool)window.GetValue(ApplyNoiseProperty);
    }

    public static void SetApplyNoise(Window window, bool value)
    {
        window.SetValue(ApplyNoiseProperty, value);
    }


    #endregion

    #region ShouldDisplayManualBorder

    public static readonly DependencyPropertyKey ShouldDisplayManualBorderPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "ShouldDisplayManualBorder",
            typeof(bool),
            typeof(WindowHelper),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShouldDisplayManualBorderProperty = ShouldDisplayManualBorderPropertyKey.DependencyProperty;

    public static bool GetShouldDisplayManualBorder(Window window)
    {
        return (bool)window.GetValue(ShouldDisplayManualBorderProperty);
    }

    private static void SetShouldDisplayManualBorder(Window window, bool value)
    {
        window.SetValue(ShouldDisplayManualBorderPropertyKey, value);
    }

    public static void UpdateShouldDisplayManualBorder(Window window)
    {
        if (window == null)
        {
            return;
        }

        var isOsBorderPresent = OSVersionHelper.IsWindows11OrGreater;

        var newValue = !isOsBorderPresent;
        SetShouldDisplayManualBorder(window, newValue);
    }

    #endregion


    #region FixMaximizedWindow

    public static readonly DependencyProperty FixMaximizedWindowProperty =
        DependencyProperty.RegisterAttached(
            "FixMaximizedWindow",
            typeof(bool),
            typeof(WindowHelper),
            new PropertyMetadata(false, OnFixMaximizedWindowChanged));

    public static bool GetFixMaximizedWindow(Window window)
    {
        return (bool)window.GetValue(FixMaximizedWindowProperty);
    }

    public static void SetFixMaximizedWindow(Window window, bool value)
    {
        window.SetValue(FixMaximizedWindowProperty, value);
    }

    private static void OnFixMaximizedWindowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Window window)
        {
            if ((bool)e.NewValue)
            {
                MaximizedWindowFixer.SetMaximizedWindowFixer(window, new MaximizedWindowFixer());
            }
            else
            {
                window.ClearValue(MaximizedWindowFixer.MaximizedWindowFixerProperty);
            }
        }
    }

    #endregion

    public static void SetWindowStyle(Window window)
    {
        bool isModern = DependencyPropertyHelper.GetValueSource(window, UseModernWindowStyleProperty).BaseValueSource != BaseValueSource.Default && GetUseModernWindowStyle(window);

        void ApplyDarkMode()
        {
            var theme = ThemeManager.GetActualTheme(window);

            bool IsDark(ElementTheme theme)
            {
                return theme == ElementTheme.Default
                    ? ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark
                    : theme == ElementTheme.Dark;
            }

            try
            {
                if (IsDark(theme))
                {
                    window.ApplyDarkMode();
                }
                else
                {
                    window.RemoveDarkMode();
                }
            }
            catch { }
        }

        var handler = new RoutedEventHandler((sender, e) => ApplyDarkMode());

        WindowResizeModeDescriptor.RemoveValueChanged(window, OnWindowResizeModeDescriptorValueChanged);
        ThemeManager.RemoveActualThemeChangedHandler(window, handler);

        if (isModern)
        {
            ApplyDarkMode();

            void onLoaded(object sender, RoutedEventArgs e)
            {
                // This is needed to fix the issue with the window not being loaded correctly
                WindowChrome.SetWindowChrome(window, (WindowChrome.GetWindowChrome(window)?.Clone() as WindowChrome) ?? WindowChrome.GetWindowChrome(window));

                window.RemoveTitleBar();
            }


            if (window.IsLoaded)
            {
                onLoaded(null, null);
            }
            else
            {

                window.Loaded -= onLoaded;
                window.Loaded += onLoaded;
            }

            ThemeManager.AddActualThemeChangedHandler(window, handler);

            WindowResizeModeDescriptor.AddValueChanged(window, OnWindowResizeModeDescriptorValueChanged);

            window.SetResourceReference(FrameworkElement.StyleProperty, TheWindowStyleKey);
        }
        else
        {
            window.ClearValue(FrameworkElement.StyleProperty);
            window.RemoveDarkMode();
        }

        UpdateWindowChrome(window);
        UpdateShouldDisplayManualBorder(window);
    }


    #region Chrome Management

    static DependencyPropertyDescriptor WindowResizeModeDescriptor = DependencyPropertyDescriptor.FromProperty(Window.ResizeModeProperty, typeof(Window));

    private static void OnWindowResizeModeDescriptorValueChanged(object sender, EventArgs e)
    {
        if (sender is Window win)
        {
            UpdateWindowChrome(win);
        }
    }


    public static WindowChrome UpdateWindowChrome(this Window window)
    {
        if (window == null)
        {
            return null;
        }

        var chrome = WindowChrome.GetWindowChrome(window);

        if (GetUseModernWindowStyle(window))
        {
            if (chrome == null)
            {
                chrome = new WindowChrome()
                {
                    CornerRadius = new CornerRadius(0),
                    NonClientFrameEdges = NonClientFrameEdges.None,
                    UseAeroCaptionButtons = false
                };
            }
            // -----------------------------
            // Resize border thickness
            // -----------------------------

            var isResizable = true;
            switch (window.ResizeMode)
            {
                case ResizeMode.NoResize:
                case ResizeMode.CanMinimize:
                    isResizable = false;
                    break;
                case ResizeMode.CanResize:
                case ResizeMode.CanResizeWithGrip:
                    isResizable = true;
                    break;
            }

            var resizeBorderThickness = isResizable ? new Thickness(4) : new Thickness(0);

            if (chrome.ResizeBorderThickness != resizeBorderThickness)
                chrome.ResizeBorderThickness = resizeBorderThickness;

            if (TitleBar.GetResizeBorderThickness(window) != resizeBorderThickness)
                TitleBar.SetResizeBorderThickness(window, new Thickness(
                    resizeBorderThickness.Left - 1,
                    resizeBorderThickness.Top - 1,
                    resizeBorderThickness.Right - 1,
                    0));

            // -----------------------------
            // Caption height
            // -----------------------------

            var captionHeight = TitleBar.GetHeight(window);

            if (chrome.CaptionHeight != captionHeight)
                chrome.CaptionHeight = captionHeight;


            // -----------------------------
            // Glass frame thickness
            // -----------------------------

            var glassFrameThickness = new Thickness(-1);
            if (chrome.GlassFrameThickness != glassFrameThickness)
                chrome.GlassFrameThickness = glassFrameThickness;


            // Final

            WindowChrome.SetWindowChrome(window, chrome);
        }

        return chrome;
    }


    #endregion
}
