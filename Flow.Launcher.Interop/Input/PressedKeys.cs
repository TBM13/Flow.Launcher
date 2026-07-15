using System.Numerics;
using System.Windows.Input;
using Flow.Launcher.PluginSDK.Hotkeys;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Flow.Launcher.Interop.Input;

/// <inheritdoc cref="IPressedKeys{TSelf}"/>
public readonly struct PressedKeys : IPressedKeys, IEquatable<PressedKeys>
{
    // Windows has 256 virtual keys so we can represent the pressed keys as 4 ulong values (4 * 64 = 256)
    private readonly ulong _b0;
    private readonly ulong _b1;
    private readonly ulong _b2;
    private readonly ulong _b3;

    /// <inheritdoc/>
    public int PressedCount =>
        BitOperations.PopCount(_b0) + BitOperations.PopCount(_b1) +
        BitOperations.PopCount(_b2) + BitOperations.PopCount(_b3);

    /// <inheritdoc/>
    public int PressedModifiersCount
    {
        get
        {
            int count = 0;
            if (KeyPressed((uint)VIRTUAL_KEY.VK_LMENU))
                count++;
            if (KeyPressed((uint)VIRTUAL_KEY.VK_RMENU))
                count++;
            if (KeyPressed((uint)VIRTUAL_KEY.VK_LCONTROL))
                count++;
            if (KeyPressed((uint)VIRTUAL_KEY.VK_RCONTROL))
                count++;
            if (KeyPressed((uint)VIRTUAL_KEY.VK_LSHIFT))
                count++;
            if (KeyPressed((uint)VIRTUAL_KEY.VK_RSHIFT))
                count++;
            if (KeyPressed((uint)VIRTUAL_KEY.VK_LWIN))
                count++;
            if (KeyPressed((uint)VIRTUAL_KEY.VK_RWIN))
                count++;
            return count;
        }
    }

    /// <inheritdoc/>
    public bool AltPressed => KeyPressed((uint)VIRTUAL_KEY.VK_LMENU) || KeyPressed((uint)VIRTUAL_KEY.VK_RMENU);
    /// <inheritdoc/>
    public bool CtrlPressed => KeyPressed((uint)VIRTUAL_KEY.VK_LCONTROL) || KeyPressed((uint)VIRTUAL_KEY.VK_RCONTROL);
    /// <inheritdoc/>
    public bool ShiftPressed => KeyPressed((uint)VIRTUAL_KEY.VK_LSHIFT) || KeyPressed((uint)VIRTUAL_KEY.VK_RSHIFT);
    /// <inheritdoc/>
    public bool WindowsPressed => KeyPressed((uint)VIRTUAL_KEY.VK_LWIN) || KeyPressed((uint)VIRTUAL_KEY.VK_RWIN);

    private PressedKeys(ulong b0, ulong b1, ulong b2, ulong b3)
    {
        _b0 = b0;
        _b1 = b1;
        _b2 = b2;
        _b3 = b3;
    }

    /// <summary>
    /// Returns a new instance with the given key marked as pressed.
    /// </summary>
    internal PressedKeys PressKey(uint vk)
    {
        if (vk <= 0 || vk > 255)
            return this;

        int segment = (int)vk >> 6; // vk / 64
        ulong mask = 1UL << ((int)vk & 63);

        return segment switch
        {
            0 => new PressedKeys(_b0 | mask, _b1, _b2, _b3),
            1 => new PressedKeys(_b0, _b1 | mask, _b2, _b3),
            2 => new PressedKeys(_b0, _b1, _b2 | mask, _b3),
            3 => new PressedKeys(_b0, _b1, _b2, _b3 | mask),
            _ => this
        };
    }

    /// <summary>
    /// Returns a new instance with the given key not marked as pressed.
    /// </summary>
    internal PressedKeys ReleaseKey(uint vk)
    {
        if (vk <= 0 || vk > 255)
            return this;

        int segment = (int)vk >> 6; // vk / 64
        ulong mask = 1UL << ((int)vk & 63);

        return segment switch
        {
            0 => new PressedKeys(_b0 & ~mask, _b1, _b2, _b3),
            1 => new PressedKeys(_b0, _b1 & ~mask, _b2, _b3),
            2 => new PressedKeys(_b0, _b1, _b2 & ~mask, _b3),
            3 => new PressedKeys(_b0, _b1, _b2, _b3 & ~mask),
            _ => this
        };
    }

    /// <inheritdoc cref="IsKeyPressed(Key)"/>
    internal bool KeyPressed(uint vk)
    {
        if (vk <= 0 || vk > 255)
            return false;

        int segment = (int)vk >> 6; // vk / 64
        int bit = (int)vk & 63;    // vk % 64
        ulong mask = 1UL << bit;

        return segment switch
        {
            0 => (_b0 & mask) != 0,
            1 => (_b1 & mask) != 0,
            2 => (_b2 & mask) != 0,
            3 => (_b3 & mask) != 0,
            _ => false
        };
    }

    /// <inheritdoc/>
    public bool IsKeyPressed(Key key)
    {
        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk <= 0 || vk > 255)
            return false;

        return KeyPressed((uint)vk);
    }
    /// <inheritdoc/>
    public bool IsModifierPressed(ModifierKeys modifiers)
    {
        bool res = true;
        if (modifiers.HasFlag(ModifierKeys.Alt))
            res &= AltPressed;
        if (modifiers.HasFlag(ModifierKeys.Control))
            res &= CtrlPressed;
        if (modifiers.HasFlag(ModifierKeys.Shift))
            res &= ShiftPressed;
        if (modifiers.HasFlag(ModifierKeys.Windows))
            res &= WindowsPressed;

        return res;
    }

    /// <inheritdoc/>
    public bool OnlyKeyPressed(Key key) => IsKeyPressed(key) && PressedCount == 1;

    /// <inheritdoc/>
    public bool OnlyModifiersPressed(ModifierKeys modifiers)
    {
        return PressedCount == PressedModifiersCount &&
               (modifiers.HasFlag(ModifierKeys.Alt) == AltPressed) &&
               (modifiers.HasFlag(ModifierKeys.Control) == CtrlPressed) &&
               (modifiers.HasFlag(ModifierKeys.Shift) == ShiftPressed) &&
               (modifiers.HasFlag(ModifierKeys.Windows) == WindowsPressed);
    }

    /// <inheritdoc/>
    public Hotkey ToHotkey()
    {
        ModifierKeys modifiers = ModifierKeys.None;
        Key mainKey = Key.None;

        for (int i = 0; i < 4; i++)
        {
            ulong segment = i switch
            {
                0 => _b0,
                1 => _b1,
                2 => _b2,
                3 => _b3,
                _ => 0
            };

            while (segment != 0)
            {
                int bit = BitOperations.TrailingZeroCount(segment);

                Key pressedKey = KeyInterop.KeyFromVirtualKey((i << 6) + bit);
                if (pressedKey != Key.None)
                {
                    if (pressedKey.ToModifierKey(out ModifierKeys mod))
                        modifiers |= mod;
                    else
                    {
                        if (mainKey != Key.None)
                            // More than one non-modifier key pressed, not a valid hotkey
                            return default;

                        mainKey = pressedKey;
                    }
                }

                segment &= segment - 1; // Clear the lowest set bit
            }
        }

        return new Hotkey()
        {
            Modifiers = modifiers,
            MainKey = mainKey
        };
    }

    public static bool operator ==(in PressedKeys left, in PressedKeys right) => left.Equals(right);
    public static bool operator !=(in PressedKeys left, in PressedKeys right) => !left.Equals(right);

    public bool Equals(PressedKeys other) =>
        _b0 == other._b0 && _b1 == other._b1 && _b2 == other._b2 && _b3 == other._b3;

    public override bool Equals(object? obj) => obj switch
    {
        PressedKeys other => Equals(other),
        _ => false
    };

    public override int GetHashCode() => HashCode.Combine(_b0, _b1, _b2, _b3);
}
