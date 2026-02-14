using System.Windows;
using System.Windows.Input;

namespace Flow.Launcher.Infrastructure.UI;

public class KeyBinding : System.Windows.Input.KeyBinding
{
    public static readonly DependencyProperty BindableGestureProperty =
        DependencyProperty.Register(
            nameof(BindableGesture),
            typeof(KeyGesture),
            typeof(KeyBinding),
            new PropertyMetadata(null, OnBindableGestureChanged));

    public KeyGesture? BindableGesture
    {
        get => (KeyGesture?)GetValue(BindableGestureProperty);
        set => SetValue(BindableGestureProperty, value);
    }

    private static void OnBindableGestureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyBinding binding && e.NewValue is KeyGesture newGesture)
        {
            binding.Gesture = newGesture;
        }
    }
}
