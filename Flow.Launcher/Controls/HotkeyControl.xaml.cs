using System.Collections.ObjectModel;
using System.Windows;
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

    public HotkeyInfo? HotkeyInfo
    {
        get => (HotkeyInfo?)GetValue(HotkeyInfoProperty);
        set => SetValue(HotkeyInfoProperty, value);
    }

    private readonly ObservableCollection<string> _keysToDisplay = [];

    public HotkeyControl()
    {
        InitializeComponent();
        KeysList.ItemsSource = _keysToDisplay;
    }

    private static void OnHotkeyInfoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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
        if (HotkeyInfo is null || !HotkeyInfo.Hotkey.IsValid)
        {
            _keysToDisplay.Add("None");
            return;
        }

        foreach (string key in HotkeyInfo.Hotkey.ToString(includeLongPress: false).Split('+'))
            _keysToDisplay.Add(key);
    }
}
