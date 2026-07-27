using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Controls;

public partial class HotkeyDisplay : UserControl
{
    private readonly ObservableCollection<string> _values = [];

    public static readonly DependencyProperty KeysProperty =
        DependencyProperty.Register(nameof(Keys), typeof(string), typeof(HotkeyDisplay),
            new PropertyMetadata(string.Empty, keyChanged));

    public string Keys
    {
        get { return (string)GetValue(KeysProperty); }
        set { SetValue(KeysProperty, value); }
    }

    public HotkeyDisplay()
    {
        InitializeComponent();
        KeysList.ItemsSource = _values;
    }

    private static void keyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not string newValue)
            return;

        if (d is not HotkeyDisplay hotkeyDisplay)
            return;

        hotkeyDisplay._values.Clear();
        foreach (string key in newValue.Split('+'))
            hotkeyDisplay._values.Add(key);
    }
}
