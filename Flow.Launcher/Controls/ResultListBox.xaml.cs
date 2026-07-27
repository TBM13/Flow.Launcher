using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Controls;

public partial class ResultListBox
{
    private Point _lastpos, _dragStart;
    private string? _path;

    public ScrollViewer ScrollViewer { get; private set; } = null!;

    public ResultListBox()
    {
        InitializeComponent();
    }

    public override void OnApplyTemplate()
    {
        ScrollViewer = (ScrollViewer)GetTemplateChild("ListBoxScrollViewer");
    }

    public static readonly DependencyProperty RightClickResultCommandProperty =
        DependencyProperty.Register(nameof(RightClickResultCommand), typeof(ICommand), typeof(ResultListBox), new UIPropertyMetadata(null));

    public ICommand RightClickResultCommand
    {
        get => (ICommand)GetValue(RightClickResultCommandProperty);
        set => SetValue(RightClickResultCommandProperty, value);
    }

    public static readonly DependencyProperty LeftClickResultCommandProperty =
        DependencyProperty.Register(nameof(LeftClickResultCommand), typeof(ICommand), typeof(ResultListBox), new UIPropertyMetadata(null));

    public ICommand LeftClickResultCommand
    {
        get => (ICommand)GetValue(LeftClickResultCommandProperty);
        set => SetValue(LeftClickResultCommandProperty, value);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] != null)
            ScrollIntoView(e.AddedItems[0]);
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        _lastpos = e.GetPosition(null);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        Point p = e.GetPosition(null);
        if (_lastpos != p && sender is ListBoxItem item)
        {
            if (!item.IsSelected)
                item.IsSelected = true;

            _lastpos = p;
        }
    }

    private void OnItemLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        // Unsubscribe first to prevent duplicate registrations during UI recycling
        element.DataContextChanged -= OnItemDataContextChanged;
        element.DataContextChanged += OnItemDataContextChanged;
        SetupScreenPositionDelegate(element);
    }

    private void OnItemUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            element.DataContextChanged -= OnItemDataContextChanged;

            if (element.DataContext is ResultViewModel viewModel)
                viewModel.GetScreenCenterPoint = null;
        }
    }

    private void OnItemDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        if (e.OldValue is ResultViewModel oldViewModel)
            oldViewModel.GetScreenCenterPoint = null;

        SetupScreenPositionDelegate(element);
    }

    private static void SetupScreenPositionDelegate(FrameworkElement element)
    {
        if (element.DataContext is ResultViewModel viewModel)
            viewModel.GetScreenCenterPoint = () => CalculateScreenCenterPoint(element);
    }

    private static Point? CalculateScreenCenterPoint(FrameworkElement element)
    {
        if (!element.IsLoaded || PresentationSource.FromVisual(element) is null)
            return null;

        try
        {
            Point centerPoint = new(element.ActualWidth / 2, element.ActualHeight / 2);
            return element.PointToScreen(centerPoint);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private void ResultList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.DirectlyOver is not FrameworkElement
            {
                DataContext: ResultViewModel { Result.CopyText: { } copyText }
            })
            return;

        _path = copyText;
        _dragStart = e.GetPosition(null);
    }

    private void ResultList_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _path is null)
        {
            _dragStart = default;
            _path = null;
            return;
        }

        Point mousePosition = e.GetPosition(null);
        Vector diff = _dragStart - mousePosition;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        bool pathExists = File.Exists(_path) || Directory.Exists(_path);
        if (!pathExists)
        {
            _path = null;
            return;
        }

        IPublicAPI.Instance.HideMainWindow();
        DataObject data = new(DataFormats.FileDrop, new[]
        {
            _path
        });

        _path = null;
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move | DragDropEffects.Copy);
    }

    private void ResultListBox_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.DirectlyOver is FrameworkElement { DataContext: ResultViewModel result })
            RightClickResultCommand?.Execute(result.Result);
    }

    private void ResultListBox_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.DirectlyOver is FrameworkElement { DataContext: ResultViewModel })
            LeftClickResultCommand?.Execute(null);
    }
}
