// Based on https://github.com/jjw24/ChefKeys
/* Copyright 2024 Jeremy Wu

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Infrastructure.Hotkeys;

/// <summary>
/// Manages global hotkeys that are active even when the app is not focused.
/// <para/>
/// It's not suggested to interact with this class directly.
/// </summary>
internal static class GlobalHotkeyManager
{
    private static UnhookWindowsHookExSafeHandle? _hookID;
    private static readonly HOOKPROC _proc;

    private static readonly Dictionary<Hotkey, Action> _registeredHotkeys = [];
    private static readonly HashSet<Key> _pressedKeys = [];
    private static ModifierKeys _pressedModifiers;
    private static bool _keyPressedBeforeModifiers;
    private static DateTime _lastsKeyDownTime;
    private static bool _isSimulatingKeyPress;

    /// <summary>
    /// The last hotkey that was triggered.
    /// </summary>
    public static Hotkey? LastHotkey { get; private set; }

    /// <summary>
    /// If true, the actions of the registered hotkeys won't be executed.
    /// </summary>
    public static bool IgnoreRegisteredHotkeys { get; set; }

    static GlobalHotkeyManager()
    {
        // Keep a reference to the delegate as a field to prevent it from being garbage collected
        _proc = HookCallback;

        // Run the hook on a dedicated thread so that
        // we don't miss any key events when the main thread is busy
        var hookThread = new Thread(HookThreadProc)
        {
            Name = "GlobalHotkeyHookThread",
            IsBackground = true
        };
        hookThread.Start();
    }

    private static void HookThreadProc()
    {
        _hookID = SetHook(_proc);

        // A low-level keyboard hook requires a message loop on the thread that installed it
        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(in msg);
            PInvoke.DispatchMessage(in msg);
        }
    }

    private static UnhookWindowsHookExSafeHandle SetHook(HOOKPROC proc)
    {
        using Process curProcess = Process.GetCurrentProcess();
        using ProcessModule curModule = curProcess.MainModule ?? throw new NullReferenceException("MainModule was null");
        return PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, proc, PInvoke.GetModuleHandle(curModule.ModuleName), 0);
    }

    private static LRESULT HookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0 && !_isSimulatingKeyPress)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            Key key = KeyInterop.KeyFromVirtualKey(vkCode);
            bool isKeyModifier = key.ToModifierKey(out ModifierKeys? modKey);

            if (key != Key.None)
            {
                if (wParam == PInvoke.WM_KEYDOWN || wParam == PInvoke.WM_SYSKEYDOWN)
                {
                    LastHotkey = null;
                    if (isKeyModifier)
                    {
                        if (_pressedKeys.Count > 0)
                            _keyPressedBeforeModifiers = true;

                        if (!_pressedModifiers.HasFlag(modKey!.Value))
                            _lastsKeyDownTime = DateTime.Now;

                        _pressedModifiers |= modKey.Value;
                    }
                    else
                    {
                        bool wasNotPressed = _pressedKeys.Add(key);
                        if (wasNotPressed)
                            _lastsKeyDownTime = DateTime.Now;
                    }
                }
                else if (wParam == PInvoke.WM_KEYUP || wParam == PInvoke.WM_SYSKEYUP)
                {
                    // If we are releasing a key from the last triggered hotkey,
                    // lets not consider this a hotkey
                    bool isKeyFromLastHotkey = LastHotkey.HasValue
                        && (LastHotkey.Value.MainKey == key || isKeyModifier && LastHotkey.Value.Modifiers.HasFlag(modKey!.Value));

                    // If we somehow missed a key down event or more than one
                    // non-modifier keys are up, lets not consider this a hotkey
                    bool validState = (isKeyModifier && _pressedModifiers.HasFlag(modKey!.Value) && _pressedKeys.Count <= 1)
                        || (!isKeyModifier && _pressedKeys.Count == 1 && _pressedKeys.Contains(key));

                    // Space+Alt (in that order) shouldn't be a valid hotkey
                    // The same applies to something like Shift+K+Alt
                    validState &= !_keyPressedBeforeModifiers;

                    if (isKeyModifier)
                        _pressedModifiers &= ~modKey!.Value;
                    else
                    {
                        _pressedKeys.Remove(key);
                        if (_pressedKeys.Count == 0)
                            _keyPressedBeforeModifiers = false;
                    }

                    if (!isKeyFromLastHotkey && validState)
                    {
                        var elapsed = DateTime.Now - _lastsKeyDownTime;
                        LastHotkey = new()
                        {
                            Modifiers = _pressedModifiers | (isKeyModifier ? modKey!.Value : ModifierKeys.None),
                            MainKey = isKeyModifier ? _pressedKeys.FirstOrDefault(Key.None) : key,
                            LongPress = elapsed >= TimeSpan.FromSeconds(0.6f)
                        };

                        // If there is no action with the long press version of the hotkey,
                        // consider it as a normal press
                        if (!_registeredHotkeys.ContainsKey(LastHotkey.Value))
                            LastHotkey = LastHotkey.Value with { LongPress = false };

                        if (_registeredHotkeys.TryGetValue(LastHotkey!.Value, out var action))
                        {
                            if (!IgnoreRegisteredHotkeys)
                                Application.Current.Dispatcher.BeginInvoke(action);

                            if (key == Key.LWin || key == Key.RWin)
                            {
                                FixWindowsKey();
                                return (LRESULT)1; // Block the key event from reaching the OS or any other app
                            }
                        }
                    }
                }
            }
        }

        // Let the next hook in the chain receive the key event (OS, some other app, etc.)
        return PInvoke.CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    private static void FixWindowsKey()
    {
        // Since we are blocking the Windows key's KEYUP event, the OS will think it's still being
        // held down and will trigger respective shortcuts when another key is pressed.
        // To prevent this, simulate ALT Key press (KEYDOWN) to trigger Windows + Alt shortcut (which does nothing).
        _isSimulatingKeyPress = true;
        PInvoke.keybd_event((int)VIRTUAL_KEY.VK_LMENU, 0, 0, UIntPtr.Zero);

        // Simulate release (KEYUP) of both the Windows and Alt keys
        PInvoke.keybd_event((int)VIRTUAL_KEY.VK_LWIN, 0, KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP, UIntPtr.Zero);
        PInvoke.keybd_event((int)VIRTUAL_KEY.VK_LMENU, 0, KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP, UIntPtr.Zero);
        _isSimulatingKeyPress = false;
    }

    /// <summary>
    /// Registers the given hotkey to perform the given action whenever it's released.
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    public static void RegisterHotkey(Hotkey hotkey, Action action)
    {
        if (!CanRegisterHotkey(hotkey))
            throw new ArgumentException($"Tried to register an invalid or already-registered hotkey '{hotkey}'", nameof(hotkey));

        _registeredHotkeys[hotkey] = action;
    }

    /// <summary>
    /// Tries to unregister the hotkey. Does nothing if it wasn't registered in the first place.
    /// </summary>
    public static void UnregisterHotkey(Hotkey hotkey)
    {
        _registeredHotkeys.Remove(hotkey);
    }

    /// <returns>True if the hotkey is valid and isn't registered already.</returns>
    public static bool CanRegisterHotkey(Hotkey hotkey)
    {
        return hotkey.IsValid && !_registeredHotkeys.ContainsKey(hotkey);
    }

    /// <returns>True if the key is currently down.</returns>
    public static bool IsKeyPressed(Key key)
        => _pressedKeys.Contains(key) || (key.ToModifierKey(out ModifierKeys? modKey) && _pressedModifiers.HasFlag(modKey.Value));

    /// <returns>All the keys that are currently down.</returns>
    public static PressedKeys GetPressedKeys()
    {
        HashSet<Key> pressedKeys = [.. _pressedKeys];
        if (_pressedModifiers.HasFlag(ModifierKeys.Alt))
            // We can't know if it's left or right, but it doesn't matter since we treat them the same
            pressedKeys.Add(Key.LeftAlt);
        if (_pressedModifiers.HasFlag(ModifierKeys.Control))
            pressedKeys.Add(Key.LeftCtrl);
        if (_pressedModifiers.HasFlag(ModifierKeys.Shift))
            pressedKeys.Add(Key.LeftShift);
        if (_pressedModifiers.HasFlag(ModifierKeys.Windows))
            pressedKeys.Add(Key.LWin);

        return new PressedKeys(pressedKeys);
    }
}
