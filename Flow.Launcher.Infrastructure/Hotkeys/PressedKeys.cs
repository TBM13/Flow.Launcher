using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace Flow.Launcher.Infrastructure.Hotkeys;

/// <summary>
/// Represents the keys that were pressed (down) at a specific moment in time.
/// <para/>
/// Extends the <see langword="=="/> and <see langword="!="/> operators to compare with:
/// <list type="bullet">
///     <item>A single <see cref="Key"/> (true if only that key was pressed).</item>
///     <item>An IEnumerable&lt;Key&gt;(true if only those keys were pressed).</item>
///     <item>A <see cref="ModifierKeys"/> (true when the given modifiers were the only keys pressed).</item>
/// </list>
/// </summary>
public class PressedKeys(HashSet<Key> pressedKeys)
{
    private readonly HashSet<Key> _pressedKeys = pressedKeys;

    /// <summary>
    /// True if <see cref="Key.LeftAlt"/> or <see cref="Key.RightAlt"/> was pressed.
    /// </summary>
    public bool AltPressed { get; } = pressedKeys.Contains(Key.LeftAlt) || pressedKeys.Contains(Key.RightAlt);
    /// <summary>
    /// True if <see cref="Key.LeftCtrl"/> or <see cref="Key.RightCtrl"/> was pressed.
    /// </summary>
    public bool CtrlPressed { get; } = pressedKeys.Contains(Key.LeftCtrl) || pressedKeys.Contains(Key.RightCtrl);
    /// <summary>
    /// True if <see cref="Key.LeftShift"/> or <see cref="Key.RightShift"/> was pressed.
    /// </summary>
    public bool ShiftPressed { get; } = pressedKeys.Contains(Key.LeftShift) || pressedKeys.Contains(Key.RightShift);
    /// <summary>
    /// True if <see cref="Key.LWin"/> or <see cref="Key.RWin"/> was pressed.
    /// </summary>
    public bool WindowsPressed { get; } = pressedKeys.Contains(Key.LWin) || pressedKeys.Contains(Key.RWin);

    public bool IsKeyPressed(Key key) => _pressedKeys.Contains(key);

    /// <summary>
    /// Tries to generate a valid hotkey from the currently pressed keys.
    /// <para/>
    /// Returns null if it's not possible (e.g. multiple non-modifier keys pressed).
    /// </summary>
    public Hotkey? ToHotkey()
    {
        ModifierKeys modifiers = ModifierKeys.None;
        Key key = Key.None;

        foreach (var pressedKey in _pressedKeys)
        {
            if (pressedKey.ToModifierKey(out ModifierKeys? mod))
                modifiers |= mod.Value;
            else
            {
                if (key != Key.None)
                {
                    // More than 1 non-modifier key is pressed, can't convert to hotkey
                    return null;
                }

                key = pressedKey;
            }
        }

        return new Hotkey()
        {
            Modifiers = modifiers,
            MainKey = key
        };
    }

    public static bool operator ==(PressedKeys pressedKeys, Key key)
        => pressedKeys._pressedKeys.Count == 1 && pressedKeys._pressedKeys.Contains(key);
    public static bool operator !=(PressedKeys pressedKeys, Key key) => !(pressedKeys == key);

    public static bool operator ==(PressedKeys pressedKeys, IEnumerable<Key> key)
        => pressedKeys._pressedKeys.SetEquals(key);
    public static bool operator !=(PressedKeys pressedKeys, IEnumerable<Key> key) => !(pressedKeys == key);

    public static bool operator ==(PressedKeys pressedKeys, ModifierKeys modifiers)
    {
        return (modifiers.HasFlag(ModifierKeys.Alt) == pressedKeys.AltPressed) &&
               (modifiers.HasFlag(ModifierKeys.Control) == pressedKeys.CtrlPressed) &&
               (modifiers.HasFlag(ModifierKeys.Shift) == pressedKeys.ShiftPressed) &&
                (modifiers.HasFlag(ModifierKeys.Windows) == pressedKeys.WindowsPressed);
    }
    public static bool operator !=(PressedKeys pressedKeys, ModifierKeys modifiers) => !(pressedKeys == modifiers);

    public override bool Equals(object? obj)
    {
        if (obj is PressedKeys other)
            return other._pressedKeys.SetEquals(_pressedKeys);

        if (obj is Key key)
            return this == key;
        if (obj is IEnumerable<Key> keys)
            return this == keys;
        if (obj is ModifierKeys modifiers)
            return this == modifiers;


        return false;
    }

    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (var key in _pressedKeys)
        {
            hash.Add(key);
        }
        return hash.ToHashCode();
    }
}
