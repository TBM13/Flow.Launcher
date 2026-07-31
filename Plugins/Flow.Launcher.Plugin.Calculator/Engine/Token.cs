using System.Runtime.CompilerServices;

namespace Flow.Launcher.Plugin.Calculator.Engine;

public enum TokenType : byte
{
    Invalid = default,

    Integer,
    Decimal,
    Hexadecimal,
    Operator,
}

public enum OperatorType : byte
{
    Invalid = default,

    // Unary prefix operators
    OpenParentheses,
    UnaryPlus,
    UnaryMinus,
    UnaryBitwiseNot,

    // Unary postfix operators
    CloseParentheses,
    UnaryFactorial,
    UnaryPercentage,

    // Binary operators
    Add,
    Subtract,
    Multiply,
    Divide,
    Remainder,
    FloorDivide,
    Power,
    // Bitwise binary operators
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,
    LeftShift,
    ArithmeticRightShift,
    LogicalRightShift,
}

public static class TokenTypeExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNumber(this TokenType type)
        => type is TokenType.Integer or TokenType.Decimal or TokenType.Hexadecimal;
}

public static class OperatorTypeExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUnaryPrefixOperator(this OperatorType op)
    {
        return op is OperatorType.OpenParentheses
            or OperatorType.UnaryPlus or OperatorType.UnaryMinus
            or OperatorType.UnaryBitwiseNot;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUnaryPostfixOperator(this OperatorType op)
    {
        return op is OperatorType.CloseParentheses
            or OperatorType.UnaryFactorial or OperatorType.UnaryPercentage;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUnaryOperator(this OperatorType op)
        => op.IsUnaryPrefixOperator() || op.IsUnaryPostfixOperator();
}

public readonly ref struct Token(TokenType type, OperatorType @operator, ReadOnlySpan<char> value)
{
    public readonly ReadOnlySpan<char> Value = value;
    public readonly TokenType Type = type;
    public readonly OperatorType Operator = @operator;
}
