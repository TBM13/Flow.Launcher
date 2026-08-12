using System;
using System.Globalization;
using System.Windows.Data;

namespace Flow.Launcher.Plugin.Program.Views.Converters
{
    public class ProgramSourceStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is true ? Localize.Settings_ProgramEnabled : Localize.Settings_ProgramDisabled;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
