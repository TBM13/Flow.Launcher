using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Flow.Launcher.AdminHelper.IPC;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.Interop;
using Flow.Launcher.Interop.Input;
using Flow.Launcher.PluginSDK.Hotkeys;
using Flow.Launcher.PluginSDK.Logging;
using Flow.Launcher.PluginSDK.Plugins;

namespace Flow.Launcher.Core.Hotkeys;

/// <summary>
/// Manages and keeps track of all the registered hotkeys in Flow Launcher.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private readonly Logger<HotkeyManager> _logger;
    private readonly ISettingsAPI _settings;
    private readonly KeyboardIpcClient _keyboardClient = new();

    private static readonly Hotkey _startMenuHotkey = new(modifiers: ModifierKeys.Windows);
    private Hotkey _lastGlobalHotkey;
    private readonly Lock _lastGlobalHotkeyLock = new();

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

    public HotkeyManager(Logger<HotkeyManager> logger, ISettingsAPI settings)
    {
        _logger = logger;
        _settings = settings;

        _keyboardClient.InitializeAsync().GetAwaiter().GetResult();

        // Register global hotkeys
        foreach (GlobalHotkeyInfo hotkey in DefaultHotkeys.GlobalHotkeys)
            RegisterHotkey(hotkey, null);
        // Register app hotkeys
        foreach (AppHotkeyInfo hotkey in DefaultHotkeys.AppHotkeys)
            RegisterHotkey(hotkey, null);

        // Start listening for hotkeys
        _keyboardClient.OnHotkeyTriggered += OnGlobalHotkeyTriggered;
    }

    private bool ShouldIgnoreHotkeys()
    {
        return _settings.IgnoreHotkeysOnFullscreen
            && WindowHelper.IsWindowFullscreen(WindowHelper.GetForegroundWindow());
    }

    private void OnGlobalHotkeyTriggered(Hotkey hotkey)
    {
        try
        {
            lock (_lastGlobalHotkeyLock)
                _lastGlobalHotkey = hotkey;

            if (IgnoreGlobalHotkeys || ShouldIgnoreHotkeys())
                return;

            Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
            {
                if (_enabledGlobalHotkeys.TryGetValue(hotkey, out GlobalHotkeyInfo? hotkeyInfo))
                    hotkeyInfo.OnHotkeyTriggered.Invoke();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error while handling global hotkey {hotkey}");
        }
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
        if (!hotkey.IsValid)
        {
            reason = "Invalid hotkey";
            return false;
        }

        // Check for conflicting app or result hotkey
        HotkeyInfo? existing = _enabledAppHotkeys.GetValueOrDefault(hotkey);
        existing ??= _enabledResultHotkeys.Values
            .Select(resultHotkeys => resultHotkeys.GetValueOrDefault(hotkey))
            .FirstOrDefault(info => info is not null);

        // Check for conflicting global hotkey
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
        if (!hotkey.IsValid)
        {
            reason = "Invalid hotkey";
            return false;
        }

        // Check for conflicting app hotkey
        HotkeyInfo? existing = _enabledAppHotkeys.GetValueOrDefault(hotkey);
        // Check for conflicting result hotkey from the same plugin
        if (_enabledResultHotkeys.TryGetValue(pluginId, out var pluginResultHotkeys))
            existing ??= pluginResultHotkeys.GetValueOrDefault(hotkey);

        // Check for conflicting global hotkey
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
        // Register hotkey
        if (!_allHotkeys.TryAdd(hotkeyInfo.Id, new(plugin?.ID, hotkeyInfo)))
        {
            _logger.LogError($"Failed to register hotkey {hotkeyInfo}: ID already registered");
            return;
        }

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
        UpdateHotkey(hotkeyId, default, forceDisable: true);
        _allHotkeys.Remove(hotkeyId);
    }

    public void UpdateHotkey(string hotkeyId, Hotkey newHotkey) => UpdateHotkey(hotkeyId, newHotkey, forceDisable: false);
    private void UpdateHotkey(string hotkeyId, Hotkey newHotkey, bool forceDisable)
    {
        bool disabling = newHotkey == default;

        // Get registered hotkey
        if (!_allHotkeys.TryGetValue(hotkeyId, out var registeredHotkey))
        {
            _logger.LogError($"Cannot update non-registered hotkey '{hotkeyId}'");
            return;
        }
        // Don't do anything if the new hotkey is the same
        if (registeredHotkey.info.Hotkey == newHotkey)
            return;

        // Ensure the new hotkey does not conflict with an existing one
        if (!disabling)
        {
            string? nonAvailabilityReason;
            bool hotkeyAvailable = registeredHotkey.info switch
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
        }
        // Ensure the hotkey can be disabled
        else
        {
            if (!registeredHotkey.info.CanBeDisabled && !forceDisable)
            {
                _logger.LogError($"Tried to disable non-disablable hotkey '{registeredHotkey.info}'");
                return;
            }
        }

        // Disable previous hotkey
        if (registeredHotkey.info.Hotkey != default)
        {
            switch (registeredHotkey.info)
            {
                case GlobalHotkeyInfo globalInfo:
                    _enabledGlobalHotkeys.Remove(globalInfo.Hotkey);

                    if (globalInfo.Hotkey == _startMenuHotkey)
                        _keyboardClient.SetBlockWindowsKey(false);
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

        registeredHotkey.info.Hotkey = newHotkey;
        if (!disabling)
        {
            // Update & enable new hotkey
            switch (registeredHotkey.info)
            {
                case GlobalHotkeyInfo globalInfo:
                    if (newHotkey == _startMenuHotkey)
                        _keyboardClient.SetBlockWindowsKey(true);

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
    public Task<PressedKeys> GetPressedKeysAsync(CancellationToken cancellationToken = default)
    {
        return _keyboardClient.GetPressedKeysAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the last global hotkey combination that was pressed, regardless of whether it is registered or not.
    /// </summary>
    /// <remarks>If no global hotkey was triggered yet, returns <see langword="default"/>.</remarks>
    public Hotkey GetLastGlobalHotkey()
    {
        lock (_lastGlobalHotkeyLock)
            return _lastGlobalHotkey;
    }

    public void Dispose()
    {
        _keyboardClient.OnHotkeyTriggered -= OnGlobalHotkeyTriggered;
        _keyboardClient.Dispose();
    }
}
