using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Flow.Launcher.Helper;

public static class WpfHelper
{
    public static readonly DependencyProperty DisableMouseWheelSelectionProperty = DependencyProperty.RegisterAttached(
        "DisableMouseWheelSelection",
        typeof(bool),
        typeof(WpfHelper),
        new PropertyMetadata(false, OnDisableMouseWheelSelectionChanged));

    public static bool GetDisableMouseWheelSelection(DependencyObject obj) => (bool)obj.GetValue(DisableMouseWheelSelectionProperty);

    public static void SetDisableMouseWheelSelection(DependencyObject obj, bool value) => obj.SetValue(DisableMouseWheelSelectionProperty, value);

    private static void OnDisableMouseWheelSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox comboBox)
            return;

        comboBox.PreviewMouseWheel -= OnComboBoxPreviewMouseWheel;
        if ((bool)e.NewValue)
            comboBox.PreviewMouseWheel += OnComboBoxPreviewMouseWheel;
    }

    private static void OnComboBoxPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ComboBox comboBox || e.Handled || comboBox.IsDropDownOpen)
            return;

        // Swallow tunneling wheel on closed ComboBox so ComboBox.OnMouseWheel cannot switch selection, then bubble to parent ScrollViewer.
        e.Handled = true;
        if (FindVisualParentInputElement(comboBox) is { } parent)
        {
            MouseWheelEventArgs routedEventArgs = new(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = comboBox
            };
            parent.RaiseEvent(routedEventArgs);
        }
    }

    private static IInputElement? FindVisualParentInputElement(DependencyObject child)
    {
        DependencyObject? current = VisualTreeHelper.GetParent(child);
        while (current is not null)
        {
            if (current is IInputElement inputElement)
                return inputElement;
            current = VisualTreeHelper.GetParent(current);
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
