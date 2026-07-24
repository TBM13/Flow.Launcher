using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Flow.Launcher.PluginSDK.WPF.MarkupExtensions;

[MarkupExtensionReturnType(typeof(Visibility))]
public class VisibleWhenExtension(Binding when) : CollapsedWhenExtension(when)
{
    protected override Visibility DefaultVisibility => Visibility.Collapsed;
    protected override Visibility InvertedVisibility => Visibility.Visible;
}
