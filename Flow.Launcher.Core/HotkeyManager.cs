using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkeys;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Flow.Launcher.Core;

public static class DefaultHotkeys
{
    public static readonly HotkeyInformation
        // Global Hotkeys
        ToggleFlowLauncher = new("ToggleFlowLauncher", "Alt+Space", "Toggle Flow Launcher"),

        // MainWindow hotkeys
        TogglePreview = new("TogglePreview", "F1", "Toggle preview pane"),
        Autocomplete = new("Autocomplete", "Tab", "Autocomplete"),
        SelectNextResult = new("SelectNextResult", "Down", "Select next result"),
        SelectPreviousResult = new("SelectPreviousResult", "Up", "Select previous result"),
        SelectNextPage = new("SelectNextPage", "PageDown", "Select next page"),
        SelectPreviousPage = new("SelectPreviousPage", "PageUp", "Select previous page"),
        ShowContextMenu = new("ShowContextMenu", "Right", "Show result's context menu"),
        HideContextMenu = new("HideContextMenu", "Left", "Hide result's context menu");

    // TODO: Add more hotkeys
    /*      new ("Escape", "HotkeyESCDesc"),
            new ("F5", "ReloadPluginHotkey"),
            new ("Alt+Home", "HotkeySelectFirstResult"),
            new ("Alt+End", "HotkeySelectLastResult"),
            new ("Ctrl+R", "HotkeyRequery"),
            new ("Ctrl+OemCloseBrackets", "QuickWidthHotkey"),
            new ("Ctrl+OemOpenBrackets", "QuickWidthHotkey"),
            new ("Ctrl+OemPlus", "QuickHeightHotkey"),
            new ("Ctrl+OemMinus", "QuickHeightHotkey"),
            new ("Ctrl+Shift+Enter", "HotkeyCtrlShiftEnterDesc"),
            new ("Shift+Enter", "OpenContextMenuHotkey"),
            new ("Enter", "HotkeyRunDesc"),
            new ("Ctrl+Enter", "OpenContainFolderHotkey"),
            new ("Alt+Enter", "HotkeyOpenResult"),
            new ("Ctrl+F12", "ToggleGameModeHotkey"),
            new ("Ctrl+Shift+C", "CopyFilePathHotkey")
    */

    public static readonly HotkeyInformation[] NonGlobalHotkeys = [
        TogglePreview,
        Autocomplete,
        SelectNextResult,
        SelectPreviousResult,
        SelectNextPage,
        SelectPreviousPage,
        ShowContextMenu,
        HideContextMenu
    ];
}

/// <summary>
/// Manages and keeps track of all the registered hotkeys in Flow Launcher.
/// </summary>
public static class HotkeyManager
{
    private static readonly ILogger Logger = LogManager.GetLogger(nameof(HotkeyManager));

#pragma warning disable CS8618
    private static Settings _settings;
    private static IPublicAPI _api;
#pragma warning restore CS8618

    /// <summary>
    /// Key is the hotkey ID, value is the hotkey information and action (or null if it's a non-global hotkey).
    /// </summary>
    private static readonly Dictionary<string, (HotkeyInformation info, Action? action)> _allHotkeys = [];
    private static readonly Dictionary<Hotkey, HotkeyInformation> _enabledHotkeys = [];

    public static void Initialize()
    {
        _api = Ioc.Default.GetRequiredService<IPublicAPI>();
        _settings = Ioc.Default.GetRequiredService<Settings>();

        // Register global hotkeys
        RegisterHotkey(DefaultHotkeys.ToggleFlowLauncher, () =>
        {
            if (_api.IsMainWindowVisible())
                _api.HideMainWindow();
            else
                _api.ShowMainWindow();
        });
        // Register non-global hotkeys
        foreach (HotkeyInformation hotkey in DefaultHotkeys.NonGlobalHotkeys)
            RegisterHotkey(hotkey, null);

        // Register global custom query hotkeys
        foreach (CustomPluginHotkey hotkey in _settings.CustomPluginHotkeys)
        {
            RegisterCustomQueryHotkey(hotkey);
        }
    }

    private static bool ShouldIgnoreHotkeys()
    {
        return _settings.IgnoreHotkeysOnFullscreen && Win32Helper.IsForegroundWindowFullscreen() || _api.IsGameModeOn();
    }

