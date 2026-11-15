namespace Flow.Launcher.Plugin.Calculator.Preview;

public sealed record BinaryBit(int Index, bool IsSet, bool IsPadding)
{
    public const int SignBitIndex = 127;

    public bool IsSignBit => Index == SignBitIndex;

    public string Text => IsSet ? "1" : "0";

    public string ToolTip => IsSignBit
        ? $"Bit {Index} (Sign) - Click to toggle"
        : $"Bit {Index} - Click to toggle";
}

public sealed record BinaryGroup(IReadOnlyList<BinaryBit> Bits);

public static class BinaryNumber
{
    public const int BitsPerGroup = 8;

    /// <summary>
    /// Splits <paramref name="value"/> into groups of eight bits, most significant group first.
    /// Bits above the shortest representation of the value are marked as padding.
    /// </summary>
    public static IReadOnlyList<BinaryGroup> GetGroups(Int128 value)
    {
        int significantBits = GetSignificantBitCount(value);
        int groupCount = (significantBits + BitsPerGroup - 1) / BitsPerGroup;
        int displayedBits = groupCount * BitsPerGroup;

        UInt128 bits = unchecked((UInt128)value);
        BinaryGroup[] groups = new BinaryGroup[groupCount];

        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            int mostSignificantBit = displayedBits - groupIndex * BitsPerGroup - 1;
            BinaryBit[] groupBits = new BinaryBit[BitsPerGroup];

            for (int i = 0; i < BitsPerGroup; i++)
            {
                int bitIndex = mostSignificantBit - i;
                bool isSet = ((bits >> bitIndex) & 1) != 0;
                bool isPadding = bitIndex != BinaryBit.SignBitIndex && bitIndex >= significantBits;

                groupBits[i] = new BinaryBit(bitIndex, isSet, isPadding);
            }

            groups[groupIndex] = new BinaryGroup(groupBits);
        }

        return groups;
    }

    /// <summary>
    /// Returns the sign bit (bit 127) when it is not already included in <paramref name="groups"/>.
    /// </summary>
    public static BinaryBit? GetSeparateSignBit(Int128 value, IReadOnlyList<BinaryGroup> groups)
    {
        if (groups.Count * BitsPerGroup > BinaryBit.SignBitIndex)
            return null;

        return new BinaryBit(BinaryBit.SignBitIndex, value < 0, IsPadding: false);
    }

    private static int GetSignificantBitCount(Int128 value)
    {
        int count = 128 - (int)Int128.LeadingZeroCount(value >= 0 ? value : ~value);
        return Math.Max(1, count);
    }
}
