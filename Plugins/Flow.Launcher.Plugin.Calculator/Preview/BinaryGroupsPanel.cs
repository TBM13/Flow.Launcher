using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.Calculator.Preview;

/// <summary>
/// Lays out bit groups in a single row when they fit, or in a single column otherwise.
/// </summary>
public sealed class BinaryGroupsPanel : Panel
{
    private bool _useSingleRow;

    protected override Size MeasureOverride(Size availableSize)
    {
        double totalWidth = 0;
        double totalHeight = 0;
        double maxWidth = 0;
        double maxHeight = 0;

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            Size desiredSize = child.DesiredSize;
            totalWidth += desiredSize.Width;
            totalHeight += desiredSize.Height;
            maxWidth = Math.Max(maxWidth, desiredSize.Width);
            maxHeight = Math.Max(maxHeight, desiredSize.Height);
        }

        _useSingleRow = totalWidth <= availableSize.Width;
        return _useSingleRow
            ? new Size(totalWidth, maxHeight)
            : new Size(maxWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_useSingleRow)
        {
            double totalWidth = 0;
            foreach (UIElement child in InternalChildren)
                totalWidth += child.DesiredSize.Width;

            double x = Math.Max(0, (finalSize.Width - totalWidth) / 2);
            foreach (UIElement child in InternalChildren)
            {
                child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
                x += child.DesiredSize.Width;
            }
        }
        else
        {
            double y = 0;
            foreach (UIElement child in InternalChildren)
            {
                double x = Math.Max(0, (finalSize.Width - child.DesiredSize.Width) / 2);
                child.Arrange(new Rect(x, y, child.DesiredSize.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height;
            }
        }

        return finalSize;
    }
}
