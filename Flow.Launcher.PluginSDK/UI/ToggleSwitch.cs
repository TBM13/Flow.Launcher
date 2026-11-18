using System.Windows;
using System.Windows.Controls.Primitives;

namespace Flow.Launcher.PluginSDK.UI;

public class ToggleSwitch : ToggleButton
{
    static ToggleSwitch()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(typeof(ToggleSwitch)));
    }

    public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
        nameof(IsOn),
        typeof(bool),
        typeof(ToggleSwitch),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOnChanged));

    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    private static void OnIsOnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ToggleSwitch toggleSwitch)
        {
            toggleSwitch.IsChecked = (bool)e.NewValue;
        }
    }

    protected override void OnChecked(RoutedEventArgs e)
    {
        SetCurrentValue(IsOnProperty, true);
        base.OnChecked(e);
    }

    protected override void OnUnchecked(RoutedEventArgs e)
    {
        SetCurrentValue(IsOnProperty, false);
        base.OnUnchecked(e);
    }
}
