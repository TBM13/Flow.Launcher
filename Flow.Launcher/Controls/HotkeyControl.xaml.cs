using System.Collections.ObjectModel;
using System.Windows;
using Flow.Launcher.Dialogs;
using Flow.Launcher.PluginSDK.Hotkeys;

namespace Flow.Launcher.Controls;

public partial class HotkeyControl
{
    public static readonly DependencyProperty HotkeyInfoProperty = DependencyProperty.Register(
        nameof(HotkeyInfo),
        typeof(HotkeyInfo),
        typeof(HotkeyControl),
        new PropertyMetadata(null, OnHotkeyInfoChanged)
    );

    public static readonly DependencyProperty KeysProperty = DependencyProperty.Register(
        nameof(Keys),
        typeof(string),
        typeof(HotkeyControl),
        new PropertyMetadata(string.Empty, OnKeysChanged)
    );

    public HotkeyInfo? HotkeyInfo
    {
        get => (HotkeyInfo?)GetValue(HotkeyInfoProperty);
        set => SetValue(HotkeyInfoProperty, value);
    }

    public string Keys
    {
        get => (string)GetValue(KeysProperty);
        set => SetValue(KeysProperty, value);
    }

    private readonly ObservableCollection<string> _keysToDisplay = [];

    public HotkeyControl()
    {
        InitializeComponent();
        KeysList.ItemsSource = _keysToDisplay;
        UpdateUI();
    }

    private static void OnHotkeyInfoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyControl control)
            control.UpdateUI();
    }

    private static void OnKeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyControl control)
            control.UpdateUI();
    }

    public void GetNewHotkey(object sender, RoutedEventArgs e)
    {
        _ = OpenHotkeyDialogAsync();
    }

    private async Task OpenHotkeyDialogAsync()
    {
        if (HotkeyInfo is null)
            return;

        HotkeyControlDialog dialog = new(HotkeyInfo)
        {
            Owner = Window.GetWindow(this)
        };

        await dialog.ShowAsync();
        UpdateUI();
    }

    private void UpdateUI()
    {
        _keysToDisplay.Clear();

        HotkeyButton.IsEnabled = HotkeyInfo is not null;

        if (HotkeyInfo is not null)
        {
            if (!HotkeyInfo.Hotkey.IsValid)
            {
                _keysToDisplay.Add("None");
                return;
            }

            foreach (string key in HotkeyInfo.Hotkey.ToString().Split('+'))
                _keysToDisplay.Add(key);
            return;
        }

        if (string.IsNullOrEmpty(Keys))
        {
            _keysToDisplay.Add("None");
            return;
        }

        foreach (string key in Keys.Split('+'))
            _keysToDisplay.Add(key);
    }
}
