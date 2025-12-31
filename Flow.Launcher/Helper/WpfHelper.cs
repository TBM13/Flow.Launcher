using System.Windows;
using System.Windows.Media;

namespace Flow.Launcher.Helper;

public static class WpfHelper
{
    public static T? FindVisualChild<T>(DependencyObject dep) where T : DependencyObject
    {
        if (dep is null)
            return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
        {
            var child = VisualTreeHelper.GetChild(dep, i);
            if (child is T t)
                return t;

            var res = FindVisualChild<T>(child);
            if (res is not null)
                return res;
        }

        return null;
    }
}
