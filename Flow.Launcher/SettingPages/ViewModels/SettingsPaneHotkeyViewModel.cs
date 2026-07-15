using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Hotkeys;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.Core.UserSettings;
using Flow.Launcher.PluginSDK.API;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneHotkeyViewModel(ISettingsAPI settings) : ObservableObject
{
    // TODO: Check if there is any better alternative
    private readonly HotkeyManager _hotkeyManager = Ioc.Default.GetRequiredService<HotkeyManager>();

    public ISettingsAPI Settings { get; } = settings;

    [ObservableProperty]
    public partial CustomPluginHotkey SelectedCustomPluginHotkey { get; set; }
    [ObservableProperty]
    public partial CustomShortcutModel SelectedCustomShortcut { get; set; }

    [RelayCommand]
    private void CustomHotkeyDelete()
    {
        var item = SelectedCustomPluginHotkey;
        if (item is null)
        {
            IPublicAPI.Instance.ShowMsgBox(Localize.pleaseSelectAnItem());
            return;
        }

        var result = IPublicAPI.Instance.ShowMsgBox(
            Localize.deleteCustomHotkeyWarning(item.Hotkey),
            Localize.delete(),
            MessageBoxButton.YesNo
        );

        if (result is MessageBoxResult.Yes)
        {
            Settings.CustomPluginHotkeys.Remove(item);
            _hotkeyManager.UnregisterCustomQueryHotkey(item);
        }
    }

    [RelayCommand]
    private void CustomHotkeyEdit()
    {
        var item = SelectedCustomPluginHotkey;
        if (item is null)
        {
            IPublicAPI.Instance.ShowMsgBox(Localize.pleaseSelectAnItem());
            return;
        }

        var settingItem = Settings.CustomPluginHotkeys.FirstOrDefault(o =>
            o.ActionKeyword == item.ActionKeyword && o.Hotkey == item.Hotkey);
        if (settingItem == null)
        {
            IPublicAPI.Instance.ShowMsgBox(Localize.invalidPluginHotkey());
            return;
        }

        var window = new CustomQueryHotkeySetting(settingItem);
        if (window.ShowDialog() is not true) return;

        var index = Settings.CustomPluginHotkeys.IndexOf(settingItem);
        if (index >= 0 && index < Settings.CustomPluginHotkeys.Count)
        {
            Settings.CustomPluginHotkeys[index] = new CustomPluginHotkey(window.Hotkey, window.ActionKeyword);
            // TODO
            /*HotkeyManager.UnregisterGlobalHotkey(settingItem.Hotkey); // remove origin hotkey
            HotkeyManager.RegisterCustomQueryHotkey(Settings.CustomPluginHotkeys[index]); // set new hotkey*/
        }
    }

    [RelayCommand]
    private void CustomHotkeyAdd()
    {
        var window = new CustomQueryHotkeySetting();
        if (window.ShowDialog() is true)
        {
            var customHotkey = new CustomPluginHotkey(window.Hotkey, window.ActionKeyword);
            Settings.CustomPluginHotkeys.Add(customHotkey);
            _hotkeyManager.RegisterCustomQueryHotkey(customHotkey); // set new hotkey
        }
    }

    [RelayCommand]
    private void CustomShortcutDelete()
    {
        var item = SelectedCustomShortcut;
        if (item is null)
        {
            IPublicAPI.Instance.ShowMsgBox(Localize.pleaseSelectAnItem());
            return;
        }

        var result = IPublicAPI.Instance.ShowMsgBox(
            Localize.deleteCustomShortcutWarning(item.Key, item.Value),
            Localize.delete(),
            MessageBoxButton.YesNo
        );

        if (result is MessageBoxResult.Yes)
        {
            Settings.CustomShortcuts.Remove(item);
        }
    }

    [RelayCommand]
    private void CustomShortcutEdit()
    {
        var item = SelectedCustomShortcut;
        if (item is null)
        {
            IPublicAPI.Instance.ShowMsgBox(Localize.pleaseSelectAnItem());
            return;
        }

        var settingItem = Settings.CustomShortcuts.FirstOrDefault(o =>
            o.Key == item.Key && o.Value == item.Value);
        if (settingItem == null)
        {
            IPublicAPI.Instance.ShowMsgBox(Localize.invalidShortcut());
            return;
        }

        var window = new CustomShortcutSetting(settingItem.Key, settingItem.Value, this);
        if (window.ShowDialog() is not true) return;

        var index = Settings.CustomShortcuts.IndexOf(settingItem);
        if (index >= 0 && index < Settings.CustomShortcuts.Count)
        {
            Settings.CustomShortcuts[index] = new CustomShortcutModel(window.Key, window.Value);
        }
    }

    [RelayCommand]
    private void CustomShortcutAdd()
    {
        var window = new CustomShortcutSetting(this);
        if (window.ShowDialog() is true)
        {
            var shortcut = new CustomShortcutModel(window.Key, window.Value);
            Settings.CustomShortcuts.Add(shortcut);
        }
    }

    internal bool DoesShortcutExist(string key)
    {
        return Settings.CustomShortcuts.Any(v => v.Key == key) ||
               Settings.BuiltinShortcuts.Any(v => v.Key == key);
    }
}
