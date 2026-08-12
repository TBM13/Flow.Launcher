using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Flow.Launcher.PluginSDK.UI.Converters;

/// <summary>
/// Converts a bound value to a <see cref="Visibility"/> depending on whether it
/// equals the value passed in <see cref="IValueConverter"/>'s parameter.
/// <para/>
/// By default the value becomes <see cref="Visibility.Visible"/> when it equals the parameter;
/// setting <see cref="IsInverted"/> to <see langword="true"/> flips the result, so it becomes
/// <see cref="Visibility.Collapsed"/> when equal.
/// </summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public class ValueToVisibilityConverter : IValueConverter
{
    public bool IsInverted { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isMatch = Equals(value, parameter);

        return (isMatch ^ IsInverted) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}