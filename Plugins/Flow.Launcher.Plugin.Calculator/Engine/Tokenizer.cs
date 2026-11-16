using System.Runtime.CompilerServices;

namespace Flow.Launcher.Plugin.Calculator.Engine;

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
                            // "4%" is unary percentage but "4 % 2" is binary remainder
                            _index > 0 && !IsWhitespace(_input[_index - 1]) &&
                            // "4%2" and "4%(2)" are binary remainder
                            (_index + 1 == _input.Length || _input[_index + 1] is not (>= '0' and <= '9' or '(' or 'e' or 'E' or 'p' or 'P' or 'π'))
                        => new Token(TokenType.Operator, OperatorType.UnaryPercentage, default),

                    // Basic operations
                    '+' => new Token(TokenType.Operator, OperatorType.Add, default),
                    '-' => new Token(TokenType.Operator, OperatorType.Subtract, default),
                    '*' => new Token(TokenType.Operator, OperatorType.Multiply, default),
                    '/' when _index + 1 < _input.Length && _input[_index + 1] == '/' =>
                        new Token(TokenType.Operator, OperatorType.FloorDivide, default),
                    '/' => new Token(TokenType.Operator, OperatorType.Divide, default),
                    '%' => new Token(TokenType.Operator, OperatorType.Remainder, default),
                    '^' when _index + 1 == _input.Length || _input[_index + 1] != '^' // "4^2" is power but "4^^2" is binary xor
                        => new Token(TokenType.Operator, OperatorType.Power, default),

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
                    // Constants
                    'e' or 'E' => new Token(TokenType.Euler, default, default),
                    'π' => new Token(TokenType.Pi, default, default),
                    'p' or 'P' when _index + 1 < _input.Length && _input[_index + 1] is 'i' or 'I' =>
                        new Token(TokenType.Pi, default, default),

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
                OperatorType.BitwiseXor => 2,
                OperatorType.LogicalRightShift => 3,
                OperatorType.ArithmeticRightShift or OperatorType.LeftShift => 2,

                // Multi-char constants
                _ when Current.Type == TokenType.Pi && c is 'p' or 'P' => 2,

                _ => 1
            };

            return true;
        }

        return false;
    }

    private bool ReadNumber()
    {
        bool isHex = false;
        bool isBinary = false;
        if (_input[_index] == '0' && _index + 1 < _input.Length)
        {
            char prefix = _input[_index + 1];
            isHex = prefix is 'x' or 'X';
            isBinary = prefix is 'b' or 'B';
        }

        int start;
        TokenType type;
        if (isBinary)
        {
            start = _index + 2;
            _index += 2;
            type = TokenType.Binary;

            while (_index < _input.Length && _input[_index] is '0' or '1')
                _index++;
        }
        else if (isHex)
        {
            start = _index + 2;
            _index += 2;
            type = TokenType.Hexadecimal;

            while (_index < _input.Length && char.IsAsciiHexDigit(_input[_index]))
                _index++;
        }
        else
        {
            start = _index;
            type = TokenType.Integer;

            while (_index < _input.Length)
            {
                char next = _input[_index];
                if (char.IsAsciiDigit(next))
                    _index++;
                else if (type == TokenType.Integer && next == '.')
                {
                    type = TokenType.Decimal;
                    _index++;
                }
                else
                    break;
            }
        }

        ReadOnlySpan<char> number = _input[start.._index];
        if (number.Length == 0 || (type == TokenType.Decimal && number[^1] == '.'))
        {
            // Not a valid number. Abort tokenization
            Current = default;
            _index = -1;
            return false;
        }

        Current = new Token(type, default, number);
        return true;
    }
}
