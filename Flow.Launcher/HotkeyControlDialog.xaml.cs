using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core;
using Flow.Launcher.Infrastructure.Hotkeys;
using iNKORE.UI.WPF.Modern.Controls;

namespace Flow.Launcher;

public partial class HotkeyControlDialog : ContentDialog
{
    private static readonly Settings _hotkeySettings = Ioc.Default.GetRequiredService<Settings>();
    private Action? _overwriteOtherHotkey;
    private string DefaultHotkey { get; }
    public string WindowTitle { get; }
    public Hotkey? CurrentHotkey { get; private set; }
    public ObservableCollection<string> KeysToDisplay { get; } = new();

    public enum EResultType
    {
        Cancel,
        Save,
        Delete
    }

    public EResultType ResultType { get; private set; } = EResultType.Cancel;
    public string ResultValue { get; private set; } = string.Empty;
    public static string EmptyHotkey => Localize.none();

    private static bool isOpenFlowHotkey;

    public HotkeyControlDialog(string hotkey, string defaultHotkey, string windowTitle = "")
    {
        WindowTitle = windowTitle switch
        {
            "" or null => Localize.hotkeyRegTitle(),
            _ => windowTitle
        };
        DefaultHotkey = defaultHotkey;
        CurrentHotkey = Hotkey.FromString(hotkey);
        SetKeysToDisplay(CurrentHotkey);

        InitializeComponent();

        HashSet<Hotkey> blockExceptions = [
            new Hotkey() { MainKey = Key.Escape, Modifiers = ModifierKeys.None }
        ];

        // TODO: Handle key press
        GlobalHotkeyManager.BlockAllKeys(blockExceptions, null);
    }

    private void Reset(object sender, RoutedEventArgs routedEventArgs)
    {
        SetKeysToDisplay(Hotkey.FromString(DefaultHotkey));
    }

    private void Delete(object sender, RoutedEventArgs routedEventArgs)
    {
        KeysToDisplay.Clear();
        KeysToDisplay.Add(EmptyHotkey);
    }

    private void Cancel(object sender, RoutedEventArgs routedEventArgs)
    {
        GlobalHotkeyManager.ReleaseAllKeys();

        ResultType = EResultType.Cancel;
        Hide();
    }

    private void Save(object sender, RoutedEventArgs routedEventArgs)
    {
        GlobalHotkeyManager.ReleaseAllKeys();

        if (KeysToDisplay.Count == 1 && KeysToDisplay[0] == EmptyHotkey)
        {
            ResultType = EResultType.Delete;
            Hide();
            return;
        }
        ResultType = EResultType.Save;
        ResultValue = string.Join("+", KeysToDisplay);
        Hide();
    }

    // TODO: Depending on if we are editing a WPF hotkey or system-wide hotkey (like open flow hotkey),
    // use WPF like now or use ChefKeysManager with all keys blocked
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        //when alt is pressed, the real key should be e.SystemKey
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        /* if (ChefKeysManager.StartMenuBlocked && key.ToString() == ChefKeysManager.STARTMENU_SIMULATED_KEY)
             return;*/

        PressedKeys pressedKeys = GlobalHotkeyManager.GetPressedKeys();
        Hotkey? hotkey = pressedKeys.ToHotkey();

        CurrentHotkey = hotkey;
        SetKeysToDisplay(CurrentHotkey);
    }

    private void SetKeysToDisplay(Hotkey? hotkey)
    {
        // TODO
        /*_overwriteOtherHotkey = null;
        KeysToDisplay.Clear();

        if (!hotkey.HasValue || !hotkey.Value.IsValid)
        {
            KeysToDisplay.Add(EmptyHotkey);
            return;
        }

        foreach (var key in hotkey.Value.ToString().Split('+'))
        {
            KeysToDisplay.Add(key);
        }

        if (tbMsg == null)
            return;

        if (_hotkeySettings.RegisteredHotkeys.FirstOrDefault(v => v.Hotkey == hotkey) is { } registeredHotkeyData)
        {
            var description = string.Format(
                App.API.GetTranslation(registeredHotkeyData.DescriptionResourceKey),
                registeredHotkeyData.DescriptionFormatVariables
            );
            Alert.Visibility = Visibility.Visible;
            if (registeredHotkeyData.RemoveHotkey is not null)
            {
                tbMsg.Text = Localize.hotkeyUnavailableEditable(description);
                SaveBtn.IsEnabled = false;
                SaveBtn.Visibility = Visibility.Collapsed;
                OverwriteBtn.IsEnabled = true;
                OverwriteBtn.Visibility = Visibility.Visible;
                _overwriteOtherHotkey = registeredHotkeyData.RemoveHotkey;
            }
            else
            {
                tbMsg.Text = Localize.hotkeyUnavailableUneditable(description);
                SaveBtn.IsEnabled = false;
                SaveBtn.Visibility = Visibility.Visible;
                OverwriteBtn.IsEnabled = false;
                OverwriteBtn.Visibility = Visibility.Collapsed;
            }
            return;
        }

        OverwriteBtn.IsEnabled = false;
        OverwriteBtn.Visibility = Visibility.Collapsed;

        if (!CheckHotkeyAvailability(hotkey.Value))
        {
            tbMsg.Text = Localize.hotkeyUnavailable();
            Alert.Visibility = Visibility.Visible;
            SaveBtn.IsEnabled = false;
            SaveBtn.Visibility = Visibility.Visible;
        }
        else
        {
            Alert.Visibility = Visibility.Collapsed;
            SaveBtn.IsEnabled = true;
            SaveBtn.Visibility = Visibility.Visible;
        }*/
    }

    private static bool CheckHotkeyAvailability(Hotkey hotkey)
    {
        // TODO
        // return HotkeyManager.CheckAvailability(hotkey);
        return true;
    }

    private void Overwrite(object sender, RoutedEventArgs e)
    {
        _overwriteOtherHotkey?.Invoke();
        Save(sender, e);
    }
}
