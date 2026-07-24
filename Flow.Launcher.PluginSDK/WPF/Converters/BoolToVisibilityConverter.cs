using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Flow.Launcher.PluginSDK.WPF.Converters;

/// <summary>
/// Converts a bool to a <see cref="Visibility"/> value
/// (true becomes <see cref="Visibility.Visibility"/> and false <see cref="Visibility.Collapsed"/>).
/// <para/>
/// If the parameter is set to "Invert" or <see langword="true"/>, the conversion is inverted.
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolVal = value is true;
        bool isInverted = IsInverted(parameter);

        return (boolVal ^ isInverted) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool boolVal = visibility == Visibility.Visible;
            bool isInverted = IsInverted(parameter);

            return boolVal ^ isInverted;
        }

        return Binding.DoNothing;
    }

    private static bool IsInverted(object parameter) => parameter switch
    {
        bool b => b,
        string s => string.Equals(s, "Invert", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "True", StringComparison.OrdinalIgnoreCase),
        _ => false
    };
}
