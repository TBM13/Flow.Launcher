using System.Globalization;
using Flow.Launcher.Plugin.Calculator.Engine;

namespace Flow.Launcher.Tests.Plugins.Calculator;

public class EvaluatorTests
{
    [Theory]
    // --- Basic Arithmetic & Left-Associativity ---
    [InlineData("0", "0")]
    [InlineData("42", "42")]
    [InlineData("1 + 2 * 3", "7")]
    [InlineData("(1 + 2) * 3", "9")]
    [InlineData("10 - 3 - 2", "5")]
    [InlineData("24 / 4 / 2", "3")]
    [InlineData("0.1 + 0.2", "0.3")]
    [InlineData("10 / 4", "2.5")]
    [InlineData("10 / 2", "5")] // Exact division demotes back to Int128
    [InlineData("   \t 1 + 2 \t  ", "3")] // Spaces and tabs

    // --- Unary Prefix Operators (+, -, ~) ---
    [InlineData("+5", "5")]
    [InlineData("-5", "-5")]
    [InlineData("--5", "5")]
    [InlineData("---5", "-5")]
    [InlineData("+-5", "-5")]
    [InlineData("-+5", "-5")]
    [InlineData("-2.5", "-2.5")]
    [InlineData("--2.5", "2.5")]
    [InlineData("~0", "-1")]
    [InlineData("~-1", "0")]
    [InlineData("-~0", "1")]

    // --- Floor Division (//) Across All Sign Combinations ---
    [InlineData("10 // 3", "3")]      // + // + (with remainder)
    [InlineData("9 // 3", "3")]       // + // + (exact)
    [InlineData("-10 // 3", "-4")]    // - // + (with remainder, floors toward -infinity)
    [InlineData("-9 // 3", "-3")]     // - // + (exact)
    [InlineData("10 // -3", "-4")]    // + // - (with remainder, floors toward -infinity)
    [InlineData("9 // -3", "-3")]     // + // - (exact)
    [InlineData("-10 // -3", "3")]    // - // - (with remainder)
    [InlineData("-9 // -3", "3")]     // - // - (exact)
    [InlineData("7.5 // 2", "3")]     // Decimal + // +
    [InlineData("-7.5 // 2", "-4")]   // Decimal - // +
    [InlineData("7.5 // -2", "-4")]   // Decimal + // -
    [InlineData("-7.5 // -2", "3")]   // Decimal - // -

    // --- Remainder (%) vs Unary Percentage (%) Disambiguation ---
    [InlineData("10 % 3", "1")]
    [InlineData("-10 % 3", "-1")]
    [InlineData("10.5 % 3", "1.5")]
    [InlineData("4%2", "0")]          // No space before digit -> binary remainder
    [InlineData("4%(3)", "1")]        // No space before '(' -> binary remainder
    [InlineData("4%", "0.04")]        // Trailing '%' -> unary percentage
    [InlineData("4% + 1", "1.04")]    // '%' followed by space and binary op -> unary percentage
    [InlineData("4%+1", "1.04")]      // '%' followed directly by binary op -> unary percentage
    [InlineData("200%", "2")]         // Whole-number percentage demotes to Int128

    // --- Contextual vs Non-Contextual Percentages ---
    [InlineData("100 + 10%", "110")]
    [InlineData("100 - 30%", "70")]
    [InlineData("100 + -20%", "80")]    // Unary minus does not block Add context
    [InlineData("100 - -20%", "120")]   // Unary minus does not block Subtract context
    [InlineData("100 + (20%)", "100.2")] // Parentheses isolate context -> non-contextual 0.2
    [InlineData("200 * 50%", "100")]    // Multiply is non-contextual (200 * 0.5)
    [InlineData("100 / 50%", "200")]    // Divide is non-contextual (100 / 0.5)
    // Higher-precedence operator after contextual percentage
    [InlineData("100 - 30% * 2", "99.4")]
    [InlineData("100 + 50% / 2", "100.25")]
    [InlineData("100 + 10% ^ 2", "100.01")]

    // --- Exponentiation (^) ---
    [InlineData("2^0", "1")]
    [InlineData("0^0", "1")]
    [InlineData("2^10", "1024")]
    [InlineData("-3^2", "-9")]        // UnaryMinus (7) < Power (8) -> -(3^2)
    [InlineData("(-3)^2", "9")]
    [InlineData("(-3)^3", "-27")]
    [InlineData("2^(-3)", "0.125")]
    [InlineData("(-2)^(-3)", "-0.125")]
    [InlineData("1.5^2", "2.25")]
    [InlineData("1.5^0", "1")]
    [InlineData("2^(3.0)", "8")]      // Integral decimal exponent is allowed
    // Unary prefix after '^' and right-associative '^'
    [InlineData("2^-3", "0.125")]
    [InlineData("2^+3", "8")]
    [InlineData("2^3^2", "512")]

