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
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Infrastructure.Hotkey;

public static class ChefKeysManager
{
    private static readonly UnhookWindowsHookExSafeHandle? _hookID;
    private static readonly HOOKPROC _proc;

    private static readonly Dictionary<KeySequence, Action> _registeredHotkeys = [];
    private static readonly HashSet<Key> _pressedKeys = [];
    private static bool _isSimulatingKeyPress = false;
    private static bool _blockAllKeys = false;
    private static HashSet<KeySequence> _blockKeysExceptions = [];
    private static Action<KeySequence>? _onKeyBlocked;

    static ChefKeysManager()
    {
        // Keep a reference to the delegate as a field to prevent it from being garbage collected
        _proc = HookCallback;
        _hookID = SetHook(_proc);
    }

    private static UnhookWindowsHookExSafeHandle SetHook(HOOKPROC proc)
    {
        using Process curProcess = Process.GetCurrentProcess();
        using ProcessModule curModule = curProcess.MainModule ?? throw new NullReferenceException("MainModule was null");
        return PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, proc, PInvoke.GetModuleHandle(curModule.ModuleName), 0);
    }

    /// <summary>
    /// Blocks ALL the key sequences from doing anything at the OS level, except for those in <paramref name="exceptions"/>.
    /// <para/>
    /// Whenever a blocked sequence is released, <paramref name="onSequenceBlocked"/> will be called with it.
    /// </summary>
    public static void BlockAllKeys(HashSet<KeySequence> exceptions, Action<KeySequence>? onSequenceBlocked)
    {
        if (_blockAllKeys)
            throw new InvalidOperationException("BlockAllKeys can only be called once.");

        _blockKeysExceptions = exceptions;
        _onKeyBlocked = onSequenceBlocked;
        _blockAllKeys = true;
    }

    /// <summary>
    /// Undoes the effect of <see cref="BlockAllKeys"/>.
    /// </summary>
    public static void ReleaseAllKeys()
    {
        _blockAllKeys = false;
        _onKeyBlocked = null;
        _blockKeysExceptions.Clear();
    }

    private static LRESULT HookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        // TODO: Handle LongPress
        if (nCode >= 0 && !_isSimulatingKeyPress)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            Key key = KeyInterop.KeyFromVirtualKey(vkCode);

            if (wParam == PInvoke.WM_KEYDOWN || wParam == PInvoke.WM_SYSKEYDOWN)
            {
                _pressedKeys.Add(key);
            }
            else if (wParam == PInvoke.WM_KEYUP || wParam == PInvoke.WM_SYSKEYUP)
            {
                _pressedKeys.Remove(key);

                Key[] sequence = new Key[_pressedKeys.Count + 1];
                _pressedKeys.CopyTo(sequence);
                sequence[^1] = key;

                _pressedKeys.Clear();
                KeySequence keySequence = new KeySequence { Keys = sequence, LongPress = false };
                if (_blockAllKeys && !_blockKeysExceptions.Contains(keySequence))
                {
                    if (key == Key.LWin || key == Key.RWin)
                        BlockStartMenu();

                    _onKeyBlocked?.Invoke(keySequence);
                    return (LRESULT)1; // Block the key event from reaching the OS or any other app
                }

                if (_registeredHotkeys.TryGetValue(keySequence, out var action))
                {
                    action.Invoke();

                    if (key == Key.LWin || key == Key.RWin)
                        BlockStartMenu();

                    return (LRESULT)1; // Block the key event from reaching the OS or any other app
                }
            }
        }

        // Let the next hook in the chain receive the key event (OS, some other app, etc.)
        return PInvoke.CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    private static void BlockStartMenu()
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
    public static void RegisterHotkey(KeySequence hotkey, Action action)
    {
        if (!CanRegisterHotkey(hotkey))
            throw new ArgumentException("Tried to register an invalid hotkey or one that was already registered.", nameof(hotkey));

        _registeredHotkeys[hotkey] = action;
    }

    /// <summary>
    /// Tries to unregister the hotkey. Does nothing if it wasn't registered in the first place.
    /// </summary>
    public static void UnregisterHotkey(KeySequence hotkey)
    {
        _registeredHotkeys.Remove(hotkey);
    }

    /// <returns>True if the sequence is valid and isn't registered already.</returns>
    public static bool CanRegisterHotkey(KeySequence sequence)
    {
        return IsValidHotkey(sequence) && !_registeredHotkeys.ContainsKey(sequence);
    }
    /// <returns>True if the sequence is valid.</returns>
    public static bool IsValidHotkey(KeySequence sequence)
    {
        bool hasDuplicates = sequence.Keys.Length != sequence.Keys.Distinct().Count();
        return !hasDuplicates && sequence.Keys.Length >= 0;
    }

    /// <returns>True if the key is currently down.</returns>
    public static bool IsKeyPressed(Key key)
        => (PInvoke.GetKeyState(KeyInterop.VirtualKeyFromKey(key)) & 0x80) != 0;

    /// <returns>All the keys that are currently down.</returns>
    public static PressedKeys GetPressedKeys()
    {
        HashSet<Key> pressedKeys = [];

        byte[] keyState = new byte[256];
        PInvoke.GetKeyboardState(keyState);

        for (int i = 0; i < keyState.Length; i++)
        {
            if ((keyState[i] & 0x80) != 0)
            {
                pressedKeys.Add(KeyInterop.KeyFromVirtualKey(i));
            }
        }

        return new PressedKeys(pressedKeys);
    }
}
