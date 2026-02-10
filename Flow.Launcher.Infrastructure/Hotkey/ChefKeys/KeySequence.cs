using System;
using System.Windows.Input;

namespace Flow.Launcher.Infrastructure.Hotkey.ChefKeys;

public record KeySequence
{
    /// <summary>
    /// The sequence of keys that will trigger the hotkey.
    /// <para/>
    /// The order of the keys doesn't matter, except for the last key which needs to be released last.<br/>
    /// E.g. Ctrl+Alt+K and Alt+Ctrl+K are the same hotkey, but Ctrl+K+Alt is a different one.
    /// </summary>
    public required Key[] Keys { get; set; }

    /// <summary>
    /// If false, the hotkey will be triggered as soon as the sequence of keys is released (default).<br/>
    /// Otherwise, they will need to be held down for at least one second before being released to trigger the hotkey.
    /// <para/>
    /// This allows two different hotkeys to be registered with the same key sequence.
    /// </summary>
    public bool LongPress { get; set; }

    public KeySequence(KeySequence other)
    {
        // Make a new array
        Keys = new Key[other.Keys.Length];
        other.Keys.CopyTo(Keys, 0);

        LongPress = other.LongPress;
    }

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(LongPress);

        if (Keys.Length > 1)
        {
            Key[] sorted = new Key[Keys.Length - 1];
            Array.Copy(Keys, sorted, Keys.Length - 1);
            Array.Sort(sorted);

            foreach (var key in sorted)
            {
                hash.Add(key);
            }
        }

        hash.Add(Keys[^1]);
        return hash.ToHashCode();
    }

    public virtual bool Equals(KeySequence? other)
    {
        if (other is null || Keys.Length != other.Keys.Length || LongPress != other.LongPress)
            return false;

        Key[] sorted = new Key[Keys.Length - 1];
        Array.Copy(Keys, sorted, Keys.Length - 1);
        Array.Sort(sorted);
        Key[] otherSorted = new Key[other.Keys.Length - 1];
        Array.Copy(other.Keys, otherSorted, other.Keys.Length - 1);
        Array.Sort(otherSorted);
        for (int i = 0; i < sorted.Length; i++)
        {
            if (sorted[i] != otherSorted[i])
                return false;
        }

        return Keys[^1] == other.Keys[^1];
    }
}
