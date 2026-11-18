using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin.Calculator.Engine;

namespace Flow.Launcher.Plugin.Calculator.Preview;

public partial class CalculatorPreviewPanel : UserControl
{
    private sealed record OperatorHelpItem(string Syntax, string Description);
    private sealed record OperatorHelpSection(string Title, IReadOnlyList<OperatorHelpItem> Items);

    private static readonly IReadOnlyList<OperatorHelpSection> OperatorHelpSections =
    [
        new("Number Formats", [
            new("123, 3.14", "Decimal / integer"),
            new("0xFF", "Hexadecimal"),
            new("0b1010", "Binary"),
            new("pi, π, e", "Pi and Euler's number"),
        ]),
        new("Arithmetic", [
            new("+, -", "Add, subtract, or unary sign"),
            new("*, /", "Multiply, divide"),
            new("//", "Floor divide"),
            new("%", "Remainder (a % b) or percentage (50%, 100 - 30%)"),
            new("^", "Power (a ^ b)"),
            new("!", "Factorial (n!)"),
            new("( )", "Grouping"),
        ]),
        new("Bitwise (Integers)", [
            new("&, |, ^^", "AND, OR, XOR"),
            new("~", "Bitwise NOT (~a)"),
            new("<<", "Left shift"),
            new(">>, >>>", "Arithmetic, logical right shift"),
        ]),
    ];

    private readonly Int128 _integerValue;
    private readonly Action<Int128> _changeValue;
    private readonly Action<string> _copyText;
    private readonly string _decimalText;
    private readonly string _formattedText;

    public CalculatorPreviewPanel(Value value, Action<Int128> changeValue, Action<string> copyText)
    {
        _changeValue = changeValue;
        _copyText = copyText;

        InitializeComponent();
        HelpSectionsItemsControl.ItemsSource = OperatorHelpSections;

        if (value.IsDecimal)
        {
            decimal decimalValue = value.AsDecimal();
            _decimalText = decimalValue.ToString(CultureInfo.InvariantCulture);
            _formattedText = decimalValue.ToString("#,##0.#############################", CultureInfo.CurrentCulture);
            IntegerOnlyPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            _integerValue = value.AsInt128();
            _decimalText = _integerValue.ToString(CultureInfo.InvariantCulture);
            _formattedText = _integerValue.ToString("N0", CultureInfo.CurrentCulture);
            HexTextBlock.Text = $"0x{_integerValue.ToString("X", CultureInfo.InvariantCulture)}";

            IReadOnlyList<BinaryGroup> groups = BinaryNumber.GetGroups(_integerValue);
            BitGroupsItemsControl.ItemsSource = groups;

            if (BinaryNumber.GetSeparateSignBit(_integerValue, groups) is { } signBit)
            {
                SignBitPresenter.Content = signBit;
                SignBitPresenter.Visibility = Visibility.Visible;
            }
        }

        DecimalTextBlock.Text = _decimalText;
        FormattedTextBlock.Text = _formattedText;
    }

    private void Decimal_Click(object sender, RoutedEventArgs e)
    {
        _copyText(_decimalText);
    }

    private void Formatted_Click(object sender, RoutedEventArgs e)
    {
        _copyText(_formattedText);
    }

    private void Hex_Click(object sender, RoutedEventArgs e)
    {
        _copyText(HexTextBlock.Text);
    }

    private void Binary_Click(object sender, RoutedEventArgs e)
    {
        _copyText($"0b{_integerValue.ToString("B", CultureInfo.InvariantCulture)}");
    }

    private void Bit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: int bitIndex })
            return;

        _changeValue(_integerValue ^ (Int128.One << bitIndex));
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        HelpPopup.IsOpen = !HelpPopup.IsOpen;
    }
}
