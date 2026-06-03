using System.Windows;
using System.Windows.Interop;
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

    /// <summary>
    /// Transforms pixels to Device Independent Pixels used by WPF
    /// </summary>
    /// <param name="visual">current window, required to get presentation source</param>
    /// <param name="unitX">horizontal position in pixels</param>
    /// <param name="unitY">vertical position in pixels</param>
    /// <returns>point containing device independent pixels</returns>
    public static Point TransformPixelsToDIP(Visual visual, double unitX, double unitY)
    {
        Matrix matrix;
        var source = PresentationSource.FromVisual(visual);
        if (source is not null)
        {
            matrix = source.CompositionTarget.TransformFromDevice;
        }
        else
        {
            using var src = new HwndSource(new HwndSourceParameters());
            matrix = src.CompositionTarget.TransformFromDevice;
        }

        return new Point((int)(matrix.M11 * unitX), (int)(matrix.M22 * unitY));
    }
}