    // --- Factorials (!) ---
    [InlineData("0!", "1")]
    [InlineData("1!", "1")]
    [InlineData("5!", "120")]
    [InlineData("-5!", "-120")]       // UnaryMinus (7) < Factorial (9) -> -(5!)
    [InlineData("3!!", "720")]        // Chained postfix: (3!)! = 6! = 720
    [InlineData("2^3!", "64")]        // Power (8) < Factorial (9) -> 2^(3!) = 2^6
    [InlineData("(2^3)!", "40320")]   // 8!
    [InlineData("33!", "8683317618811886495518194401280000000")] // Max supported factorial

    // --- Hexadecimal, Binary & Bitwise Operations ---
    [InlineData("0x0", "0")]
    [InlineData("0xFF", "255")]
    [InlineData("0Xff", "255")]
    [InlineData("0xdeadBEEF", "3735928559")]
    [InlineData("0b0", "0")]
    [InlineData("0b1", "1")]
    [InlineData("0b01101", "13")]
    [InlineData("0B101010", "42")]
    [InlineData("0b101 + 0b010", "7")]
    [InlineData("0b1111 & 0x0F", "15")]
    [InlineData("-0b101", "-5")]
    [InlineData("0b1010 << 1", "20")]
    [InlineData("6 & 3", "2")]
    [InlineData("6 | 3", "7")]
    [InlineData("6 ^^ 3", "5")]
    [InlineData("1 | 2 ^^ 3 & 4", "3")] // Precedence: & (3) > ^^ (2) > | (1)
    [InlineData("1 << 2 + 1", "8")]     // Precedence: + (5) > << (4) -> 1 << 3
    [InlineData("16 >> 2", "4")]
    [InlineData("-16 >> 2", "-4")]
    [InlineData("-16 >>> 124", "15")]
    [InlineData("1 << 0", "1")]

    // --- Decimal-to-Int128 Automatic Demotion ---
    [InlineData("(1.5 + 1.5)!", "6")]       // 3.0 demotes to Int128(3), allowing factorial
    [InlineData("(2.5 * 2) << 1", "10")]    // 5.0 demotes to Int128(5), allowing shift
    [InlineData("(10 / 2) & 3", "1")]       // 5.0 demotes to Int128(5), allowing bitwise AND
    [InlineData("200% | 1", "3")]           // 200% = 2.0 -> Int128(2), allowing bitwise OR

    // --- 128-Bit Integer Capacity (Beyond decimal.MaxValue) ---
    [InlineData("2^100", "1267650600228229401496703205376")]
    [InlineData("2^126", "85070591730234615865843651857942052864")]
    [InlineData("0x7FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", "170141183460469231731687303715884105727")] // Int128.MaxValue

    // --- Constants (e, pi, π) ---
    [InlineData("e", "2.7182818284590452353602874714")]
    [InlineData("E", "2.7182818284590452353602874714")]
    [InlineData("pi", "3.1415926535897932384626433833")]
    [InlineData("PI", "3.1415926535897932384626433833")]
    [InlineData("Pi", "3.1415926535897932384626433833")]
    [InlineData("π", "3.1415926535897932384626433833")]
    [InlineData("-pi", "-3.1415926535897932384626433833")]
    [InlineData("+e", "2.7182818284590452353602874714")]
    [InlineData("2 * pi", "6.2831853071795864769252867666")]
    [InlineData("e ^ 0", "1")]
    [InlineData("pi // 1", "3")]
    [InlineData("10%pi", "0.5752220392306202846120698501")]
    public void Evaluate_ValidExpressions_ReturnsExpectedValue(string expression, string expected)
    {
        Value? result = Evaluator.Evaluate(expression);

        Assert.NotNull(result);
        Assert.Equal(expected, result.Value.ToString(CultureInfo.InvariantCulture));
    }

