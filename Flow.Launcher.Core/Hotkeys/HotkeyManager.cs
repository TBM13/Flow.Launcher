using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Flow.Launcher.Infrastructure.API;
using Flow.Launcher.Infrastructure.Hotkeys;
using Flow.Launcher.Infrastructure.Plugins;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Interop;
using Flow.Launcher.PluginSDK.Logging;

namespace Flow.Launcher.Core;

/// <summary>
/// Manages and keeps track of all the registered hotkeys in Flow Launcher.
/// </summary>
public class HotkeyManager : IDisposable
{
    private readonly Logger<HotkeyManager> _logger;
    private readonly Settings _settings;
    private readonly KeyboardManager _keyboardManager = new();

    /// <summary>
    /// All the registered hotkeys. Key is hotkey ID.
    /// <para/>
    /// Value is a tuple with the plugin ID and hotkey info.
    /// </summary>
    private readonly Dictionary<string, (string? pluginId, HotkeyInfo info)> _allHotkeys = [];

    // Enabled hotkeys
    private readonly Dictionary<Hotkey, GlobalHotkeyInfo> _enabledGlobalHotkeys = [];
    private readonly Dictionary<Hotkey, AppHotkeyInfo> _enabledAppHotkeys = [];
    /// <summary>
    /// Key is plugin ID.
    /// </summary>
    private readonly Dictionary<string, Dictionary<Hotkey, ResultHotkeyInfo>> _enabledResultHotkeys = [];

    /// <summary>
    /// If true, global hotkeys will not be triggered.
    /// </summary>
    public bool IgnoreGlobalHotkeys { get; set; }

    /// <summary>
    /// The last global hotkey combination that was pressed, regardless of whether it is a registered hotkey or not.
    /// </summary>
    public Hotkey? LastGlobalHotkey { get; private set; }

    public HotkeyManager(Logger<HotkeyManager> logger, Settings settings)
    {
        _logger = logger;
        _settings = settings;

        // Register global hotkeys
        foreach (GlobalHotkeyInfo hotkey in DefaultHotkeys.GlobalHotkeys)
            RegisterHotkey(hotkey, null);
        // Register app hotkeys
        foreach (AppHotkeyInfo hotkey in DefaultHotkeys.AppHotkeys)
            RegisterHotkey(hotkey, null);

        // Register global custom query hotkeys
        foreach (CustomPluginHotkey hotkey in _settings.CustomPluginHotkeys)
        {
            RegisterCustomQueryHotkey(hotkey);
        }

        // Start listening for hotkeys
        _keyboardManager.OnHotkeyTriggered += OnGlobalHotkeyTriggered;
        _keyboardManager.Initialize();
    }

    private bool ShouldIgnoreHotkeys()
    {
        return (_settings.IgnoreHotkeysOnFullscreen && WindowHelper.IsForegroundWindowFullscreen())
            || IPublicAPI.Instance.IsGameModeOn();
    }

