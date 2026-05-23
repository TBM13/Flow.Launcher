using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Infrastructure.Hotkeys;

namespace Flow.Launcher.Infrastructure.WPF;

public class KeyBinding : System.Windows.Input.KeyBinding
{
    public static readonly DependencyProperty HotkeyProperty =
        DependencyProperty.Register(
            nameof(Hotkey),
            typeof(Hotkey),
            typeof(KeyBinding),
            new PropertyMetadata(new Hotkey() { MainKey = Key.None, Modifiers = ModifierKeys.None }, OnHotkeyChanged));

    public Hotkey Hotkey
    {
        get => (Hotkey)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyBinding binding && e.NewValue is Hotkey hotkey)
        {
            binding.Key = hotkey.MainKey;
            binding.Modifiers = hotkey.Modifiers;
        }
    }
}
