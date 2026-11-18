using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Flow.Launcher.PluginSDK.UI;

[ContentProperty(nameof(Content))]
public class SettingsExpander : ItemsControl
{
    static SettingsExpander()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SettingsExpander),
            new FrameworkPropertyMetadata(typeof(SettingsExpander)));
    }

    public static readonly RoutedEvent ExpandedEvent = EventManager.RegisterRoutedEvent(
        nameof(Expanded),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(SettingsExpander));

    public event RoutedEventHandler Expanded
    {
        add => AddHandler(ExpandedEvent, value);
        remove => RemoveHandler(ExpandedEvent, value);
    }

    public static readonly RoutedEvent CollapsedEvent = EventManager.RegisterRoutedEvent(
        nameof(Collapsed),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(SettingsExpander));

    public event RoutedEventHandler Collapsed
    {
        add => AddHandler(CollapsedEvent, value);
        remove => RemoveHandler(CollapsedEvent, value);
    }

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header),
        typeof(string),
        typeof(SettingsExpander),
        new PropertyMetadata(null, OnHeaderChanged));

    public string? Header
    {
        get => (string?)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    private static readonly DependencyPropertyKey HasHeaderPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasHeader),
        typeof(bool),
        typeof(SettingsExpander),
        new PropertyMetadata(false));

    public static readonly DependencyProperty HasHeaderProperty = HasHeaderPropertyKey.DependencyProperty;

    public bool HasHeader => (bool)GetValue(HasHeaderProperty);

    private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsExpander expander)
        {
            expander.SetValue(HasHeaderPropertyKey, !string.IsNullOrEmpty((string?)e.NewValue));
        }
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(SettingsExpander),
        new PropertyMetadata(null, OnDescriptionChanged));

    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    private static readonly DependencyPropertyKey HasDescriptionPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasDescription),
        typeof(bool),
        typeof(SettingsExpander),
        new PropertyMetadata(false));

    public static readonly DependencyProperty HasDescriptionProperty = HasDescriptionPropertyKey.DependencyProperty;

    public bool HasDescription => (bool)GetValue(HasDescriptionProperty);

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsExpander expander)
        {
            expander.SetValue(HasDescriptionPropertyKey, !string.IsNullOrEmpty((string?)e.NewValue));
        }
    }

    public static readonly DependencyProperty HeaderIconProperty = DependencyProperty.Register(
        nameof(HeaderIcon),
        typeof(object),
        typeof(SettingsExpander),
        new PropertyMetadata(null, OnHeaderIconChanged));

    public object? HeaderIcon
    {
        get => GetValue(HeaderIconProperty);
        set => SetValue(HeaderIconProperty, value);
    }

    private static readonly DependencyPropertyKey HasHeaderIconPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasHeaderIcon),
        typeof(bool),
        typeof(SettingsExpander),
        new PropertyMetadata(false));

    public static readonly DependencyProperty HasHeaderIconProperty = HasHeaderIconPropertyKey.DependencyProperty;

    public bool HasHeaderIcon => (bool)GetValue(HasHeaderIconProperty);

    private static void OnHeaderIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsExpander expander)
        {
            expander.SetValue(HasHeaderIconPropertyKey, e.NewValue is not null);
        }
    }

    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content),
        typeof(object),
        typeof(SettingsExpander),
        new PropertyMetadata(null));

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(SettingsExpander),
        new PropertyMetadata(new CornerRadius(5)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded),
        typeof(bool),
        typeof(SettingsExpander),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsExpandedChanged));

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsExpander expander)
        {
            bool expanded = (bool)e.NewValue;
            expander.RaiseEvent(new RoutedEventArgs(expanded ? ExpandedEvent : CollapsedEvent, expander));
        }
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is FrameworkElement;
    }

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is SettingsCard card)
        {
            card.CornerRadius = new CornerRadius(0);
            card.BorderThickness = new Thickness(0, 1, 0, 0);
        }
    }
}
