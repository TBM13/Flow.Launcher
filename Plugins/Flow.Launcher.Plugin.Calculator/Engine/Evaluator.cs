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
            OperatorType.UnaryPlus or OperatorType.UnaryMinus => 7,
            OperatorType.UnaryFactorial => 8,

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

        if (isUnary)
        {
            Value val = values[--valueCount];
            Value res = ExecuteUnaryOperator(op, val);
            values[valueCount++] = res;
        }
        else
        {
            Value right = values[--valueCount];
            Value left = values[--valueCount];
            Value res = ExecuteBinaryOperator(op, left, right);
            values[valueCount++] = res;
        }

        return true;
    }

    private static Value ExecuteUnaryOperator(OperatorType op, in Value val)
    {
        checked        // Ensure exceptions are thrown when overflow occurs
        {
            Value res = op switch
            {
                // Unary prefix operators
                OperatorType.UnaryPlus => val,
                OperatorType.UnaryMinus => val.IsDecimal
                    ? new Value(-val.AsDecimal())
                    : new Value(-val.AsInt128()),

                // Unary postfix operators
                OperatorType.UnaryFactorial => Factorial(val),

                _ => throw new NotSupportedException($"Unary operator {op} not implemented")
            };

            // If the result is a decimal but does not have a fractional part,
            // convert it to integer since it has a larger scale capacity
            if (res.IsDecimal && decimal.IsInteger(res.AsDecimal()))
                res = new Value(res.AsInt128());

            return res;
        }
    }

    private static Value ExecuteBinaryOperator(OperatorType op, in Value left, in Value right)
    {
        bool useDecimalMath = left.IsDecimal || right.IsDecimal;

        checked     // Ensure exceptions are thrown when overflow occurs
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

            // If the result is a decimal but does not have a fractional part,
            // convert it to integer since it has a larger scale capacity
            if (res.IsDecimal && decimal.IsInteger(res.AsDecimal()))
                res = new Value(res.AsInt128());

            return res;
        }
    }

    private static Value Factorial(in Value val)
    {
        // Factorial is only mathematically valid for non-negative integers
        Int128 n = val.AsInt128();
        if (n < 0)
            throw new ArgumentException("Factorial is not defined for negative numbers");

        checked
        {
            Int128 result = 1;
            for (Int128 i = 2; i <= n; i++)
                result *= i;

            return new Value(result);
        }
    }
}
