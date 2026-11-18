using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Flow.Launcher.PluginSDK.UI;

[TemplatePart(Name = PartTextBox, Type = typeof(TextBox))]
[TemplatePart(Name = PartUpButton, Type = typeof(ButtonBase))]
[TemplatePart(Name = PartDownButton, Type = typeof(ButtonBase))]
public class NumberBox : Control
{
    private const string PartTextBox = "PART_TextBox";
    private const string PartUpButton = "PART_UpButton";
    private const string PartDownButton = "PART_DownButton";

    private TextBox? _textBox;
    private ButtonBase? _upButton;
    private ButtonBase? _downButton;

    static NumberBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NumberBox),
            new FrameworkPropertyMetadata(typeof(NumberBox)));
    }

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(int),
        typeof(NumberBox),
        new PropertyMetadata(-999, OnBoundsChanged));

    public int Minimum
    {
        get => (int)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(int),
        typeof(NumberBox),
        new PropertyMetadata(999, OnBoundsChanged));

    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(int),
        typeof(NumberBox),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        d.CoerceValue(ValueProperty);
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        var box = (NumberBox)d;
        int min = box.Minimum;
        int max = Math.Max(min, box.Maximum);
        return Math.Clamp((int)baseValue, min, max);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NumberBox box && box._textBox is not null)
        {
            box._textBox.Text = ((int)e.NewValue).ToString();
        }
    }

    public override void OnApplyTemplate()
    {
        if (_textBox is not null)
        {
            _textBox.PreviewKeyDown -= OnTextBoxPreviewKeyDown;
            _textBox.LostFocus -= OnTextBoxLostFocus;
        }

        if (_upButton is not null)
        {
            _upButton.Click -= OnUpButtonClick;
        }

        if (_downButton is not null)
        {
            _downButton.Click -= OnDownButtonClick;
        }

        base.OnApplyTemplate();

        _textBox = GetTemplateChild(PartTextBox) as TextBox;
        _upButton = GetTemplateChild(PartUpButton) as ButtonBase;
        _downButton = GetTemplateChild(PartDownButton) as ButtonBase;

        if (_textBox is not null)
        {
            _textBox.Text = Value.ToString();
            _textBox.PreviewKeyDown += OnTextBoxPreviewKeyDown;
            _textBox.LostFocus += OnTextBoxLostFocus;
        }

        if (_upButton is not null)
        {
            _upButton.Click += OnUpButtonClick;
        }

        if (_downButton is not null)
        {
            _downButton.Click += OnDownButtonClick;
        }
    }

    private void OnUpButtonClick(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(ValueProperty, Math.Clamp(Value + 1, Minimum, Math.Max(Minimum, Maximum)));
    }

    private void OnDownButtonClick(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(ValueProperty, Math.Clamp(Value - 1, Minimum, Math.Max(Minimum, Maximum)));
    }

    private void OnTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                SetCurrentValue(ValueProperty, Math.Clamp(Value + 1, Minimum, Math.Max(Minimum, Maximum)));
                e.Handled = true;
                break;
            case Key.Down:
                SetCurrentValue(ValueProperty, Math.Clamp(Value - 1, Minimum, Math.Max(Minimum, Maximum)));
                e.Handled = true;
                break;
            case Key.Enter:
                CommitText();
                e.Handled = true;
                break;
        }
    }

    private void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        CommitText();
    }

    private void CommitText()
    {
        if (_textBox is null)
        {
            return;
        }

        int max = Math.Max(Minimum, Maximum);
        if (string.IsNullOrWhiteSpace(_textBox.Text))
        {
            SetCurrentValue(ValueProperty, Math.Clamp(0, Minimum, max));
        }
        else if (int.TryParse(_textBox.Text, out int parsed))
        {
            SetCurrentValue(ValueProperty, Math.Clamp(parsed, Minimum, max));
        }

        _textBox.Text = Value.ToString();
    }
}
