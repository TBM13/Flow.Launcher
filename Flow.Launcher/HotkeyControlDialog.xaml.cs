using System.Collections.ObjectModel;
using System.Windows;
using Flow.Launcher.Core;
using Flow.Launcher.Infrastructure.Hotkeys;
using iNKORE.UI.WPF.Modern.Controls;

namespace Flow.Launcher;

public partial class HotkeyControlDialog : ContentDialog
{
    private Hotkey _newHotkey;

    public string WindowTitle { get; }
    public HotkeyInformation Hotkey { get; init; }
    public ObservableCollection<string> KeysToDisplay { get; } = [];

    public HotkeyControlDialog(HotkeyInformation hotkey, string? windowTitle = null)
    {
        WindowTitle = windowTitle ?? Localize.hotkeyRegTitle();
        Hotkey = hotkey;
        _newHotkey = hotkey.Hotkey;

        InitializeComponent();
        UpdateUI();

        GlobalHotkeyManager.IgnoreRegisteredHotkeys = true;
        PreviewKeyDown += (_, e) =>
        {
            // Prevent the key event from being handled by other controls in the dialog
            e.Handled = true;
        };
        PreviewKeyUp += (_, e) =>
        {
            if (GlobalHotkeyManager.LastHotkey.HasValue)
            {
                _newHotkey = GlobalHotkeyManager.LastHotkey.Value with
                {
                    LongPress = LongPressCheckbox.IsChecked!.Value
                };
                UpdateUI();

                e.Handled = true;
            }
        };
    }

    private void Reset(object sender, RoutedEventArgs routedEventArgs)
    {
        _newHotkey = Hotkey.DefaultHotkey;
        UpdateUI();
    }

    private void Delete(object sender, RoutedEventArgs routedEventArgs)
    {
        _newHotkey = default;
        UpdateUI();
    }

    private void Cancel(object sender, RoutedEventArgs routedEventArgs)
    {
        GlobalHotkeyManager.IgnoreRegisteredHotkeys = false;
        Hide();
    }

    private void Save(object sender, RoutedEventArgs routedEventArgs)
    {
        HotkeyManager.UpdateHotkey(Hotkey, _newHotkey);
        GlobalHotkeyManager.IgnoreRegisteredHotkeys = false;
        Hide();
    }

    private void UpdateUI()
    {
        ResetBtn.IsEnabled = _newHotkey != Hotkey.DefaultHotkey;
        DeleteBtn.IsEnabled = _newHotkey.IsValid;

        LongPressCheckbox.Visibility =
            (Hotkey is GlobalHotkeyInformation && _newHotkey != default) ? Visibility.Visible : Visibility.Collapsed;
        LongPressCheckbox.IsChecked = _newHotkey.LongPress;

        KeysToDisplay.Clear();
        if (!_newHotkey.IsValid)
        {
            KeysToDisplay.Add("None");
            return;
        }

        foreach (var key in _newHotkey.ToString(includeLongPress: false).Split('+'))
            KeysToDisplay.Add(key);

        bool hotkeyChanged = _newHotkey != Hotkey.Hotkey;
        if (hotkeyChanged && !HotkeyManager.IsHotkeyAvailable(_newHotkey, out string? reason))
        {
            tbMsg.Text = reason;
            SaveBtn.IsEnabled = false;
            Alert.Visibility = Visibility.Visible;
        }
        else
        {
            SaveBtn.IsEnabled = true;
            Alert.Visibility = Visibility.Collapsed;
        }
    }

    private void LongPressCheckbox_Checked(object sender, RoutedEventArgs e)
    {
        _newHotkey = _newHotkey with
        {
            LongPress = true,
        };
        UpdateUI();
    }

    private void LongPressCheckbox_Unchecked(object sender, RoutedEventArgs e)
    {
        _newHotkey = _newHotkey with
        {
            LongPress = false,
        };
        UpdateUI();
    }
}
