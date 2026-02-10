using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Hotkey.ChefKeys;
using Flow.Launcher.Infrastructure.UserSettings;
using iNKORE.UI.WPF.Modern.Controls;

namespace Flow.Launcher;

public partial class HotkeyControlDialog : ContentDialog
{
    private static readonly IHotkeySettings _hotkeySettings = Ioc.Default.GetRequiredService<Settings>();
    private Action? _overwriteOtherHotkey;
    private string DefaultHotkey { get; }
    public string WindowTitle { get; }
    public HotkeyModel CurrentHotkey { get; private set; }
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
        CurrentHotkey = new HotkeyModel(hotkey);
        SetKeysToDisplay(CurrentHotkey);

        InitializeComponent();

        HashSet<KeySequence> blockExceptions = [
            new KeySequence() { Keys = [Key.Escape] }
        ];

        // TODO: Handle key press
        ChefKeysManager.BlockAllKeys(blockExceptions, null);
    }

    private void Reset(object sender, RoutedEventArgs routedEventArgs)
    {
        SetKeysToDisplay(new HotkeyModel(DefaultHotkey));
    }

    private void Delete(object sender, RoutedEventArgs routedEventArgs)
    {
        KeysToDisplay.Clear();
        KeysToDisplay.Add(EmptyHotkey);
    }

    private void Cancel(object sender, RoutedEventArgs routedEventArgs)
    {
        ChefKeysManager.ReleaseAllKeys();

        ResultType = EResultType.Cancel;
        Hide();
    }

    private void Save(object sender, RoutedEventArgs routedEventArgs)
    {
        ChefKeysManager.ReleaseAllKeys();

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

        SpecialKeyState specialKeyState = GlobalHotkey.CheckModifiers();

        var hotkeyModel = new HotkeyModel(
            specialKeyState.AltPressed,
            specialKeyState.ShiftPressed,
            specialKeyState.WinPressed,
            specialKeyState.CtrlPressed,
            key);

        CurrentHotkey = hotkeyModel;
        SetKeysToDisplay(CurrentHotkey);
    }

    private void SetKeysToDisplay(HotkeyModel? hotkey)
    {
        _overwriteOtherHotkey = null;
        KeysToDisplay.Clear();

        if (hotkey == null || hotkey == default(HotkeyModel))
        {
            KeysToDisplay.Add(EmptyHotkey);
            return;
        }

        foreach (var key in hotkey.Value.EnumerateDisplayKeys()!)
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

        if (!CheckHotkeyAvailability(hotkey.Value, true))
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
        }
    }

    private static bool CheckHotkeyAvailability(HotkeyModel hotkey, bool validateKeyGesture)
    {
        if (isOpenFlowHotkey && (hotkey.ToString() == "LWin" || hotkey.ToString() == "RWin"))
            return true;

        return hotkey.Validate(validateKeyGesture) && HotKeyMapper.CheckAvailability(hotkey.ToSequence());
    }

    private void Overwrite(object sender, RoutedEventArgs e)
    {
        _overwriteOtherHotkey?.Invoke();
        Save(sender, e);
    }
}
