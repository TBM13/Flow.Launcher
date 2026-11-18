using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Flow.Launcher.PluginSDK.UI;

public static class TextBoxHelper
{
    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.RegisterAttached(
        "PlaceholderText",
        typeof(string),
        typeof(TextBoxHelper),
        new PropertyMetadata(string.Empty, OnPlaceholderTextChanged));

    public static string GetPlaceholderText(DependencyObject obj) => (string)obj.GetValue(PlaceholderTextProperty);

    public static void SetPlaceholderText(DependencyObject obj, string value) => obj.SetValue(PlaceholderTextProperty, value);

    private static void OnPlaceholderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        textBox.Loaded -= OnTextBoxLoaded;
        textBox.Unloaded -= OnTextBoxUnloaded;
        textBox.TextChanged -= OnTextBoxTextChanged;
        textBox.IsVisibleChanged -= OnTextBoxIsVisibleChanged;

        string newText = (string?)e.NewValue ?? string.Empty;
        if (!string.IsNullOrEmpty(newText))
        {
            textBox.Loaded += OnTextBoxLoaded;
            textBox.Unloaded += OnTextBoxUnloaded;
            textBox.TextChanged += OnTextBoxTextChanged;
            textBox.IsVisibleChanged += OnTextBoxIsVisibleChanged;

            if (textBox.IsLoaded)
            {
                UpdatePlaceholder(textBox);
            }
        }
        else
        {
            RemovePlaceholder(textBox);
        }
    }

    private static void OnTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdatePlaceholder(textBox);
        }
    }

    private static void OnTextBoxUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            RemovePlaceholder(textBox);
        }
    }

    private static void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdatePlaceholder(textBox);
        }
    }

    private static void OnTextBoxIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdatePlaceholder(textBox);
        }
    }

    private static void UpdatePlaceholder(TextBox textBox)
    {
        AdornerLayer? layer = AdornerLayer.GetAdornerLayer(textBox);
        if (layer is null)
        {
            return;
        }

        string text = GetPlaceholderText(textBox);
        bool shouldShow = textBox.IsVisible && !string.IsNullOrEmpty(text) && string.IsNullOrEmpty(textBox.Text);

        PlaceholderAdorner? adorner = layer.GetAdorners(textBox)?.OfType<PlaceholderAdorner>().FirstOrDefault();
        if (adorner is null)
        {
            if (!shouldShow)
            {
                return;
            }

            adorner = new PlaceholderAdorner(textBox, text);
            layer.Add(adorner);
        }
        else
        {
            adorner.UpdateText(text);
        }

        adorner.SetVisible(shouldShow);
    }

    private static void RemovePlaceholder(TextBox textBox)
    {
        AdornerLayer? layer = AdornerLayer.GetAdornerLayer(textBox);
        if (layer is null)
        {
            return;
        }

        PlaceholderAdorner[]? adorners = layer.GetAdorners(textBox)?.OfType<PlaceholderAdorner>().ToArray();
        if (adorners is null)
        {
            return;
        }

        foreach (PlaceholderAdorner adorner in adorners)
        {
            layer.Remove(adorner);
        }
    }

    private sealed class PlaceholderAdorner : Adorner
    {
        private readonly TextBlock _textBlock;

        public PlaceholderAdorner(TextBox adornedElement, string text) : base(adornedElement)
        {
            IsHitTestVisible = false;

            _textBlock = new TextBlock
            {
                Text = text,
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                IsHitTestVisible = false
            };
            _textBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");

            AddVisualChild(_textBlock);
        }

        public void UpdateText(string text) => _textBlock.Text = text;

        public void SetVisible(bool isVisible) =>
            Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) =>
            index == 0 ? _textBlock : throw new ArgumentOutOfRangeException(nameof(index));

        protected override Size MeasureOverride(Size constraint)
        {
            _textBlock.Measure(constraint);
            return AdornedElement.RenderSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _textBlock.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}
