using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using Flow.Launcher.PluginSDK.WPF.Converters;

namespace Flow.Launcher.PluginSDK.WPF.MarkupExtensions;

[MarkupExtensionReturnType(typeof(Visibility))]
public class CollapsedWhenExtension : MarkupExtension
{
    public Binding? When { get; set; }
    public object? IsEqualTo { get; set; }

    public bool? IsEqualToBool
    {
        get => IsEqualTo as bool?;
        set => IsEqualTo = value;
    }

    public int? IsEqualToInt
    {
        get => IsEqualTo as int?;
        set => IsEqualTo = value;
    }

    protected virtual Visibility DefaultVisibility => Visibility.Visible;
    protected virtual Visibility InvertedVisibility => Visibility.Collapsed;

    public CollapsedWhenExtension() { }

    public CollapsedWhenExtension(Binding when)
    {
        When = when;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (When is null)
            return DependencyProperty.UnsetValue;

        var converter = new HideableVisibilityConverter
        {
            DefaultVisibility = DefaultVisibility,
            InvertedVisibility = InvertedVisibility,
            IsEqualTo = IsEqualTo
        };

        if (IsEqualTo is Binding isEqualToBinding)
        {
            var multiBinding = new MultiBinding
            {
                Converter = converter,
                Bindings = { CloneBinding(When), isEqualToBinding }
            };

            return multiBinding.ProvideValue(serviceProvider);
        }

        // Clone When to avoid mutating a potentially sealed or shared Binding instance
        var targetBinding = CloneBinding(When);
        targetBinding.Converter = converter;

        return targetBinding.ProvideValue(serviceProvider);
    }

    private static Binding CloneBinding(Binding source)
    {
        var clone = new Binding
        {
            Path = source.Path,
            Mode = source.Mode,
            UpdateSourceTrigger = source.UpdateSourceTrigger,
            ConverterCulture = source.ConverterCulture,
            ConverterParameter = source.ConverterParameter,
            FallbackValue = source.FallbackValue,
            TargetNullValue = source.TargetNullValue,
            ValidatesOnDataErrors = source.ValidatesOnDataErrors,
            ValidatesOnExceptions = source.ValidatesOnExceptions,
            ValidatesOnNotifyDataErrors = source.ValidatesOnNotifyDataErrors,
            NotifyOnValidationError = source.NotifyOnValidationError,
            StringFormat = source.StringFormat,
            BindingGroupName = source.BindingGroupName,
            IsAsync = source.IsAsync,
            AsyncState = source.AsyncState,
            BindsDirectlyToSource = source.BindsDirectlyToSource,
            Delay = source.Delay,
            XPath = source.XPath,
            NotifyOnSourceUpdated = source.NotifyOnSourceUpdated,
            NotifyOnTargetUpdated = source.NotifyOnTargetUpdated
        };

        if (source.Source != null) clone.Source = source.Source;
        else if (source.RelativeSource != null) clone.RelativeSource = source.RelativeSource;
        else if (source.ElementName != null) clone.ElementName = source.ElementName;

        foreach (var rule in source.ValidationRules)
        {
            clone.ValidationRules.Add(rule);
        }

        return clone;
    }
}