    [Theory]
    // Empty / whitespace
    [InlineData("")]
    [InlineData("   \t  ")]
    // Incomplete expressions
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("~")]
    [InlineData("!")]
    [InlineData("%")]
    [InlineData("1 +")]
    [InlineData("1 *")]
    [InlineData("1 <<")]
    [InlineData("1 ^^")]
    [InlineData("* 2")]
    // Mismatched or empty parentheses
    [InlineData("()")]
    [InlineData("( )")]
    [InlineData("((1 + 2)")]
    [InlineData("(1 + 2))")]
    [InlineData(")1 + 2(")]
    [InlineData("1 (2)")]       // Implicit multiplication is not supported
    [InlineData("(1)(2)")]      // Implicit multiplication is not supported
    [InlineData("5!3")]         // Missing binary operator after postfix operator
    // Malformed numbers
    [InlineData(".5")]          // Leading dot without zero
    [InlineData("5.")]          // Trailing dot
    [InlineData("1.2.3")]       // Multiple dots
    [InlineData("0x")]          // Hex prefix with no digits
    [InlineData("0X")]
    [InlineData("0xG")]         // Invalid hex digit
    [InlineData("0x1.5")]       // Hex with decimal point
    [InlineData("0b")]          // Binary prefix with no digits
    [InlineData("0B")]
    [InlineData("0b2")]         // Invalid binary digit
    [InlineData("0b012")]
    [InlineData("0b1.0")]       // Binary with decimal point
    [InlineData("0b1a")]        // Trailing letters on binary number
    [InlineData("123a")]        // Trailing letters on decimal integer
    // Invalid operators
    [InlineData("1 < 2")]       // Single '<'
    [InlineData("1 > 2")]       // Single '>'
    [InlineData("1 >>>> 2")]    // 4 '>'
    [InlineData("1 <<< 2")]     // 3 '<'
    [InlineData("1 ^^^ 2")]     // 3 '^'
    [InlineData("1 && 2")]      // Logical AND not supported
    [InlineData("1 || 2")]      // Logical OR not supported
    [InlineData("5 ! 2")]       // '!' is not a binary operator
    [InlineData("5 ~ 2")]       // '~' is not a binary operator
    // Invalid constants & implicit multiplication
    [InlineData("p")]
    [InlineData("pia")]
    [InlineData("euler")]
    [InlineData("2pi")]
    [InlineData("2e")]
    [InlineData("pi2")]
    [InlineData("e2")]
    public void Evaluate_InvalidSyntax_ReturnsNull(string expression)
    {
        Value? result = Evaluator.Evaluate(expression);
        Assert.Null(result);
    }

    [Theory]
    // Division by zero
    [InlineData("1 / 0", typeof(DivideByZeroException))]
    [InlineData("1.5 / 0", typeof(DivideByZeroException))]
    [InlineData("1 // 0", typeof(DivideByZeroException))]
    [InlineData("1.5 // 0", typeof(DivideByZeroException))]
    [InlineData("1 % 0", typeof(DivideByZeroException))]
    [InlineData("1.5 % 0", typeof(DivideByZeroException))]
    [InlineData("0^(-1)", typeof(DivideByZeroException))]

    // Factorial domain & range errors
    [InlineData("(-1)!", typeof(ArgumentException))]
    [InlineData("(-10)!", typeof(ArgumentException))]
    [InlineData("34!", typeof(ArgumentException))]
    [InlineData("100!", typeof(ArgumentException))]
    [InlineData("1.5!", typeof(InvalidOperationException))]

    // Power domain errors
    [InlineData("2^1.5", typeof(ArgumentException))]
    [InlineData("2^(-0.5)", typeof(ArgumentException))]

    // Bitwise operations on non-integer decimals
    [InlineData("~1.5", typeof(ArgumentException))]
    [InlineData("1.5 & 1", typeof(ArgumentException))]
    [InlineData("1 & 1.5", typeof(ArgumentException))]
    [InlineData("1.5 | 1", typeof(ArgumentException))]
    [InlineData("1.5 ^^ 1", typeof(ArgumentException))]
    [InlineData("1.5 << 1", typeof(ArgumentException))]
    [InlineData("1 << 1.5", typeof(ArgumentException))]
    [InlineData("1.5 >> 1", typeof(ArgumentException))]
    [InlineData("1.5 >>> 1", typeof(ArgumentException))]

    // Shift amount exceeding Int32
    [InlineData("1 << 3000000000", typeof(OverflowException))]
    [InlineData("1 >> 3000000000", typeof(OverflowException))]
    [InlineData("1 >>> 3000000000", typeof(OverflowException))]

    // Arithmetic & conversion overflows
    [InlineData("2^127", typeof(OverflowException))]          // Exceeds Int128.MaxValue
    [InlineData("(-2)^127 - 1", typeof(OverflowException))]   // Below Int128.MinValue
    [InlineData("-((-2)^127)", typeof(OverflowException))]    // -Int128.MinValue overflows
    [InlineData("2^100 + 0.5", typeof(OverflowException))]    // Int128 > decimal.MaxValue converted to decimal
    [InlineData("2^100 / 2", typeof(OverflowException))]      // Divide converts operands to decimal
    [InlineData("(2^100)%", typeof(OverflowException))]       // Percentage converts operand to decimal
    [InlineData("10.1^100", typeof(OverflowException))]       // Decimal power overflow
    // Constant domain errors
    [InlineData("pi!", typeof(InvalidOperationException))]
    [InlineData("2^pi", typeof(ArgumentException))]
    [InlineData("pi & 1", typeof(ArgumentException))]
    public void Evaluate_MathAndOverflowErrors_ThrowsExpectedException(string expression, Type exceptionType)
    {
        Assert.Throws(exceptionType, () => Evaluator.Evaluate(expression));
    }
}
