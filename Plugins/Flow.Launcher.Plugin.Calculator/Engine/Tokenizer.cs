using System.Runtime.CompilerServices;

namespace Flow.Launcher.Plugin.Calculator.Engine;

public ref struct Tokenizer(ReadOnlySpan<char> input)
{
    private readonly ReadOnlySpan<char> _input = input;
    private int _index = 0;

    public Token Current { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWhitespace(char c) => c is ' ' or '\t';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHexChar(char c) => c is >= 'a' and <= 'f' or >= 'A' and <= 'F';

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

            if (Current.Type.IsNumber() || Current.Operator.IsUnaryPostfixOperator())
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
                            (_index + 1 == _input.Length || _input[_index + 1] is not (>= '0' and <= '9' or '('))
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
                if (char.IsAsciiDigit(c))
                    return ReadNumber();

                Current = c switch
                {
                    // Unary prefix operators
                    '(' => new Token(TokenType.Operator, OperatorType.OpenParentheses, default),
                    '+' => new Token(TokenType.Operator, OperatorType.UnaryPlus, default),
                    '-' => new Token(TokenType.Operator, OperatorType.UnaryMinus, default),
                    '~' => new Token(TokenType.Operator, OperatorType.UnaryBitwiseNot, default),

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
        bool isDecimal = false;
        bool isHex = _input[_index] == '0' && _index + 1 < _input.Length
                       && _input[_index + 1] is 'x' or 'X';

        int start;
        if (isHex)
        {
            start = _index + 2;
            _index += 2;
        }
        else
        {
            start = _index;
            _index++;
        }

        while (_index < _input.Length)
        {
            char next = _input[_index];
            if (char.IsAsciiDigit(next) || (isHex && IsHexChar(next)))
                _index++;
            else if (next == '.')
            {
                if (isDecimal || isHex)
                {
                    // Multiple dots or an hex number with a dot. Abort
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
        if (number.Length == 0 || (isDecimal && number[^1] == '.'))
        {
            // Not a valid number. Abort tokenization
            Current = default;
            _index = -1;
            return false;
        }

        TokenType type = isHex
            ? TokenType.Hexadecimal
            : isDecimal ? TokenType.Decimal : TokenType.Integer;

        Current = new Token(type, default, number);
        return true;
    }
}
