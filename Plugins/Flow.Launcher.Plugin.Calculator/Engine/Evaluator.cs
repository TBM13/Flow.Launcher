using System.Globalization;
using System.Runtime.CompilerServices;

namespace Flow.Launcher.Plugin.Calculator.Engine;

public static class Evaluator
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetPrecedence(OperatorType op)
    {
        return op switch
        {
            // Parentheses (lowest precedence)
            OperatorType.OpenParentheses => 0,

            // Bitwise operations
            OperatorType.BitwiseOr => 1,
            OperatorType.BitwiseXor => 2,
            OperatorType.BitwiseAnd => 3,
            OperatorType.LeftShift or OperatorType.ArithmeticRightShift or OperatorType.LogicalRightShift => 4,

            // Basic operations
            OperatorType.Add or OperatorType.Subtract => 5,
            OperatorType.Multiply or OperatorType.Divide
                or OperatorType.FloorDivide or OperatorType.Remainder => 6,

            // Unary Operators (highest precedence)
            // Prefix
            OperatorType.UnaryPlus or OperatorType.UnaryMinus
                or OperatorType.UnaryBitwiseNot => 7,
            // Postfix
            OperatorType.UnaryFactorial or OperatorType.UnaryPercentage => 8,

            _ => throw new InvalidOperationException($"Operator {op} does not have a precedence defined")
        };
    }


    public static Value? Evaluate(string input)
    {
        Tokenizer tokenizer = new(input);
        bool hasNext = tokenizer.MoveNext();
        if (!hasNext)
            // Exit early to avoid creating the stacks
            return null;

        // 32 should be enough for most expressions. However, expressions such as:
        // (((((((((((((((((((((((((((((((((1+1)))))))))))))))))))))))))))))))))
        // will cause an exception
        Span<Value> values = stackalloc Value[32];
        Span<OperatorType> operators = stackalloc OperatorType[32];
        int valueCount = 0;
        int opCount = 0;

        while (hasNext)
        {
            Token token = tokenizer.Current;
            switch (token.Type)
            {
                // Numbers
                case TokenType.Integer:
                    Int128 i = Int128.Parse(token.Value, CultureInfo.InvariantCulture);
                    values[valueCount++] = new Value(i);
                    break;
                case TokenType.Decimal:
                    decimal d = decimal.Parse(token.Value, CultureInfo.InvariantCulture);
                    values[valueCount++] = new Value(d);
                    break;
                case TokenType.Hexadecimal:
                    Int128 h = Int128.Parse(token.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    values[valueCount++] = new Value(h);
                    break;

                // Operators
                case TokenType.Operator:
                    OperatorType op = token.Operator;
                    switch (op)
                    {
                        case OperatorType.OpenParentheses:
                            operators[opCount++] = token.Operator;
                            break;
                        case OperatorType.CloseParentheses:
                            // Pop and execute until we find the matching '('
                            while (opCount > 0 && operators[opCount - 1] != OperatorType.OpenParentheses)
                            {
                                if (!ExecuteTopOperator(values, operators, ref valueCount, ref opCount))
                                    return null;
                            }

                            if (opCount == 0)
                                return null;    // Mismatched parenthesis

                            opCount--; // Remove '('
                            break;

                        default:
                            int opPrecedence = GetPrecedence(op);
                            bool isRightAssociative = op.IsUnaryPrefixOperator();

                            // While the top operator on the stack has higher or equal precedence, run it first
                            while (opCount > 0)
                            {
                                int topPrecedence = GetPrecedence(operators[opCount - 1]);
                                if (topPrecedence > opPrecedence || (topPrecedence == opPrecedence && !isRightAssociative))
                                {
                                    if (!ExecuteTopOperator(values, operators, ref valueCount, ref opCount))
                                        return null;
                                }
                                else
                                    break;
                            }

                            operators[opCount++] = op;
                            break;
                    }

                    break;
            }

            hasNext = tokenizer.MoveNext();
        }

        // Abort on invalid token
        if (tokenizer.Current.Type == TokenType.Invalid)
            return null;

        // Evaluate remaining operators
        while (opCount > 0)
        {
            if (operators[opCount - 1] == OperatorType.OpenParentheses)
                return null;        // Mismatched parenthesis

            if (!ExecuteTopOperator(values, operators, ref valueCount, ref opCount))
                return null;
        }

        if (valueCount != 1)
            return null;        // Invalid expression

        return values[--valueCount];
    }

    private static bool ExecuteTopOperator(
        Span<Value> values, Span<OperatorType> operators, ref int valueCount, ref int opCount)
    {
        OperatorType op = operators[--opCount];
        bool isUnary = op.IsUnaryOperator();
        int requiredOperands = isUnary ? 1 : 2;

        if (valueCount < requiredOperands)
            return false;       // Invalid expression structure

        Value res;
        if (isUnary)
        {
            Value val = values[--valueCount];

            if (op != OperatorType.UnaryPercentage)
                res = ExecuteUnaryOperator(op, val);
            else
                res = ExecuteUnaryPercentage(val, values, operators, valueCount, opCount);
        }
        else
        {
            Value right = values[--valueCount];
            Value left = values[--valueCount];
            res = ExecuteBinaryOperator(op, left, right);
        }

        values[valueCount++] = res;
        return true;
    }

    private static Value ExecuteUnaryOperator(OperatorType op, in Value val)
    {
        checked
        {
            Value res = op switch
            {
                // Unary prefix operators
                OperatorType.UnaryPlus => val,
                OperatorType.UnaryMinus => val.IsDecimal
                    ? new Value(-val.AsDecimal())
                    : new Value(-val.AsInt128()),
                OperatorType.UnaryBitwiseNot => !val.IsDecimal
                    ? new Value(~val.AsInt128())
                    : throw new InvalidOperationException("Bitwise operations not supported on decimal values"),

                // Unary postfix operators
                OperatorType.UnaryFactorial => val.Factorial(),

                _ => throw new NotSupportedException($"Unary operator {op} not implemented")
            };

            // Convert decimal to int128 if there's no fractional part to maximize capacity
            if (res.IsDecimal && decimal.IsInteger(res.AsDecimal()))
                res = new Value(res.AsInt128());

            return res;
        }
    }

    private static Value ExecuteBinaryOperator(OperatorType op, in Value left, in Value right)
    {
        bool useDecimalMath = left.IsDecimal || right.IsDecimal;

        checked
        {
            Value res = op switch
            {
                // Basic operations
                OperatorType.Add => useDecimalMath
                    ? new Value(left.AsDecimal() + right.AsDecimal())
                    : new Value(left.AsInt128() + right.AsInt128()),
                OperatorType.Subtract => useDecimalMath
                    ? new Value(left.AsDecimal() - right.AsDecimal())
                    : new Value(left.AsInt128() - right.AsInt128()),
                OperatorType.Multiply => useDecimalMath
                    ? new Value(left.AsDecimal() * right.AsDecimal())
                    : new Value(left.AsInt128() * right.AsInt128()),
                // For normal division, always use decimal math to preserve fractional results
                OperatorType.Divide => new Value(left.AsDecimal() / right.AsDecimal()),
                OperatorType.FloorDivide => useDecimalMath
                    ? new Value(Math.Floor(left.AsDecimal() / right.AsDecimal()))
                    : new Value((left.AsInt128() / right.AsInt128()) -
                        ((left.AsInt128() % right.AsInt128() != 0 && (left.AsInt128() ^ right.AsInt128()) < 0) ? 1 : 0)),
                OperatorType.Remainder => useDecimalMath
                    ? new Value(left.AsDecimal() % right.AsDecimal())
                    : new Value(left.AsInt128() % right.AsInt128()),

                // Bitwise operations
                OperatorType.BitwiseAnd => !useDecimalMath
                    ? new Value(left.AsInt128() & right.AsInt128())
                    : throw new InvalidOperationException("Bitwise operations not supported on decimal values"),
                OperatorType.BitwiseOr => !useDecimalMath
                    ? new Value(left.AsInt128() | right.AsInt128())
                    : throw new InvalidOperationException("Bitwise operations not supported on decimal values"),
                OperatorType.BitwiseXor => !useDecimalMath
                    ? new Value(left.AsInt128() ^ right.AsInt128())
                    : throw new InvalidOperationException("Bitwise operations not supported on decimal values"),
                OperatorType.LeftShift => !useDecimalMath
                    ? new Value(left.AsInt128() << right.AsInt32())
                    : throw new InvalidOperationException("Bitwise operations not supported on decimal values"),
                OperatorType.ArithmeticRightShift => !useDecimalMath
                    ? new Value(left.AsInt128() >> right.AsInt32())
                    : throw new InvalidOperationException("Bitwise operations not supported on decimal values"),
                OperatorType.LogicalRightShift => !useDecimalMath
                    ? new Value(left.AsInt128() >>> right.AsInt32())
                    : throw new InvalidOperationException("Bitwise operations not supported on decimal values"),

                _ => throw new NotSupportedException($"Binary operator {op} not implemented")
            };

            // Convert decimal to int128 if there's no fractional part to maximize capacity
            if (res.IsDecimal && decimal.IsInteger(res.AsDecimal()))
                res = new Value(res.AsInt128());

            return res;
        }
    }

    private static Value ExecuteUnaryPercentage(in Value value,
        Span<Value> values, Span<OperatorType> operators, int valueCount, int opCount)
    {
        Value res;

        // Find the closest binary operator in the same context
        OperatorType contextOp = OperatorType.Invalid;
        for (int i = opCount - 1; i >= 0; i--)
        {
            OperatorType op = operators[i];
            if (op.IsUnaryOperator() && op is not OperatorType.OpenParentheses)
                continue;

            contextOp = op;
            break;
        }

        // Contextual percentage applies if the binary operator is Add or Subtract
        // E.g: "100 - 30%" = 70     "50 + 10%" = 55
        bool isContextual = valueCount > 0 && contextOp is OperatorType.Add or OperatorType.Subtract;
        // TODO: The next token might have higher precedence than the binary operator
        // E.g.: "100 - 30% * 2" should ideally be evaluated as "100 - (30% * 2)"
        // but is currently evaluated as "100 - (30 * 2)%"

        checked
        {
            decimal percentageFactor = value.AsDecimal() / 100m;
            if (isContextual)
            {
                // Left operand of the binary operator
                Value baseVal = values[valueCount - 1];
                res = new Value(baseVal.AsDecimal() * percentageFactor);
            }
            else
                res = new Value(percentageFactor);
        }

        // Convert decimal to int128 if there's no fractional part to maximize capacity
        if (res.IsDecimal && decimal.IsInteger(res.AsDecimal()))
            res = new Value(res.AsInt128());

        return res;
    }
}
