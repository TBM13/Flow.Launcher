using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Flow.Launcher.Interop;

namespace Flow.Launcher.Helper;

/// <summary>
/// Attached properties for enabling Windows 11 rounded corners and Acrylic backdrop blur on WPF <see cref="Popup"/> and <see cref="ContextMenu"/> controls.
/// </summary>
public static class PopupHelper
{
    private static readonly MethodInfo? s_hookupParentPopup = typeof(ContextMenu).GetMethod(
        "HookupParentPopup",
        BindingFlags.Instance | BindingFlags.NonPublic);
    public static readonly DependencyProperty UseSystemBackdropProperty = DependencyProperty.RegisterAttached(
        "UseSystemBackdrop",
        typeof(bool),
        typeof(PopupHelper),
        new PropertyMetadata(false, OnUseSystemBackdropChanged));

    public static bool GetUseSystemBackdrop(DependencyObject obj) => (bool)obj.GetValue(UseSystemBackdropProperty);

    public static void SetUseSystemBackdrop(DependencyObject obj, bool value) => obj.SetValue(UseSystemBackdropProperty, value);

    private static void OnUseSystemBackdropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!WindowHelper.SupportsAcrylicPopup)
            return;

        if (d is Popup popup)
        {
            popup.Loaded -= OnPopupLoaded;
            popup.Unloaded -= OnPopupUnloaded;
            popup.Opened -= OnPopupOpened;
            if (popup.Child is not null)
                PresentationSource.RemoveSourceChangedHandler(popup.Child, OnChildSourceChanged);

            if ((bool)e.NewValue)
            {
                popup.AllowsTransparency = false;
                popup.Loaded += OnPopupLoaded;
                popup.Unloaded += OnPopupUnloaded;
                popup.Opened += OnPopupOpened;

                if (popup.IsLoaded && popup.Child is not null)
                    PresentationSource.AddSourceChangedHandler(popup.Child, OnChildSourceChanged);
            }
        }
        else if (d is ContextMenu menu)
        {
            menu.Initialized -= OnContextMenuInitialized;
            menu.Opened -= OnContextMenuOpened;
            PresentationSource.RemoveSourceChangedHandler(menu, OnChildSourceChanged);

            if ((bool)e.NewValue)
            {
                menu.Initialized += OnContextMenuInitialized;
                menu.Opened += OnContextMenuOpened;
                PresentationSource.AddSourceChangedHandler(menu, OnChildSourceChanged);

                // HookupParentPopup mutates logical Parent and would trigger a cyclic Style error if run mid-UpdateStyleProperty.
                if (menu.IsInitialized)
                    menu.Dispatcher.BeginInvoke(DispatcherPriority.Send, () => EnsureNonLayeredContextMenuPopup(menu));
            }
        }
    }

    private static void OnContextMenuInitialized(object? sender, EventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            menu.Initialized -= OnContextMenuInitialized;
            EnsureNonLayeredContextMenuPopup(menu);
        }
    }

    private static void EnsureNonLayeredContextMenuPopup(ContextMenu menu)
    {
        if (!GetUseSystemBackdrop(menu) || menu.IsOpen)
            return;

        // ContextMenu forces WS_EX_LAYERED on its internal Popup, letting alpha=0 pixels pass mouse hover to underlying windows.
        if (menu.Parent is null)
            s_hookupParentPopup?.Invoke(menu, null);

        if (menu.Parent is Popup popup && popup.AllowsTransparency && !popup.IsOpen)
            popup.AllowsTransparency = false;
    }

    private static void OnPopupLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Popup popup && popup.Child is not null)
        {
            PresentationSource.RemoveSourceChangedHandler(popup.Child, OnChildSourceChanged);
            PresentationSource.AddSourceChangedHandler(popup.Child, OnChildSourceChanged);
        }
    }

    private static void OnPopupUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Popup popup && popup.Child is not null)
            PresentationSource.RemoveSourceChangedHandler(popup.Child, OnChildSourceChanged);
    }

    private static void OnChildSourceChanged(object? sender, SourceChangedEventArgs e)
    {
        if (e.NewSource is HwndSource hs && sender is DependencyObject element)
            ApplyBackdrop(hs, IsDarkTheme(element));
    }

    private static void OnPopupOpened(object? sender, EventArgs e)
    {
        if (sender is Popup popup && popup.Child is not null && PresentationSource.FromVisual(popup.Child) is HwndSource hs)
            ApplyBackdrop(hs, IsDarkTheme(popup));
    }

    private static void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu && PresentationSource.FromVisual(menu) is HwndSource hs)
            ApplyBackdrop(hs, IsDarkTheme(menu));
    }

    private static void ApplyBackdrop(HwndSource source, bool isDark)
    {
        if (source.Handle == 0)
            return;

        source.CompositionTarget.BackgroundColor = Colors.Transparent;
        WindowHelper.EnablePopupAcrylic(source.Handle, isDark);
    }

    private static bool IsDarkTheme(DependencyObject element)
    {
        object? brush = element is FrameworkElement fe
            ? fe.TryFindResource("TextFillColorPrimaryBrush")
            : Application.Current?.TryFindResource("TextFillColorPrimaryBrush");

        return brush is SolidColorBrush solid && (solid.Color.R + solid.Color.G + solid.Color.B) > 384;
    }
}
