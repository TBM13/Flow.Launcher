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

            // Unary prefix (+x, -x, ~x)
            OperatorType.UnaryPlus or OperatorType.UnaryMinus
                or OperatorType.UnaryBitwiseNot => 7,

            // Exponentiation (to make "-3^2" evaluate as "-(3^2)" instead of "(-3)^2")
            OperatorType.Power => 8,

            // Unary Postfix (x!, x%) (highest precedence)
            OperatorType.UnaryFactorial or OperatorType.UnaryPercentage => 9,

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
                case TokenType.Binary:
                    Int128 b = Int128.Parse(token.Value, NumberStyles.BinaryNumber, CultureInfo.InvariantCulture);
                    values[valueCount++] = new Value(b);
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
                            if (!op.IsUnaryPrefixOperator())
                            {
                                int opPrecedence = GetPrecedence(op);
                                bool isRightAssociative = op == OperatorType.Power;

                                while (opCount > 0)
                                {
                                    int topPrecedence = GetPrecedence(operators[opCount - 1]);
                                    if (topPrecedence > opPrecedence || (topPrecedence == opPrecedence && !isRightAssociative))
                                    {
                                        if (!ExecuteTopOperator(values, operators, ref valueCount, ref opCount, op))
                                            return null;
                                    }
                                    else
                                        break;
                                }
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
        Span<Value> values, ReadOnlySpan<OperatorType> operators,
        ref int valueCount, ref int opCount,
        OperatorType nextOp = OperatorType.Invalid)
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
                res = op switch
                {
                    // Unary prefix operators
                    OperatorType.UnaryPlus => +val,
                    OperatorType.UnaryMinus => -val,
                    OperatorType.UnaryBitwiseNot => ~val,

                    // Unary postfix operators
                    OperatorType.UnaryFactorial => val.Factorial(),

                    _ => throw new NotSupportedException($"Unary operator {op} not implemented")
                };
            else
                res = ExecuteUnaryPercentage(
                    val, values, operators, valueCount, opCount, nextOp);
        }
        else
        {
            Value right = values[--valueCount];
            Value left = values[--valueCount];
            res = op switch
            {
                // Basic operations
                OperatorType.Add => left + right,
                OperatorType.Subtract => left - right,
                OperatorType.Multiply => left * right,
                OperatorType.Divide => left / right,
                OperatorType.FloorDivide => left.FloorDivide(right),
                OperatorType.Remainder => left % right,
                OperatorType.Power => left.Pow(right),

                // Bitwise operations
                OperatorType.BitwiseAnd => left & right,
                OperatorType.BitwiseOr => left | right,
                OperatorType.BitwiseXor => left ^ right,
                OperatorType.LeftShift => left << right,
                OperatorType.ArithmeticRightShift => left >> right,
                OperatorType.LogicalRightShift => left >>> right,

                _ => throw new NotSupportedException($"Binary operator {op} not implemented")
            };
        }

        values[valueCount++] = res;
        return true;
    }

    private static Value ExecuteUnaryPercentage(in Value value,
        ReadOnlySpan<Value> values, ReadOnlySpan<OperatorType> operators,
        int valueCount, int opCount,
        OperatorType nextOp)
    {
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
        // and no higher-precedence operator follows the percentage
        // E.g: "100 - 30%" = 70,     "50 + 10%" = 55,   "100 - 30% * 2" = 99.4
        bool isContextual = valueCount > 0
            && contextOp is OperatorType.Add or OperatorType.Subtract
            && (nextOp == OperatorType.Invalid || GetPrecedence(nextOp) <= GetPrecedence(contextOp));

        Value percentageFactor = value / new Value(100m);
        if (isContextual)
        {
            // Left operand of the binary operator
            Value baseVal = values[valueCount - 1];
            return baseVal * percentageFactor;
        }

        return percentageFactor;
    }
}
