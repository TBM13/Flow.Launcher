using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Resources.Controls;

public class CardGroupCardStyleSelector : StyleSelector
{
    public Style FirstStyle { get; set; }
    public Style MiddleStyle { get; set; }
    public Style LastStyle { get; set; }

    public override Style SelectStyle(object item, DependencyObject container)
    {
        var itemsControl = ItemsControl.ItemsControlFromItemContainer(container);
        var index = itemsControl.ItemContainerGenerator.IndexFromContainer(container);

        Card card = item as Card;
        card.Loaded += (_, _) =>
        {
            Border border = (Border)card.Template.FindName("BD", card);
            border.Margin = new Thickness(0, 0, 0, 0);
            border.CornerRadius = new CornerRadius(0);
            border.Background = System.Windows.Media.Brushes.Transparent;
            border.BorderThickness = new Thickness(0, 1, 0, 0);

            if (index == 0)
                border.BorderThickness = new Thickness(0, 0, 0, 0);
        };

        if (index == 0)
            return FirstStyle;

        if (index == itemsControl.Items.Count - 1) return LastStyle;
        return MiddleStyle;
    }
}
