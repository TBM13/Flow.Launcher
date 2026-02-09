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

namespace Flow.Launcher.Infrastructure.Hotkey.ChefKeys;

public static class ChefKeysManager
{
    #region setup

    private static UnhookWindowsHookExSafeHandle _hookID;
    private static HOOKPROC _proc;
    private static bool _isSimulatingKeyPress = false;

    private static readonly Dictionary<int, KeyRecord> registeredHotkeys;
    private static readonly KeyRecord nonRegisteredKeyRecord;

    static ChefKeysManager()
    {
        registeredHotkeys = [];
        nonRegisteredKeyRecord = new KeyRecord { vk_code = 0, HandleKeyPress = HandleNonRegisteredKeyPress };

        _proc = HookCallback;
    }

    public static void Start()
    {
        _hookID = SetHook(_proc);
    }

    public static void Stop()
    {
        PInvoke.UnhookWindowsHookEx((HHOOK)_hookID.DangerousGetHandle());
    }

    private static UnhookWindowsHookExSafeHandle SetHook(HOOKPROC proc)
    {
        using Process curProcess = Process.GetCurrentProcess();
        using ProcessModule curModule = curProcess.MainModule;
        return PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, proc, PInvoke.GetModuleHandle(curModule.ModuleName), 0);
    }

    #endregion setup


    #region key press logic

    private static bool isWinKeyDown = false;
    private static bool nonRegisteredKeyDown = false;
    private static bool cancelSingleKeyAction = false;
    private static bool registeredKeyDown = false;
    private static int lastRegisteredDownKey = 0;

    public static bool StartMenuEnableBlocking = false;
    public static bool StartMenuBlocked = false;
    public static string StartMenuSimulatedKey = "LeftAlt";

    private static LRESULT HookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0 && !_isSimulatingKeyPress)
        {
            int vkCode = Marshal.ReadInt32(lParam);

            var isNotRegisteredkey = !registeredHotkeys.TryGetValue(vkCode, out KeyRecord registeredKeyRecord);

            var keyRecord = isNotRegisteredkey ? nonRegisteredKeyRecord : registeredKeyRecord;

            // Create a one-off win key record when blocking is enabled
            if (StartMenuEnableBlocking && (vkCode == (int)VIRTUAL_KEY.VK_LWIN || vkCode == (int)VIRTUAL_KEY.VK_RWIN))
            {
                keyRecord = new KeyRecord
                {
                    vk_code = vkCode,
                    HandleKeyPress = HandleRegisteredKeyPress,
                    isSingleKeyRegistered = true,
                    action = null
                };
            }

            var blockKeyPress = keyRecord.HandleKeyPress(wParam, vkCode, keyRecord);

            if (blockKeyPress)
                return (LRESULT)1;
        }

        return PInvoke.CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    private static bool HandleRegisteredKeyPress(WPARAM wParam, int vkCode, KeyRecord keyRecord)
    {
        if (nonRegisteredKeyDown)
            cancelSingleKeyAction = true;

        if (wParam == PInvoke.WM_KEYDOWN || wParam == PInvoke.WM_SYSKEYDOWN)
        {
            StartMenuBlocked = false;

            registeredKeyDown = true;

            lastRegisteredDownKey = vkCode;

            if (vkCode == (int)VIRTUAL_KEY.VK_LWIN || vkCode == (int)VIRTUAL_KEY.VK_RWIN)
                isWinKeyDown = true;

            // Non-modifier combos i.e. ending with a non-modifier key e.g. LeftAlt+Z
            // can be handled with key down so it's triggered faster than waiting till key up
            if (lastRegisteredDownKey == vkCode && !keyRecord.vkCodeIsModifierKey && keyRecord.AreKeyCombosRegistered())
            {
                var comboRecord = GetComboRecord(keyRecord);

                if (comboRecord is not null)
                {
                    comboRecord.action?.Invoke();

                    // This is needed because combo has a non-register key that triggers cancelSingleKeyAction to true,
                    // this need to be reset for subsequent single key press.
                    cancelSingleKeyAction = false;

                    return false;
                }
            }
        }

        if (wParam == PInvoke.WM_KEYUP || wParam == PInvoke.WM_SYSKEYUP)
        {
            registeredKeyDown = false;

            if (lastRegisteredDownKey != vkCode)
                cancelSingleKeyAction = true;

            if (vkCode == (int)VIRTUAL_KEY.VK_LWIN || vkCode == (int)VIRTUAL_KEY.VK_RWIN)
            {
                if (!cancelSingleKeyAction && isWinKeyDown)
                {
                    BlockWindowsStartMenu();

                    StartMenuBlocked = true;

                    keyRecord.action?.Invoke();

                    return true;
                }

                isWinKeyDown = false;
                cancelSingleKeyAction = false;

                return false;
            }

            // Modifier combos e.g. leftctrl+leftshift must be handled with key up to avoid triggering
            // when additional modifier keys are pressed e.g. leftctrl+leftshift+leftalt or leftctrl+leftshift+s
            if (lastRegisteredDownKey == vkCode && keyRecord.AreKeyCombosRegistered() && keyRecord.vkCodeIsModifierKey)
            {
                var comboRecord = GetComboRecord(keyRecord);

                if (comboRecord is not null)
                {
                    comboRecord.action?.Invoke();

                    // This is needed because combo has a non-register key that triggers cancelSingleKeyAction to true,
                    // this need to be reset for subsequent single key press.
                    cancelSingleKeyAction = false;

                    return false;
                }
            }

            if (!cancelSingleKeyAction)
            {
                if (keyRecord.isSingleKeyRegistered)
                    keyRecord.action?.Invoke();
            }

            cancelSingleKeyAction = false;
        }

        return false;
    }

    private static bool HandleNonRegisteredKeyPress(WPARAM wParam, int vkCode, KeyRecord registeredKeyRecord)
    {
        if (wParam == PInvoke.WM_KEYDOWN || wParam == PInvoke.WM_SYSKEYDOWN)
        {
            nonRegisteredKeyDown = true;
            lastRegisteredDownKey = vkCode;
            StartMenuBlocked = false;
        }

        if (wParam == PInvoke.WM_KEYUP || wParam == PInvoke.WM_SYSKEYUP)
            nonRegisteredKeyDown = false;

        // Handles instance where non-registered key is up while single registered key is still down,
        // e.g. registered Ctrl down, unregistered Esc down & up, registered Ctrl up
        if (registeredKeyDown)
            cancelSingleKeyAction = true;

        return false;
    }

    private static KeyComboRecord GetComboRecord(KeyRecord keyRecord)
    {
        KeyComboRecord comboFound = null;
        for (var index = 0; index < keyRecord.KeyComboRecords.Count; index++)
        {
            if (keyRecord.KeyComboRecords[index].AreComboKeysHeldDown())
            {
                comboFound = keyRecord.KeyComboRecords[index];

                break;
            }
        }

        return comboFound;
    }

    private static void BlockWindowsStartMenu()
    {
        _isSimulatingKeyPress = true;
        PInvoke.keybd_event((int)VIRTUAL_KEY.VK_LMENU, 0, 0, UIntPtr.Zero);
        _isSimulatingKeyPress = false;
        _isSimulatingKeyPress = true;
        PInvoke.keybd_event((int)VIRTUAL_KEY.VK_LWIN, 0, KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP, UIntPtr.Zero);
        _isSimulatingKeyPress = false;
        isWinKeyDown = false;
        _isSimulatingKeyPress = true;
        PInvoke.keybd_event((int)VIRTUAL_KEY.VK_LMENU, 0, KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP, UIntPtr.Zero);
        _isSimulatingKeyPress = false;
    }

    #endregion key press logic


    #region key management

    public static void RegisterHotkey(string hotkey, Action action) => RegisterHotkey(hotkey, hotkey, action);

    public static void RegisterHotkey(string hotkey, string previousHotkey, Action action)
    {
        hotkey = ConvertIncorrectKeyString(hotkey);
        previousHotkey = ConvertIncorrectKeyString(previousHotkey);

        UnregisterHotkey(previousHotkey);

        if (string.IsNullOrEmpty(hotkey))
            return;

        // The released key need to be the unique key in the dictionary.
        // The last key in the combo is the release key that triggers action
        var keys = SplitHotkeyReversed(hotkey);

        var vkCodeCombo0 = keys.ElementAtOrDefault(1) is not null ? ToKeyCode(keys.ElementAtOrDefault(1)) : 0;
        var vkCodeCombo1 = keys.ElementAtOrDefault(2) is not null ? ToKeyCode(keys.ElementAtOrDefault(2)) : 0;
        var vkCodeCombo2 = keys.ElementAtOrDefault(3) is not null ? ToKeyCode(keys.ElementAtOrDefault(3)) : 0;

        var singleKey = vkCodeCombo0 + vkCodeCombo1 + vkCodeCombo2 == 0;
        var comboKeys = singleKey is false;
        var vk_code = ToKeyCode(keys.First());

        if (registeredHotkeys.TryGetValue(vk_code, out var existingKeyRecord))
        {
            if (singleKey && !existingKeyRecord.isSingleKeyRegistered)
            {
                existingKeyRecord.isSingleKeyRegistered = true;
                existingKeyRecord.action += action;
            }

            if (comboKeys)
                existingKeyRecord.RegisterKeyCombo(hotkey, vk_code, action, vkCodeCombo0, vkCodeCombo1, vkCodeCombo2);

            return;
        }

        var keyRecord = new KeyRecord
        {
            vk_code = ToKeyCode(keys.First()),
            HandleKeyPress = HandleRegisteredKeyPress,
            isSingleKeyRegistered = singleKey,
            action = singleKey ? action : null
        };

        if (comboKeys)
            keyRecord.RegisterKeyCombo(hotkey, vk_code, action, vkCodeCombo0, vkCodeCombo1, vkCodeCombo2);

        registeredHotkeys.Add(ToKeyCode(keys.First()), keyRecord);
    }

    public static void UnregisterHotkey(string hotkey)
    {
        var existingKeyRecord = GetRegisteredKeyRecord(hotkey);

        if (existingKeyRecord is null)
            return;

        if (!IsComboString(hotkey))
        {
            existingKeyRecord.action -= existingKeyRecord.action;
            existingKeyRecord.isSingleKeyRegistered = false;

            if (!existingKeyRecord.AreKeyCombosRegistered())
                registeredHotkeys.Remove(existingKeyRecord.vk_code);

            return;
        }

        var comboRecord = existingKeyRecord.KeyComboRecords.FirstOrDefault(x => x.comboRaw == hotkey);
        comboRecord.action -= comboRecord.action;
        existingKeyRecord.KeyComboRecords.RemoveAll(x => x.comboRaw == hotkey);

        if (!existingKeyRecord.isSingleKeyRegistered && !existingKeyRecord.AreKeyCombosRegistered())
            registeredHotkeys.Remove(existingKeyRecord.vk_code);
    }

    internal static KeyRecord GetRegisteredKeyRecord(string hotkey)
    {
        if (string.IsNullOrEmpty(hotkey) || !registeredHotkeys.TryGetValue(ToKeyCode(SplitHotkeyReversed(hotkey).First()), out var existingKeyRecord))
            return null;

        var isComboString = IsComboString(hotkey);

        if (!isComboString && !existingKeyRecord.isSingleKeyRegistered)
            return null;

        if (isComboString && !existingKeyRecord.KeyComboRecords.Exists(x => x.comboRaw == hotkey))
            return null;

        return existingKeyRecord;
    }

    public static bool CanRegisterHotkey(string hotkey) => IsAvailable(hotkey) && IsValidHotkey(hotkey);

    public static bool IsAvailable(string hotkey) => GetRegisteredKeyRecord(ConvertIncorrectKeyString(hotkey)) is null;

    public static bool IsValidHotkey(string hotkey)
    {
        if (!(!string.IsNullOrEmpty(hotkey) && SplitHotkeyReversed(ConvertIncorrectKeyString(hotkey)).Count() <= 4))
            return false;


        // TODO: add support for same key combo e.g. LeftControl + LeftControl
        if (!IsComboString(hotkey))
            return true;

        return !hotkey.Split('+').GroupBy(key => key).Any(chunk => chunk.Count() > 1);
        //
    }

    private static int ToKeyCode(string key)
    {
        return KeyInterop.VirtualKeyFromKey((Key)Enum.Parse(typeof(Key), key));
    }

    private static IEnumerable<string> SplitHotkeyReversed(string hotkey)
        => hotkey.Split("+", StringSplitOptions.RemoveEmptyEntries).Reverse();

    private static string ConvertIncorrectKeyString(string hotkey)
    {
        if (string.IsNullOrEmpty(hotkey))
            return string.Empty;

        var keys = hotkey.Split("+", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var newHotkey = string.Empty;
        foreach (var key in keys)
        {
            if (!string.IsNullOrEmpty(newHotkey))
                newHotkey += "+";
            // TODO: convert lowercase string passed in e.g. "win", "lwin", "leftalt+z" ??
            switch (key.ToLower())
            {
                case "alt":
                    newHotkey += "LeftAlt";
                    break;
                case "lalt":
                    newHotkey += "LeftAlt";
                    break;
                case "ralt":
                    newHotkey += "RightAlt";
                    break;
                case "ctrl":
                    newHotkey += "LeftCtrl";
                    break;
                case "lctrl":
                    newHotkey += "LeftCtrl";
                    break;
                case "rctrl":
                    newHotkey += "RightCtrl";
                    break;
                case "shift":
                    newHotkey += "LeftShift";
                    break;
                case "lshift":
                    newHotkey += "LeftShift";
                    break;
                case "rshift":
                    newHotkey += "RightShift";
                    break;
                case "win":
                    newHotkey += "LWin";
                    break;
                default:
                    newHotkey += key;
                    break;
            }
        }

        return newHotkey;
    }

    private static bool IsComboString(string hotkey) => hotkey.Contains('+');

    #endregion key management
}
