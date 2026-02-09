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
using System.Linq;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Flow.Launcher.Infrastructure.Hotkey.ChefKeys;

internal record KeyRecord
{
    internal int vk_code
    {
        get => field;
        set
        {
            field = value;
            vkCodeIsModifierKey = IsModifierKeyCode(vk_code);
        }
    }

    internal bool vkCodeIsModifierKey { get; set; } = false;

    internal List<KeyComboRecord> KeyComboRecords = [];

    internal bool isSingleKeyRegistered { get; set; } = false;

    internal Action action;

    internal Func<WPARAM, int, KeyRecord, bool> HandleKeyPress { get; set; }

    internal bool AreKeyCombosRegistered() => KeyComboRecords.Count > 0;

    internal void RegisterKeyCombo(string hotkey, int vk_code, Action action, int vkCodeCombo0, int vkCodeCombo1 = 0, int vkCodeCombo2 = 0)
    {
        if (KeyComboRecords.Any(x => x.comboRaw == hotkey))
            return;

        KeyComboRecords
            .Add(
                new KeyComboRecord
                {
                    vk_code = vk_code,
                    vkCodeCombo0 = vkCodeCombo0,
                    vkCodeCombo1 = vkCodeCombo1,
                    vkCodeCombo2 = vkCodeCombo2,
                    action = action,
                    comboRaw = hotkey
                });

    }
    private static bool IsModifierKeyCode(int vk_code)
    {
        return vk_code switch
        {
            (int)VIRTUAL_KEY.VK_LCONTROL => true,
            (int)VIRTUAL_KEY.VK_RCONTROL => true,
            (int)VIRTUAL_KEY.VK_LMENU => true,
            (int)VIRTUAL_KEY.VK_RMENU => true,
            (int)VIRTUAL_KEY.VK_LSHIFT => true,
            (int)VIRTUAL_KEY.VK_RSHIFT => true,
            (int)VIRTUAL_KEY.VK_LWIN => true,
            (int)VIRTUAL_KEY.VK_RWIN => true,
            _ => false,
        };
    }
};

internal record KeyComboRecord
{
    internal int vk_code { get; set; }

    internal Action action;

    internal int vkCodeCombo0 { get; set; } = 0;

    internal int vkCodeCombo1 { get; set; } = 0;

    internal int vkCodeCombo2 { get; set; } = 0;

    internal string comboRaw { get; set; } = string.Empty;

    internal bool AreComboKeysHeldDown()
    {
        var heldDown = true;

        // vk_code is the release key, which is already pressed, no need to check.
        var comboKeys = new Dictionary<int, string> { { vk_code, string.Empty } };

        if (vkCodeCombo0 > 0)
        {
            comboKeys.Add(vkCodeCombo0, string.Empty);

            if ((PInvoke.GetAsyncKeyState(vkCodeCombo0) & 0x8000) == 0)
                heldDown = false;
        }

        if (vkCodeCombo1 > 0)
        {
            comboKeys.Add(vkCodeCombo1, string.Empty);

            if ((PInvoke.GetAsyncKeyState(vkCodeCombo1) & 0x8000) == 0)
                heldDown = false;
        }

        if (vkCodeCombo2 > 0)
        {
            comboKeys.Add(vkCodeCombo2, string.Empty);

            if ((PInvoke.GetAsyncKeyState(vkCodeCombo2) & 0x8000) == 0)
                heldDown = false;
        }

        if (NonRegisteredModifierKeyPressed(comboKeys))
            heldDown = false;

        return heldDown;
    }

    private bool NonRegisteredModifierKeyPressed(Dictionary<int, string> comboKeys)
    {
        if (!comboKeys.ContainsKey((int)VIRTUAL_KEY.VK_LCONTROL) && ((PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_LCONTROL) & 0x8000) != 0))
            return true;

        if (!comboKeys.ContainsKey((int)VIRTUAL_KEY.VK_RCONTROL) && ((PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_RCONTROL) & 0x8000) != 0))
            return true;

        if (!comboKeys.ContainsKey((int)VIRTUAL_KEY.VK_LMENU) && ((PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_LMENU) & 0x8000) != 0))
            return true;

        if (!comboKeys.ContainsKey((int)VIRTUAL_KEY.VK_RMENU) && ((PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_RMENU) & 0x8000) != 0))
            return true;

        if (!comboKeys.ContainsKey((int)VIRTUAL_KEY.VK_LSHIFT) && ((PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_LSHIFT) & 0x8000) != 0))
            return true;

        if (!comboKeys.ContainsKey((int)VIRTUAL_KEY.VK_RSHIFT) && ((PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_RSHIFT) & 0x8000) != 0))
            return true;

        if (!comboKeys.ContainsKey((int)VIRTUAL_KEY.VK_LWIN) && ((PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_LWIN) & 0x8000) != 0))
            return true;

        if (!comboKeys.ContainsKey((int)VIRTUAL_KEY.VK_RWIN) && ((PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_RWIN) & 0x8000) != 0))
            return true;

        return false;
    }
}
