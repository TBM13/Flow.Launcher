using System;
using System.Diagnostics;
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

        ChefKeysManager.Start();
        SetHotkey(_settings.Hotkey, OnToggleHotkey);
        LoadCustomPluginHotkey();
    }

    internal static void OnToggleHotkey()
    {
        if (!_mainViewModel.ShouldIgnoreHotkeys())
            _mainViewModel.ToggleFlowLauncher();
    }

    private static void SetHotkey(string hotkeyStr, Action action)
    {
        var hotkey = new HotkeyModel(hotkeyStr);
        SetHotkey(hotkey, action);
    }

    internal static void SetHotkey(HotkeyModel hotkey, Action action)
    {
        string hotkeyStr = hotkey.ToString();
        Trace.WriteLine($"Registering hotkey: {hotkey}");
        ChefKeysManager.RegisterHotkey(hotkeyStr, action);
    }

    internal static void RemoveHotkey(string hotkeyStr)
    {
        Trace.WriteLine($"Unregistering hotkey: {hotkeyStr}");
        ChefKeysManager.UnregisterHotkey(hotkeyStr);
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

    internal static bool CheckAvailability(HotkeyModel currentHotkey)
    {
        var res = ChefKeysManager.IsAvailable(currentHotkey.ToString());
        Trace.WriteLine($"Checking availability for hotkey: {currentHotkey}, result: {res}");
        return res;
    }
}
