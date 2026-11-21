using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flow.Launcher.PluginSDK.UI;

public enum SettingsCardContentAlignment
{
    Right,
    Vertical
}

public class SettingsCard : ContentControl
{
    static SettingsCard()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SettingsCard),
            new FrameworkPropertyMetadata(typeof(SettingsCard)));
    }

    public SettingsCard()
    {
        Loaded += (_, _) => UpdateIsNested();
    }

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header),
        typeof(string),
        typeof(SettingsCard),
        new PropertyMetadata(null, OnHeaderChanged));

    public string? Header
    {
        get => (string?)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    private static readonly DependencyPropertyKey HasHeaderPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasHeader),
        typeof(bool),
        typeof(SettingsCard),
        new PropertyMetadata(false));

    public static readonly DependencyProperty HasHeaderProperty = HasHeaderPropertyKey.DependencyProperty;

    public bool HasHeader => (bool)GetValue(HasHeaderProperty);

    private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsCard card)
        {
            card.SetValue(HasHeaderPropertyKey, !string.IsNullOrEmpty((string?)e.NewValue));
        }
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(SettingsCard),
        new PropertyMetadata(null, OnDescriptionChanged));

    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    private static readonly DependencyPropertyKey HasDescriptionPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasDescription),
        typeof(bool),
        typeof(SettingsCard),
        new PropertyMetadata(false));

    public static readonly DependencyProperty HasDescriptionProperty = HasDescriptionPropertyKey.DependencyProperty;

    public bool HasDescription => (bool)GetValue(HasDescriptionProperty);

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsCard card)
        {
            card.SetValue(HasDescriptionPropertyKey, !string.IsNullOrEmpty((string?)e.NewValue));
        }
    }

    public static readonly DependencyProperty HeaderIconProperty = DependencyProperty.Register(
        nameof(HeaderIcon),
        typeof(object),
        typeof(SettingsCard),
        new PropertyMetadata(null, OnHeaderIconChanged));

    public object? HeaderIcon
    {
        get => GetValue(HeaderIconProperty);
        set => SetValue(HeaderIconProperty, value);
    }

    private static readonly DependencyPropertyKey HasHeaderIconPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasHeaderIcon),
        typeof(bool),
        typeof(SettingsCard),
        new PropertyMetadata(false));

    public static readonly DependencyProperty HasHeaderIconProperty = HasHeaderIconPropertyKey.DependencyProperty;

    public bool HasHeaderIcon => (bool)GetValue(HasHeaderIconProperty);

    private static void OnHeaderIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsCard card)
        {
            card.SetValue(HasHeaderIconPropertyKey, e.NewValue is not null);
        }
    }

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(SettingsCard),
        new PropertyMetadata(new CornerRadius(5)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly DependencyProperty ContentAlignmentProperty = DependencyProperty.Register(
        nameof(ContentAlignment),
        typeof(SettingsCardContentAlignment),
        typeof(SettingsCard),
        new PropertyMetadata(SettingsCardContentAlignment.Right));

    public SettingsCardContentAlignment ContentAlignment
    {
        get => (SettingsCardContentAlignment)GetValue(ContentAlignmentProperty);
        set => SetValue(ContentAlignmentProperty, value);
    }

    private static readonly DependencyPropertyKey IsNestedPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsNested),
        typeof(bool),
        typeof(SettingsCard),
        new PropertyMetadata(false));

    public static readonly DependencyProperty IsNestedProperty = IsNestedPropertyKey.DependencyProperty;

    public bool IsNested => (bool)GetValue(IsNestedProperty);

    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        UpdateIsNested();
    }

    private void UpdateIsNested()
    {
        SetValue(IsNestedPropertyKey, HasSettingsContainerAncestor(this));
    }

    internal static bool HasSettingsContainerAncestor(DependencyObject element)
    {
        DependencyObject? current = VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element);
        while (current is not null)
        {
            if (current is SettingsExpander or SettingsCard)
                return true;

            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }

        return false;
    }
}