    private static void RegisterHotkey(HotkeyInformation hotkey, Action? action)
    {
        if (_allHotkeys.TryGetValue(hotkey.Id, out var existing))
            throw new InvalidOperationException($"Hotkey '{hotkey}' has the same ID than already-registered hotkey '{existing.info}'");

        Hotkey disabledHotkey = new()
        {
            MainKey = Key.None,
            Modifiers = ModifierKeys.None
        };

        // Read user hotkey from settings if exists
        if (_settings.Hotkeys.TryGetValue(hotkey.Id, out string? userHotkey))
        {
            Hotkey h = Hotkey.FromString(userHotkey);
            if (h.IsValid)
                hotkey.Hotkey = h;
            else
            {
                // Error if it the value isn't empty (which means disabled)
                if (!string.IsNullOrEmpty(userHotkey))
                    Logger.ZLogError($"Invalid hotkey in user settings for '{hotkey.Id}': {userHotkey}");

                // Disable the hotkey
                hotkey.Hotkey = disabledHotkey;
            }
        }

        // Ensure the hotkey doesn't collide with another one that is enabled
        if (_enabledHotkeys.TryGetValue(hotkey.Hotkey, out var value))
        {
            Logger.ZLogError($"Disabling hotkey '{hotkey}' since it's already assigned to '{value.Id}'");
            hotkey.Hotkey = disabledHotkey;
        }

        // Global hotkey
        if (action is not null)
        {
            var originalAction = action;
            action = () =>
            {
                if (!ShouldIgnoreHotkeys())
                    originalAction.Invoke();
            };

            if (hotkey.Hotkey.IsValid)
            {
                if (!GlobalHotkeyManager.CanRegisterHotkey(hotkey.Hotkey))
                {
                    Logger.ZLogError($"Can't register hotkey '{hotkey}' for '{hotkey.Id}'");
                    hotkey.Hotkey = disabledHotkey;
                }
                else
                    GlobalHotkeyManager.RegisterHotkey(hotkey.Hotkey, action);
            }
        }

        _allHotkeys[hotkey.Id] = (hotkey, action);
        if (hotkey.Hotkey.IsValid)
            _enabledHotkeys[hotkey.Hotkey] = hotkey;
    }

    /// <summary>
    /// Registers the given custom query hotkey.
    /// </summary>
    public static void RegisterCustomQueryHotkey(CustomPluginHotkey hotkey)
    {
        string id = $"CustomQuery {hotkey.ActionKeyword}";
        HotkeyInformation info = new(id, hotkey.Hotkey, $"Custom query \"{hotkey.ActionKeyword}\"");
        RegisterHotkey(info, () =>
        {
            _api.ShowMainWindow();
            // Make sure to go back to the query results page first since it can cause issues if current page is context menu
            _api.BackToQueryResults();
            _api.ChangeQuery(hotkey.ActionKeyword, true);
        });
    }

    public static void UnregisterCustomQueryHotkey(CustomPluginHotkey hotkey)
    {
        string id = $"CustomQuery {hotkey.ActionKeyword}";
        HotkeyInformation info = GetHotkeyInformationById(id);

        if (_enabledHotkeys.ContainsKey(info.Hotkey))
            GlobalHotkeyManager.UnregisterHotkey(info.Hotkey);

        _enabledHotkeys.Remove(info.Hotkey);
        _allHotkeys.Remove(id);
    }

    /// <param name="hotkey"></param>
    /// <param name="reason">The reason why the hotkey is not available.</param>
    /// <returns>True if the hotkey is valid and available.</returns>
    public static bool IsHotkeyAvailable(Hotkey hotkey, [NotNullWhen(false)] out string? reason)
    {
        if (!hotkey.IsValid)
        {
            reason = "Invalid hotkey.";
            return false;
        }
        if (_enabledHotkeys.TryGetValue(hotkey, out var value))
        {
            reason = $"The hotkey is already assigned to \"{value.Description}\".";
            return false;
        }

        reason = null;
        return true;
    }

    public static void UpdateHotkey(HotkeyInformation hotkey, Hotkey newHotkey)
    {
        if (hotkey.Hotkey == newHotkey)
            return;

        if (!IsHotkeyAvailable(newHotkey, out string? reason))
            throw new ArgumentException($"Can't update hotkey '{hotkey}': {reason}");
        if (!_allHotkeys.TryGetValue(hotkey.Id, out var existingHotkey))
            throw new InvalidOperationException($"Tried to update a hotkey that isn't registered: {hotkey.Id} ({hotkey})");
        if (hotkey != existingHotkey.info)
            throw new InvalidOperationException($"Tried to update hotkey '{hotkey}' but the one registered is '{existingHotkey.info}'");

        bool wasEnabled = _enabledHotkeys.Remove(hotkey.Hotkey);
        hotkey.Hotkey = newHotkey;
        _enabledHotkeys[newHotkey] = hotkey;

        if (existingHotkey.action is not null)
        {
            if (wasEnabled)
                GlobalHotkeyManager.UnregisterHotkey(hotkey.Hotkey);

            GlobalHotkeyManager.RegisterHotkey(newHotkey, existingHotkey.action);
        }
    }

    public static HotkeyInformation GetHotkeyInformationById(string id)
    {
        if (_allHotkeys.TryGetValue(id, out var hotkey))
            return hotkey.info;

        throw new KeyNotFoundException($"No hotkey registered with ID '{id}'");
    }
}