    private bool OnGlobalHotkeyTriggered(Hotkey hotkey)
    {
        LastGlobalHotkey = hotkey;

        if (IgnoreGlobalHotkeys)
            return false;
        if (ShouldIgnoreHotkeys())
            return false;
        if (!_enabledGlobalHotkeys.TryGetValue(hotkey, out GlobalHotkeyInfo? hotkeyInfo))
            return false;

        bool isWindowsKey = hotkey.MainKey == Key.None && hotkey.Modifiers == ModifierKeys.Windows;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            if (isWindowsKey)
            {
                // Since we blocked the Windows key's KEYUP event, the OS will think it's still being
                // held down and will trigger respective shortcuts when another key is pressed.
                // To prevent this, simulate Alt Key press to trigger Windows + Alt shortcut (which does nothing).
                KeyboardManager.SimulateKeyEvent(Key.LeftAlt, isKeyUp: false);

                // Simulate release of both the Windows and Alt keys
                KeyboardManager.SimulateKeyEvent(Key.LWin, isKeyUp: true);
                KeyboardManager.SimulateKeyEvent(Key.LeftAlt, isKeyUp: true);
            }

            hotkeyInfo.OnHotkeyTriggered.Invoke();
        });

        if (isWindowsKey)
        {
            // Block Windows key's KEYUP event
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether the given global hotkey is valid and does not conflict with any other hotkey.
    /// </summary>
    /// <param name="reason">The reason why the hotkey is not available.</param>
    public bool IsGlobalHotkeyAvailable(Hotkey hotkey, [NotNullWhen(false)] out string? reason)
    {
        if (!hotkey.IsValid)
        {
            reason = "Invalid hotkey";
            return false;
        }
        if (hotkey.Modifiers == ModifierKeys.None)
        {
            reason = "Global hotkeys must contain at least one modifier (CTRL/ALT/SHIFT/WINDOWS)";
            return false;
        }

        // Check for conflicting global hotkey
        HotkeyInfo? existing = _enabledGlobalHotkeys.GetValueOrDefault(hotkey);
        // Check for conflicting app or result hotkey
        hotkey = hotkey with { LongPress = false };
        existing ??= _enabledAppHotkeys.GetValueOrDefault(hotkey);
        existing ??= _enabledResultHotkeys.Values
            .Select(resultHotkeys => resultHotkeys.GetValueOrDefault(hotkey))
            .FirstOrDefault(info => info is not null);

        if (existing is not null)
        {
            reason = $"Hotkey already used by \"{existing.Name}\"";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// Checks whether the given app hotkey is valid and does not conflict with any other hotkey.
    /// </summary>
    /// <param name="reason">The reason why the hotkey is not available.</param>
    public bool IsAppHotkeyAvailable(Hotkey hotkey, [NotNullWhen(false)] out string? reason)
    {
        if (!hotkey.IsValid || hotkey.LongPress)
        {
            reason = "Invalid hotkey";
            return false;
        }

        // Check for conflicting app or result hotkey
        HotkeyInfo? existing = _enabledAppHotkeys.GetValueOrDefault(hotkey);
        existing ??= _enabledResultHotkeys.Values
            .Select(resultHotkeys => resultHotkeys.GetValueOrDefault(hotkey))
            .FirstOrDefault(info => info is not null);

        // Check for conflicting global hotkey, with long press set or not
        existing ??= _enabledGlobalHotkeys.GetValueOrDefault(hotkey);
        hotkey = hotkey with { LongPress = true };
        existing ??= _enabledGlobalHotkeys.GetValueOrDefault(hotkey);

        if (existing is not null)
        {
            reason = $"Hotkey already used by \"{existing.Name}\"";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// Checks whether a plugin's result hotkey is valid and does not conflict with any other hotkey.
    /// </summary>
    /// <param name="reason">The reason why the hotkey is not available.</param>
    public bool IsResultHotkeyAvailable(string pluginId, Hotkey hotkey, [NotNullWhen(false)] out string? reason)
    {
        if (!hotkey.IsValid || hotkey.LongPress)
        {
            reason = "Invalid hotkey";
            return false;
        }

        // Check for conflicting app hotkey
        HotkeyInfo? existing = _enabledAppHotkeys.GetValueOrDefault(hotkey);
        // Check for conflicting result hotkey from the same plugin
        if (_enabledResultHotkeys.TryGetValue(pluginId, out var pluginResultHotkeys))
            existing ??= pluginResultHotkeys.GetValueOrDefault(hotkey);

        // Check for conflicting global hotkey, with long press set or not
        existing ??= _enabledGlobalHotkeys.GetValueOrDefault(hotkey);
        hotkey = hotkey with { LongPress = true };
        existing ??= _enabledGlobalHotkeys.GetValueOrDefault(hotkey);

        if (existing is not null)
        {
            reason = $"Hotkey already used by \"{existing.Name}\"";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// Returns a valid hotkey that should be used for the given hotkey info,
    /// or <see langword="default"/> if the hotkey is disabled.
    /// </summary>
    private Hotkey GetHotkey(HotkeyInfo hotkeyInfo)
    {
        // Read user hotkey from settings
        if (_settings.Hotkeys.TryGetValue(hotkeyInfo.Id, out string? userHotkey))
        {
            userHotkey = userHotkey.Trim();
            if (string.IsNullOrEmpty(userHotkey) && hotkeyInfo.CanBeDisabled)
                // An empty string means the hotkey is disabled
                return default;

            if (Hotkey.TryParse(userHotkey, out Hotkey h))
                return h;

            _logger.LogError($"Invalid hotkey '{userHotkey}' for {hotkeyInfo}. Falling back to default");
        }

        if (!hotkeyInfo.DefaultHotkey.IsValid)
        {
            _logger.LogError($"The default hotkey for {hotkeyInfo} is invalid: {hotkeyInfo.DefaultHotkey}");
            return default;
        }

        return hotkeyInfo.DefaultHotkey;
    }

    public void RegisterHotkey(GlobalHotkeyInfo hotkeyInfo, PluginMetadata? plugin) => RegisterHotkeyInternal(hotkeyInfo, plugin);
    public void RegisterHotkey(AppHotkeyInfo hotkeyInfo, PluginMetadata? plugin) => RegisterHotkeyInternal(hotkeyInfo, plugin);
    public void RegisterHotkey(ResultHotkeyInfo hotkeyInfo, PluginMetadata plugin) => RegisterHotkeyInternal(hotkeyInfo, plugin);
    private void RegisterHotkeyInternal(HotkeyInfo hotkeyInfo, PluginMetadata? plugin)
    {
        // Check there are no registered hotkeys with the same ID
        if (_allHotkeys.ContainsKey(hotkeyInfo.Id))
        {
            _logger.LogError($"Failed to register hotkey {hotkeyInfo}: ID already registered");
            return;
        }

        // Register hotkey
        _allHotkeys[hotkeyInfo.Id] = new(plugin?.ID, hotkeyInfo);

        // Get the hotkey that should be used
        Hotkey hotkey = GetHotkey(hotkeyInfo);
        if (!hotkey.IsValid)
            return;     // Hotkey is disabled or invalid

        // Enable the hotkey
        UpdateHotkey(hotkeyInfo.Id, hotkey);
    }

    public void UnregisterHotkey(string hotkeyId)
    {
        // Get registered hotkey
        if (!_allHotkeys.ContainsKey(hotkeyId))
        {
            _logger.LogError($"Cannot un-register non-registered hotkey '{hotkeyId}'");
            return;
        }

        // Disable hotkey
        UpdateHotkey(hotkeyId, default);
        _allHotkeys.Remove(hotkeyId);
    }

    public void UpdateHotkey(string hotkeyId, Hotkey newHotkey)
    {
        // Get registered hotkey
        if (!_allHotkeys.TryGetValue(hotkeyId, out var registeredHotkey))
        {
            _logger.LogError($"Cannot update non-registered hotkey '{hotkeyId}'");
            return;
        }
        // Don't do anything if the new hotkey is the same
        if (registeredHotkey.info.Hotkey == newHotkey)
            return;

        // Handle disabling hotkey
        bool disabling = newHotkey == default;
        if (disabling)
        {
            if (!registeredHotkey.info.CanBeDisabled)
                _logger.LogError($"Tried to disable non-disablable hotkey '{registeredHotkey.info}'");
            else
            {
                registeredHotkey.info.Hotkey = default;
                switch (registeredHotkey.info)
                {
                    case GlobalHotkeyInfo globalInfo:
                        _enabledGlobalHotkeys.Remove(globalInfo.Hotkey);
                        break;
                    case AppHotkeyInfo appInfo:
                        _enabledAppHotkeys.Remove(appInfo.Hotkey);
                        break;
                    case ResultHotkeyInfo resultInfo:
                        _enabledResultHotkeys[registeredHotkey.pluginId!].Remove(resultInfo.Hotkey);
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            return;
        }

        // Ensure the hotkey does not conflict with an existing one
        string? nonAvailabilityReason = null;
        bool hotkeyAvailable = disabling || registeredHotkey.info switch
        {
            GlobalHotkeyInfo => IsGlobalHotkeyAvailable(newHotkey, out nonAvailabilityReason),
            AppHotkeyInfo => IsAppHotkeyAvailable(newHotkey, out nonAvailabilityReason),
            ResultHotkeyInfo => IsResultHotkeyAvailable(registeredHotkey.pluginId!, newHotkey, out nonAvailabilityReason),
            _ => throw new InvalidOperationException()
        };
        if (!hotkeyAvailable)
        {
            _logger.LogError(
                $"Cannot update hotkey '{registeredHotkey.info}' to '{newHotkey}': {nonAvailabilityReason}");
            return;
        }

        // Update & enable hotkey
        registeredHotkey.info.Hotkey = newHotkey;
        switch (registeredHotkey.info)
        {
            case GlobalHotkeyInfo globalInfo:
                _enabledGlobalHotkeys[newHotkey] = globalInfo;
                break;
            case AppHotkeyInfo appInfo:
                _enabledAppHotkeys[newHotkey] = appInfo;
                break;
            case ResultHotkeyInfo resultInfo:
                if (!_enabledResultHotkeys.TryGetValue(registeredHotkey.pluginId!, out var pluginResultHotkeys))
                {
                    pluginResultHotkeys = [];
                    _enabledResultHotkeys[registeredHotkey.pluginId!] = pluginResultHotkeys;
                }
                pluginResultHotkeys[newHotkey] = resultInfo;
                break;
            default:
                throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Registers the given custom query hotkey.
    /// </summary>
    public void RegisterCustomQueryHotkey(CustomPluginHotkey queryHotkeyInfo)
    {
        Hotkey hotkey = default;
        if (!string.IsNullOrEmpty(queryHotkeyInfo.Hotkey))
        {
            if (!Hotkey.TryParse(queryHotkeyInfo.Hotkey, out hotkey))
            {
                _logger.LogError($"Invalid hotkey '{queryHotkeyInfo.Hotkey}' for custom query \"{queryHotkeyInfo.ActionKeyword}\"");
                return;
            }
        }

        GlobalHotkeyInfo info = new()
        {
            Id = $"CustomQuery {queryHotkeyInfo.ActionKeyword}",
            Name = $"Custom query \"{queryHotkeyInfo.ActionKeyword}\"",
            DefaultHotkey = hotkey,
            OnHotkeyTriggered = () =>
            {
                IPublicAPI.Instance.ShowMainWindow();
                // Make sure to go back to the query results page first since it can cause issues if current page is context menu
                IPublicAPI.Instance.BackToQueryResults();
                IPublicAPI.Instance.ChangeQuery(queryHotkeyInfo.ActionKeyword, true);
            }
        };

        RegisterHotkey(info, null);
    }

    public void UnregisterCustomQueryHotkey(CustomPluginHotkey hotkey)
    {
        string id = $"CustomQuery {hotkey.ActionKeyword}";
        UnregisterHotkey(id);
    }

    /// <summary>
    /// Gets the hotkey information for the given hotkey ID.
    /// </summary>
    /// <exception cref="KeyNotFoundException"></exception>
    public HotkeyInfo GetHotkeyInformation(string id)
    {
        if (_allHotkeys.TryGetValue(id, out var hotkeyInfo))
            return hotkeyInfo.info;

        throw new KeyNotFoundException($"Hotkey '{id}' not registered");
    }

    /// <inheritdoc cref="KeyboardManager.GetPressedKeys"/>
    public PressedKeys GetPressedKeys()
    {
        return _keyboardManager.GetPressedKeys();
    }

    public void Dispose()
    {
        _keyboardManager.OnHotkeyTriggered -= OnGlobalHotkeyTriggered;
        _keyboardManager.Dispose();
    }
}
