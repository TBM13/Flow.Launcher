using System;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Hotkey.ChefKeys;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Helper;

internal static class HotKeyMapper
{
#pragma warning disable CS8618
    private static Settings _settings;
    private static MainViewModel _mainViewModel;
#pragma warning restore CS8618

    internal static void Initialize()
    {
        _mainViewModel = Ioc.Default.GetRequiredService<MainViewModel>();
        _settings = Ioc.Default.GetRequiredService<Settings>();

        SetHotkey(_settings.Hotkey, OnToggleHotkey);
        LoadCustomPluginHotkey();
    }

    internal static void OnToggleHotkey()
    {
        if (!_mainViewModel.ShouldIgnoreHotkeys())
            _mainViewModel.ToggleFlowLauncher();
    }

    internal static void SetHotkey(string hotkey, Action action)
    {
        HotkeyModel model = new(hotkey);
        SetHotkey(model.ToSequence(), action);
    }
    internal static void SetHotkey(KeySequence hotkey, Action action)
    {
        ChefKeysManager.RegisterHotkey(hotkey, action);
    }

    internal static void RemoveHotkey(string hotkey)
    {
        HotkeyModel model = new(hotkey);
        RemoveHotkey(model.ToSequence());
    }
    internal static void RemoveHotkey(KeySequence hotkey)
    {
        ChefKeysManager.UnregisterHotkey(hotkey);
    }

    internal static void LoadCustomPluginHotkey()
    {
        if (_settings.CustomPluginHotkeys == null)
            return;

        foreach (CustomPluginHotkey hotkey in _settings.CustomPluginHotkeys)
        {
            SetCustomQueryHotkey(hotkey);
        }
    }

    internal static void SetCustomQueryHotkey(CustomPluginHotkey hotkey)
    {
        SetHotkey(hotkey.Hotkey, () =>
        {
            if (_mainViewModel.ShouldIgnoreHotkeys())
                return;

            App.API.ShowMainWindow();
            // Make sure to go back to the query results page first since it can cause issues if current page is context menu
            App.API.BackToQueryResults();
            App.API.ChangeQuery(hotkey.ActionKeyword, true);
        });
    }

    internal static bool CheckAvailability(KeySequence hotkey)
    {
        var res = ChefKeysManager.CanRegisterHotkey(hotkey);
        return res;
    }
}
