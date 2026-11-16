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
    public string ToString(IFormatProvider provider) =>
        IsDecimal ? _decimalValue.ToString(provider) : _integerValue.ToString(provider);

    /// <summary>
    /// Converts the decimal to a <see cref="Value"/> backed by an <see cref="Int128"/> when possible.
    /// </summary>
    private static Value FromDecimal(decimal value)
        => decimal.IsInteger(value) ? new Value((Int128)value) : new Value(value);

    private static (Int128 Left, Int128 Right) AsBitwiseOperands(Value left, Value right)
    {
        if (left.IsDecimal || right.IsDecimal)
            throw new ArgumentException("Bitwise operations not supported on decimal values");

        return (left.AsInt128(), right.AsInt128());
    }

    private static (Int128 Value, int Shift) AsShiftOperands(Value left, Value right)
    {
        if (left.IsDecimal || right.IsDecimal)
            throw new ArgumentException("Bitwise operations not supported on decimal values");

        int shift = right.AsInt32();
        if (shift < 0)
            throw new ArgumentException($"Shift amount cannot be negative: {shift}");
        if (shift >= 128)
            throw new ArgumentException($"Shift amount is too large: {shift}");

        return (left.AsInt128(), shift);
    }

    public static Value operator +(Value value) => value;
    public static Value operator -(Value value)
    {
        checked
        {
            return value.IsDecimal
                ? FromDecimal(-value.AsDecimal())
                : new Value(-value.AsInt128());
        }
    }

    public static Value operator ~(Value value)
    {
        if (value.IsDecimal)
            throw new ArgumentException("Bitwise operations not supported on decimal values");

        return new Value(~value.AsInt128());
    }

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

    public static Value operator +(Value left, Value right)
    {
        checked
        {
            return left.IsDecimal || right.IsDecimal
                ? FromDecimal(left.AsDecimal() + right.AsDecimal())
                : new Value(left.AsInt128() + right.AsInt128());
        }
    }

    public static Value operator -(Value left, Value right)
    {
        checked
        {
            return left.IsDecimal || right.IsDecimal
                ? FromDecimal(left.AsDecimal() - right.AsDecimal())
                : new Value(left.AsInt128() - right.AsInt128());
        }
    }

    public static Value operator *(Value left, Value right)
    {
        checked
        {
            return left.IsDecimal || right.IsDecimal
                ? FromDecimal(left.AsDecimal() * right.AsDecimal())
                : new Value(left.AsInt128() * right.AsInt128());
        }
    }

    public static Value operator /(Value left, Value right)
    {
        checked
        {
            // For normal division, always use decimal math to preserve fractional results
            return FromDecimal(left.AsDecimal() / right.AsDecimal());
        }
    }

    public Value FloorDivide(Value right)
    {
        checked
        {
            if (IsDecimal || right.IsDecimal)
                return FromDecimal(Math.Floor(AsDecimal() / right.AsDecimal()));

            Int128 l = AsInt128();
            Int128 r = right.AsInt128();
            return new Value((l / r) - ((l % r != 0 && (l ^ r) < 0) ? 1 : 0));
        }
    }

    public static Value operator %(Value left, Value right)
    {
        checked
        {
            return left.IsDecimal || right.IsDecimal
                ? FromDecimal(left.AsDecimal() % right.AsDecimal())
                : new Value(left.AsInt128() % right.AsInt128());
        }
    }

    /// <exception cref="ArgumentException"></exception>
    public Value Pow(Value exponent)
    {
        Int128 e;
        try
        {
            e = exponent.AsInt128();
        }
        catch (InvalidOperationException)
        {
            throw new ArgumentException($"Exponent cannot be a decimal: {exponent}");
        }

        checked
        {
            if (!IsDecimal && e >= 0)
            {
                Int128 b = AsInt128();

                Int128 result = 1;
                while (e > 0)
                {
                    if ((e & 1) == 1) result *= b;
                    if (e > 1) b *= b;
                    e >>= 1;
                }

                return new(result);
            }
            else
            {
                decimal b = AsDecimal();
                UInt128 absExp;
                unchecked
                {
                    // Use UInt128 to avoid overflow when e is Int128.MinValue
                    // We need to do this inside an unchecked context or else this is
                    // considered an overflow when e < 0
                    absExp = e < 0 ? (0 - (UInt128)e) : (UInt128)e;
                }

                decimal result = 1;
                while (absExp > 0)
                {
                    if ((absExp & 1) == 1) result *= b;
                    if (absExp > 1) b *= b;
                    absExp >>= 1;
                }

                if (e < 0)
                    // Inverting the base before the while loop would prevent overflow exceptions
                    // on large negative numbers, but would also make the result less accurate
                    result = 1m / result;

                return FromDecimal(result);
            }
        }
    }

    public static Value operator &(Value left, Value right)
    {
        (Int128 l, Int128 r) = AsBitwiseOperands(left, right);
        return new Value(l & r);
    }

    public static Value operator |(Value left, Value right)
    {
        (Int128 l, Int128 r) = AsBitwiseOperands(left, right);
        return new Value(l | r);
    }

    public static Value operator ^(Value left, Value right)
    {
        (Int128 l, Int128 r) = AsBitwiseOperands(left, right);
        return new Value(l ^ r);
    }

    public static Value operator <<(Value left, Value right)
    {
        (Int128 value, int shift) = AsShiftOperands(left, right);
        return new Value(value << shift);
    }

    public static Value operator >>(Value left, Value right)
    {
        (Int128 value, int shift) = AsShiftOperands(left, right);
        return new Value(value >> shift);
    }

    public static Value operator >>>(Value left, Value right)
    {
        (Int128 value, int shift) = AsShiftOperands(left, right);
        return new Value(value >>> shift);
    }
}
