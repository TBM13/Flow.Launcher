using System.Runtime.CompilerServices;

namespace Flow.Launcher.Plugin.Calculator.Engine;

public enum TokenType : byte
{
    Invalid = default,

    Integer,
    Decimal,
    Operator,
}

public enum OperatorType : byte
{
    Invalid = default,

    // Unary prefix operators
    OpenParentheses,
    UnaryPlus,
    UnaryMinus,

    // Unary postfix operators
    CloseParentheses,
    UnaryFactorial,
    UnaryPercentage,

    // Basic operations
    Add,
    Subtract,
    Multiply,
    Divide,
    Remainder,
    FloorDivide,

    // Bitwise operations
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,
    LeftShift,
    ArithmeticRightShift,
    LogicalRightShift,
}

public static class OperatorTypeExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUnaryPrefixOperator(this OperatorType op)
    {
        return op is OperatorType.OpenParentheses or OperatorType.UnaryPlus or OperatorType.UnaryMinus;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUnaryPostfixOperator(this OperatorType op)
    {
        return op is OperatorType.CloseParentheses or OperatorType.UnaryFactorial or OperatorType.UnaryPercentage;
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

public ref struct Tokenizer(ReadOnlySpan<char> input)
{
    private readonly ReadOnlySpan<char> _input = input;
    private int _index = 0;

    public Token Current { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWhitespace(char c) => c is ' ' or '\t';

    /// <returns>True if there are more valid tokens to read.</returns>
    public bool MoveNext()
    {
        if (_index == -1)
            // Tokenization aborted
            return false;

        while (_index < _input.Length)
        {
            char c = _input[_index];

            // Spaces
            if (IsWhitespace(c))
            {
                _index++;
                continue;
            }

            if (Current.Type is TokenType.Decimal or TokenType.Integer
                || Current.Operator.IsUnaryPostfixOperator())
            {
                // Previous token was a number (or a postfix operator like factorial)
                // so we are expecting an unary postfix operator or a binary operator
                Current = c switch
                {
                    // Unary postfix operators
                    ')' => new Token(TokenType.Operator, OperatorType.CloseParentheses, default),
                    '!' => new Token(TokenType.Operator, OperatorType.UnaryFactorial, default),
                    '%' when
                            // 4% is unary percentage but 4 % 2 is binary remainder
                            _index > 0 && !IsWhitespace(_input[_index - 1]) &&
                            // 4%2 is binary remainder
                            (_index + 1 == _input.Length || !char.IsAsciiDigit(_input[_index + 1]))
                        => new Token(TokenType.Operator, OperatorType.UnaryPercentage, default),

                    // Basic operations
                    '+' => new Token(TokenType.Operator, OperatorType.Add, default),
                    '-' => new Token(TokenType.Operator, OperatorType.Subtract, default),
                    '*' => new Token(TokenType.Operator, OperatorType.Multiply, default),
                    '/' when _index + 1 < _input.Length && _input[_index + 1] == '/' =>
                        new Token(TokenType.Operator, OperatorType.FloorDivide, default),
                    '/' => new Token(TokenType.Operator, OperatorType.Divide, default),
                    '%' => new Token(TokenType.Operator, OperatorType.Remainder, default),

                    // Bitwise operations
                    '&' => new Token(TokenType.Operator, OperatorType.BitwiseAnd, default),
                    '|' => new Token(TokenType.Operator, OperatorType.BitwiseOr, default),
                    '^' => new Token(TokenType.Operator, OperatorType.BitwiseXor, default),
                    '>' when _index + 2 < _input.Length && _input[_index + 1] == '>' && _input[_index + 2] == '>' =>
                        new Token(TokenType.Operator, OperatorType.LogicalRightShift, default),
                    '>' when _index + 1 < _input.Length && _input[_index + 1] == '>' =>
                        new Token(TokenType.Operator, OperatorType.ArithmeticRightShift, default),
                    '<' when _index + 1 < _input.Length && _input[_index + 1] == '<' =>
                        new Token(TokenType.Operator, OperatorType.LeftShift, default),

                    _ => default
                };
            }
            else
            {
                // Previous token was a binary operator or an unary prefix operator,
                // so we are expecting a number or an unary prefix operator
                if (char.IsAsciiDigit(c) || c == '.')
                    return ReadNumber();

                Current = c switch
                {
                    // Unary prefix operators
                    '(' => new Token(TokenType.Operator, OperatorType.OpenParentheses, default),
                    '+' => new Token(TokenType.Operator, OperatorType.UnaryPlus, default),
                    '-' => new Token(TokenType.Operator, OperatorType.UnaryMinus, default),

                    _ => default
                };
            }

            // Abort if token is invalid
            if (Current.Type == TokenType.Invalid)
            {
                _index = -1;
                return false;
            }

            _index += Current.Operator switch
            {
                // Multi-char basic operations
                OperatorType.FloorDivide => 2,

                // Multi-char bitwise operations
                OperatorType.LogicalRightShift => 3,
                OperatorType.ArithmeticRightShift or OperatorType.LeftShift => 2,

                _ => 1
            };

            return true;
        }

        return false;
    }

    private bool ReadNumber()
    {
        char c = _input[_index];

        int start = _index;
        bool isDecimal = c == '.';
        _index++;

        while (_index < _input.Length)
        {
            char next = _input[_index];
            if (char.IsAsciiDigit(next))
                _index++;
            else if (next == '.')
            {
                if (isDecimal)
                {
                    // Multiple dots, not a valid number (e.g. "42.6.3")
                    // Abort tokenization
                    Current = default;
                    _index = -1;
                    return false;
                }

                isDecimal = true;
                _index++;
            }
            else
                break;
        }

        ReadOnlySpan<char> number = _input[start.._index];
        if (isDecimal && (number[0] == '.' || number[^1] == '.'))
        {
            // Not a valid number. Abort tokenization
            Current = default;
            _index = -1;
            return false;
        }

        Current = new Token(isDecimal ? TokenType.Decimal : TokenType.Integer, default, number);
        return true;
    }
}
