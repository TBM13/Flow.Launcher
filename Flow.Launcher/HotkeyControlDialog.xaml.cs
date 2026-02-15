using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
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
                _newHotkey = GlobalHotkeyManager.LastHotkey.Value;
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
        _newHotkey = new()
        {
            MainKey = Key.None,
            Modifiers = ModifierKeys.None,
        };
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

        KeysToDisplay.Clear();
        if (!_newHotkey.IsValid)
        {
            KeysToDisplay.Add("None");
            return;
        }

        foreach (var key in _newHotkey.ToString().Split('+'))
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
}
