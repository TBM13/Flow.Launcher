using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flow.Launcher.PluginSDK.UI;

public class FontIcon : TextBlock
{
    static FontIcon()
    {
        FontFamilyProperty.OverrideMetadata(
            typeof(FontIcon),
            new FrameworkPropertyMetadata(new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets,Segoe UI Symbol")));
        FontSizeProperty.OverrideMetadata(typeof(FontIcon), new FrameworkPropertyMetadata(16.0));
        TextAlignmentProperty.OverrideMetadata(typeof(FontIcon), new FrameworkPropertyMetadata(TextAlignment.Center));
        HorizontalAlignmentProperty.OverrideMetadata(typeof(FontIcon), new FrameworkPropertyMetadata(HorizontalAlignment.Center));
        VerticalAlignmentProperty.OverrideMetadata(typeof(FontIcon), new FrameworkPropertyMetadata(VerticalAlignment.Center));
    }

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(string),
        typeof(FontIcon),
        new PropertyMetadata(string.Empty, OnGlyphChanged));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    private static void OnGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FontIcon icon)
        {
            icon.Text = (string?)e.NewValue ?? string.Empty;
        }
    }
}
