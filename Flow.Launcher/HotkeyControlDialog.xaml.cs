using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Hotkeys;
using Flow.Launcher.PluginSDK.Hotkeys;
using iNKORE.UI.WPF.Modern.Controls;

namespace Flow.Launcher;

public partial class HotkeyControlDialog : ContentDialog
{
    // TODO: Check if there is any better alternative
    private readonly HotkeyManager _hotkeyManager = Ioc.Default.GetRequiredService<HotkeyManager>();
    private Hotkey _newHotkey;

    public string WindowTitle { get; }
    public HotkeyInfo Hotkey { get; init; }
    public ObservableCollection<string> KeysToDisplay { get; } = [];

    public HotkeyControlDialog(HotkeyInfo hotkey)
    {
        WindowTitle = hotkey.Name;
        Hotkey = hotkey;
        _newHotkey = hotkey.Hotkey;

        InitializeComponent();
        UpdateUI();

        _hotkeyManager.IgnoreGlobalHotkeys = true;
        PreviewKeyDown += (_, e) =>
        {
            // Prevent the key event from being handled by other controls in the dialog
            e.Handled = true;
        };
        PreviewKeyUp += (_, e) =>
        {
            // TODO: This is fine for global hotkeys, but WPF hotkeys should use the future wpf hotkey manager
            // Also we should probably make LastGlobalHotkey an observable property and subscribe to its changes
            if (_hotkeyManager.LastGlobalHotkey.HasValue)
            {
                _newHotkey = _hotkeyManager.LastGlobalHotkey.Value;
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
        _hotkeyManager.IgnoreGlobalHotkeys = false;
        Hide();
    }

    private void Save(object sender, RoutedEventArgs routedEventArgs)
    {
        _hotkeyManager.UpdateHotkey(Hotkey.Id, _newHotkey);
        _hotkeyManager.IgnoreGlobalHotkeys = false;
        Hide();
    }

    private void UpdateUI()
    {
        ResetBtn.IsEnabled = _newHotkey != Hotkey.DefaultHotkey;
        DeleteBtn.IsEnabled = Hotkey.CanBeDisabled && _newHotkey.IsValid;

        KeysToDisplay.Clear();
        if (!_newHotkey.IsValid)
        {
            KeysToDisplay.Add("None");
            SaveBtn.IsEnabled = Hotkey.CanBeDisabled;
            Alert.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var key in _newHotkey.ToString().Split('+'))
            KeysToDisplay.Add(key);

        bool hotkeyChanged = _newHotkey != Hotkey.Hotkey;
        string? unavailabilityReason = null;
        bool hotkeyAvailable = Hotkey switch
        {
            GlobalHotkeyInfo => _hotkeyManager.IsGlobalHotkeyAvailable(_newHotkey, out unavailabilityReason),
            AppHotkeyInfo => _hotkeyManager.IsAppHotkeyAvailable(_newHotkey, out unavailabilityReason),
            // TODO:
            //ResultHotkeyInfo => _hotkeyManager.IsResultHotkeyAvailable(_newHotkey, out unavailabilityReason),
            _ => throw new InvalidOperationException("Unknown hotkey type")
        };

        if (hotkeyChanged && !hotkeyAvailable)
        {
            tbMsg.Text = unavailabilityReason;
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
