using System.Runtime.InteropServices;

namespace Flow.Launcher.Plugin.Calculator.Engine;

public enum ValueKind : byte
{
    Integer,
    Decimal
}

[StructLayout(LayoutKind.Explicit)]
public readonly struct Value
{
    [FieldOffset(0)] private readonly Int128 _integerValue;
    [FieldOffset(0)] private readonly decimal _decimalValue;
    [FieldOffset(16)] private readonly ValueKind _kind;

    public bool IsDecimal => _kind == ValueKind.Decimal;

    public Value(Int128 integer)
    {
        _integerValue = integer;
        _kind = ValueKind.Integer;
    }

    public Value(decimal dec)
    {
        _decimalValue = dec;
        _kind = ValueKind.Decimal;
    }

    public Int128 AsInt128()
    {
        if (_kind == ValueKind.Integer)
            return _integerValue;

        if (decimal.IsInteger(_decimalValue))
            return (Int128)_decimalValue;

        throw new InvalidOperationException($"Decimal {_decimalValue} cannot be converted to an integer");
    }

    public int AsInt32()
    {
        Int128 intValue = AsInt128();
        if (intValue > int.MaxValue || intValue < int.MinValue)
            throw new OverflowException($"Integer {intValue} is too large to fit into an Int32 representation");

        return (int)intValue;
    }

    /// <exception cref="OverflowException"></exception>
    public decimal AsDecimal()
    {
        if (_kind == ValueKind.Decimal)
            return _decimalValue;

        if (_integerValue > (Int128)decimal.MaxValue || _integerValue < (Int128)decimal.MinValue)
            throw new OverflowException($"Integer {_integerValue} is too large to fit into a decimal representation");

        return (decimal)_integerValue;
    }

    public override string ToString() => IsDecimal ? _decimalValue.ToString() : _integerValue.ToString();

    /// <exception cref="ArgumentException"></exception>
    public Value Factorial()
    {
        Int128 n = AsInt128();
        if (n < 0)
            throw new ArgumentException($"Factorial is not defined for negative numbers: {n}");
        if (n > 33)
            throw new ArgumentException($"Factorial is too large to compute: {n}");

        // No need to check for overflow since we already limited n to 33
        Int128 result = 1;
        for (int i = 2; i <= n; i++)
            result *= i;

        return new Value(result);
    }
}
