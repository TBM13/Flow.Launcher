using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.Helper;
using Flow.Launcher.Interop;
using Flow.Launcher.Interop.Hardware;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Logging;
using Flow.Launcher.ViewModel;
using iNKORE.UI.WPF.Modern;
using DataObject = System.Windows.DataObject;
using Key = System.Windows.Input.Key;

namespace Flow.Launcher;

public partial class MainWindow : Window
{
    // Window Event: Close Event
    public bool CanClose { get; set; } = false;

    private readonly Logger<MainWindow> _logger;
    private readonly MainViewModel _vm;
    private readonly ISettingsAPI _settings;
    private readonly PluginManager _pluginManager;

    // Window Event: Key Event
    private bool _isArrowKeyPressed = false;

    // Window WndProc
    private HwndSource? _hwndSource;
    private int _initialWidth;
    private int _initialHeight;

    // ResultListbox
    private double _resultListboxVerticalOffset = 0;

    public MainWindow(Logger<MainWindow> logger, MainViewModel viewModel,
        ISettingsAPI settings, PluginManager pluginManager)
    {
        _logger = logger;
        _vm = viewModel;
        _settings = settings;
        _pluginManager = pluginManager;
        DataContext = _vm;

        Topmost = _settings.ShowAtTopmost;

        InitializeComponent();

        DataObject.AddPastingHandler(QueryTextBox, QueryTextBox_OnPaste);
    }

    #region Window Event

#pragma warning disable VSTHRD100 // Avoid async void methods

    private void OnSourceInitialized(object sender, EventArgs e)
    {
        nint handle = WindowHelper.GetWindowHandle(this, true);
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource.AddHook(WndProc);
        WindowHelper.HideFromAltTab(this);
        WindowHelper.DisableControlBox(this);
    }

    private void OnLoaded(object sender, RoutedEventArgs _)
    {
        // Hide window if need
        if (_settings.HideOnStartup)
        {
            _vm.Hide();
        }
        else
        {
            _vm.Show();
        }

        // Initialize color scheme
        if (_settings.ColorScheme == ColorScheme.Light)
        {
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
        }
        else if (_settings.ColorScheme == ColorScheme.Dark)
        {
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
        }

        // Initialize resize mode after refreshing frame
        SetupResizeMode();

        // Reset preview
        _vm.ResetPreview();

        // Since the default main window visibility is visible, so we need set focus during startup
        QueryTextBox.Focus();

        // Set the initial state of the QueryTextBoxCursorMovedToEnd property
        // Without this part, when shown for the first time, switching the context menu does not move the cursor to the end.
        _vm.QueryTextCursorMovedToEnd = false;

        // View model property changed event
        _vm.PropertyChanged += (o, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.MainWindowVisibilityStatus):
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (_vm.MainWindowVisibilityStatus)
                            {
                                // Update position & Activate
                                UpdatePosition();
                                Activate();

                                // Reset preview
                                _vm.ResetPreview();

                                // Select last query if need
                                if (!_vm.LastQuerySelected)
                                {
                                    QueryTextBox.SelectAll();
                                    _vm.LastQuerySelected = true;
                                }

                                // Focus query box
                                QueryTextBox.Focus();
                            }
                        });
                        break;
                    }
                case nameof(MainViewModel.QueryTextCursorMovedToEnd):
                    if (_vm.QueryTextCursorMovedToEnd)
                    {
                        // QueryTextBox seems to be update with a DispatcherPriority as low as ContextIdle.
                        // To ensure QueryTextBox is up to date with QueryText from the View, we need to Dispatch with such a priority
                        Dispatcher.Invoke(() => QueryTextBox.CaretIndex = QueryTextBox.Text.Length);
                        _vm.QueryTextCursorMovedToEnd = false;
                    }
                    break;

                case nameof(MainViewModel.SelectedResults):
                    if (_vm.QueryResultsSelected())
                    {
                        // Restore previous scroll position
                        ResultListBox.ScrollViewer.ScrollToVerticalOffset(_resultListboxVerticalOffset);
                    }
                    else
                    {
                        // Save current scroll position
                        _resultListboxVerticalOffset = ResultListBox.ScrollViewer.VerticalOffset;
                    }

                    break;

                case nameof(MainViewModel.QueryText):
                    if (QueryTextBox.Text == _vm.QueryText)
                        return;

                    // By using BeginChange and EndChange we allow CTRL + Z to undo this query
                    QueryTextBox.BeginChange();
                    QueryTextBox.Text = _vm.QueryText;
                    QueryTextBox.EndChange();
                    break;
            }
        };

        // Settings property changed event
        _settings.PropertyChanged += (o, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(ISettingsAPI.FixedWindowSize):
                    SetupResizeMode();
                    break;
                case nameof(ISettingsAPI.ShowHomePage):
                    if (_vm.QueryResultsSelected() && string.IsNullOrEmpty(_vm.QueryText))
                    {
                        _vm.QueryResults();
                    }
                    break;
                case nameof(ISettingsAPI.ShowAtTopmost):
                    Topmost = _settings.ShowAtTopmost;
                    break;
            }
        };

        // Initialize query state
        if (_settings.ShowHomePage && string.IsNullOrEmpty(_vm.QueryText))
        {
            _vm.QueryResults();
        }
    }

    private async void OnClosing(object sender, CancelEventArgs e)
    {
        // TODO: Would it be better to move this to OnClosed?
        if (!CanClose)
        {
            CanClose = true;
            IPublicAPI.Instance.SaveAppAllSettings();
            e.Cancel = true;
            await _pluginManager.DisposePluginsAsync();
            // After plugins are all disposed, we shutdown application to close app
            // We use this instead of Close() to avoid InvalidOperationException when calling Close() in OnClosing event
            Application.Current.Shutdown();
        }
    }

    private void OnClosed(object sender, EventArgs e)
    {
        try
        {
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource?.Dispose();
        }
        catch (Exception)
        {
            // Ignored
        }
        finally
        {
            _hwndSource = null;
        }
    }

    private void OnDeactivated(object sender, EventArgs e)
    {
        // This condition stops extra hide call when animator is on,
        // which causes the toggling to occasional hide instead of show.
        if (_vm.MainWindowVisibilityStatus)
        {
            if (_settings.HideOnLostFocus)
            {
                _vm.Hide();
            }
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                _isArrowKeyPressed = true;
                _vm.SelectedResults.SelectNextResult();
                e.Handled = true;
                break;
            case Key.Up:
                _isArrowKeyPressed = true;
                _vm.SelectedResults.SelectPrevResult();
                e.Handled = true;
                break;
            case Key.PageDown:
                _vm.SelectedResults.SelectNextPage();
                e.Handled = true;
                break;
            case Key.PageUp:
                _vm.SelectedResults.SelectPrevPage();
                e.Handled = true;
                break;
            case Key.Right:
                if (_vm.QueryResultsSelected()
                    && QueryTextBox.CaretIndex == QueryTextBox.Text.Length)
                {
                    _vm.LoadContextMenuCommand.Execute(null);
                    e.Handled = true;
                }
                break;
            case Key.Left:
                if (!_vm.QueryResultsSelected() && QueryTextBox.CaretIndex == 0)
                {
                    _vm.EscCommand.Execute(null);
                    e.Handled = true;
                }
                break;
            case Key.Back:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    if (_vm.QueryResultsSelected()
                        && QueryTextBox.Text.Length > 0
                        && QueryTextBox.CaretIndex == QueryTextBox.Text.Length)
                    {
                        if (QueryTextBox.Text.Contains('\\') || QueryTextBox.Text.Contains('/'))
                        {
                            _vm.BackspaceCommand.Execute(null);
                            e.Handled = true;
                        }
                    }
                }
                break;
            default:
                break;
        }
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Up || e.Key == Key.Down)
        {
            _isArrowKeyPressed = false;
        }
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isArrowKeyPressed)
        {
            e.Handled = true; // Ignore Mouse Hover when press Arrowkeys
        }
    }

#pragma warning restore VSTHRD100 // Avoid async void methods

    #endregion

    #region Window Context Menu Event

    private void OnContextMenusForSettingsClick(object sender, RoutedEventArgs e)
    {
        _vm.Hide();
        IPublicAPI.Instance.OpenSettingDialog();
    }

    #endregion

    #region Window WndProc

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WindowHelper.WM_ENTERSIZEMOVE:
                _initialWidth = (int)Width;
                _initialHeight = (int)Height;
                handled = true;
                break;
            case WindowHelper.WM_EXITSIZEMOVE:
                //Prevent updating the number of results when the window height is below the height of a single result item.
                //This situation occurs not only when the user manually resizes the window, but also when the window is released from a side snap, as the OS automatically adjusts the window height.
                //(Without this check, releasing from a snap can cause the window height to hit the minimum, resulting in only 2 results being shown.)
                if (_initialHeight != (int)Height && Height > (QueryTextBox.Height + Const.ItemHeightSize))
                {
                    if (!_settings.FixedWindowSize)
                    {
                        // Get shadow margin
                        var shadowMargin = 0;

                        // Calculate max results to show
                        var itemCount = (Height - (QueryTextBox.Height + 14) - shadowMargin) / Const.ItemHeightSize;
                        if (itemCount < 2)
                        {
                            _settings.MaxResultsToShow = 2;
                        }
                        else
                        {
                            _settings.MaxResultsToShow = Convert.ToInt32(Math.Truncate(itemCount));
                        }
                    }

                    SizeToContent = SizeToContent.Height;
                }
                else
                {
                    // Update height when exiting maximized snap state.
                    SizeToContent = SizeToContent.Height;
                }

                if (_initialWidth != (int)Width)
                {
                    if (!_settings.FixedWindowSize)
                    {
                        // Update width
                        _settings.WindowWidth = Width;
                    }

                    SizeToContent = SizeToContent.Height;
                }

                handled = true;
                break;
            case WindowHelper.WM_NCLBUTTONDBLCLK: // Block the double click in frame
                SizeToContent = SizeToContent.Height;
                handled = true;
                break;
            case WindowHelper.WM_SYSCOMMAND: // Block Maximize/Minimize by Win+Up and Win+Down Arrow
                var command = wParam.ToInt32() & 0xFFF0;
                if (command == WindowHelper.SC_MAXIMIZE || command == WindowHelper.SC_MINIMIZE)
                {
                    SizeToContent = SizeToContent.Height;
                    handled = true;
                }
                break;
        }

        return IntPtr.Zero;
    }

    #endregion

    #region Window Position

    /// <summary>
    /// Avoid calling this function at startup since we can't access the monitor information while the user is logging-in.
    /// </summary>
    private void UpdatePosition()
    {
        // Initialize call twice to work around multi-display alignment issue- https://github.com/Flow-Launcher/Flow.Launcher/issues/2910
        InitializePosition();
        InitializePosition();
    }

    /// <summary>
    /// Avoid calling this function at startup since we can't access the monitor information while the user is logging-in.
    /// </summary>
    private void InitializePosition()
    {
        // Initialize call twice to work around multi-display alignment issue- https://github.com/Flow-Launcher/Flow.Launcher/issues/2910
        InitializePositionInner();
        InitializePositionInner();
        return;

        void InitializePositionInner()
        {
            if (_settings.Display == DisplayType.RememberLastDisplay)
            {
                var lastDisplayWidth = _settings.LastDisplayWidth;
                var lastDisplayHeight = _settings.LastDisplayHeight;
                GetDpi(out var previousDpiX, out var previousDpiY);

                _settings.LastDisplayWidth = SystemParameters.VirtualScreenWidth;
                _settings.LastDisplayHeight = SystemParameters.VirtualScreenHeight;
                GetDpi(out var currentDpiX, out var currentDpiY);

                if (lastDisplayWidth != 0 && lastDisplayHeight != 0 &&
                    previousDpiX != 0 && previousDpiY != 0 &&
                    (lastDisplayWidth != SystemParameters.VirtualScreenWidth ||
                     lastDisplayHeight != SystemParameters.VirtualScreenHeight ||
                     previousDpiX != currentDpiX || previousDpiY != currentDpiY))
                {
                    AdjustPositionForResolutionChange();
                    return;
                }
            }
            else
            {
                var screen = SelectedScreen();
                switch (_settings.DisplayPosition)
                {
                    case DisplayPosition.Center:
                        Left = HorizonCenter(screen);
                        Top = VerticalCenter(screen);
                        break;
                    case DisplayPosition.CenterTop:
                        Left = HorizonCenter(screen);
                        Top = VerticalTop(screen);
                        break;
                    case DisplayPosition.LeftTop:
                        Left = HorizonLeft(screen);
                        Top = VerticalTop(screen);
                        break;
                    case DisplayPosition.RightTop:
                        Left = HorizonRight(screen);
                        Top = VerticalTop(screen);
                        break;
                    case DisplayPosition.Custom:
                        var customLeft = WpfHelper.TransformPixelsToDIP(this,
                            screen.WorkingArea.X + _settings.CustomDisplayPositionLeft, 0);
                        var customTop = WpfHelper.TransformPixelsToDIP(this, 0,
                            screen.WorkingArea.Y + _settings.CustomDisplayPositionTop);
                        Left = customLeft.X;
                        Top = customTop.Y;
                        break;
                }
            }
        }
    }

    private void AdjustPositionForResolutionChange()
    {
        var screenWidth = SystemParameters.VirtualScreenWidth;
        var screenHeight = SystemParameters.VirtualScreenHeight;
        GetDpi(out var currentDpiX, out var currentDpiY);

        var previousLeft = Left;
        var previousTop = Top;
        GetDpi(out var previousDpiX, out var previousDpiY);

        var widthRatio = screenWidth / _settings.LastDisplayWidth;
        var heightRatio = screenHeight / _settings.LastDisplayHeight;
        var dpiXRatio = currentDpiX / previousDpiX;
        var dpiYRatio = currentDpiY / previousDpiY;

        var newLeft = previousLeft * widthRatio * dpiXRatio;
        var newTop = previousTop * heightRatio * dpiYRatio;

        var screenLeft = SystemParameters.VirtualScreenLeft;
        var screenTop = SystemParameters.VirtualScreenTop;

        var maxX = screenLeft + screenWidth - ActualWidth;
        var maxY = screenTop + screenHeight - ActualHeight;

        Left = Math.Max(screenLeft, Math.Min(newLeft, maxX));
        Top = Math.Max(screenTop, Math.Min(newTop, maxY));
    }

    private void GetDpi(out double dpiX, out double dpiY)
    {
        var source = PresentationSource.FromVisual(this);
        if (source != null && source.CompositionTarget != null)
        {
            var matrix = source.CompositionTarget.TransformToDevice;
            dpiX = 96 * matrix.M11;
            dpiY = 96 * matrix.M22;
        }
        else
        {
            dpiX = 96;
            dpiY = 96;
        }
    }

    /// <summary>
    /// Avoid calling this function at startup since we can't access the monitor information while the user is logging-in.
    /// </summary>
    private MonitorInfo SelectedScreen()
    {
        MonitorInfo? screen;
        switch (_settings.Display)
        {
            case DisplayType.Cursor:
                screen = MonitorHelper.GetCursorDisplayMonitor();
                break;
            case DisplayType.Focus:
                screen = MonitorHelper.GetNearestDisplayMonitor(WindowHelper.GetForegroundWindow());
                break;
            case DisplayType.Primary:
                screen = MonitorHelper.GetPrimaryDisplayMonitor();
                break;
            case DisplayType.Custom:
                var allScreens = MonitorHelper.GetDisplayMonitors();
                if (_settings.DisplayNumber <= allScreens.Count)
                    screen = allScreens[_settings.DisplayNumber - 1];
                else
                    screen = allScreens[0];
                break;
            default:
                screen = MonitorHelper.GetPrimaryDisplayMonitor();
                break;
        }

        return screen ?? MonitorHelper.GetPrimaryDisplayMonitor();
    }

    private double HorizonCenter(MonitorInfo screen)
    {
        var dip1 = WpfHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
        var dip2 = WpfHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
        var left = (dip2.X - ActualWidth) / 2 + dip1.X;
        return left;
    }

    private double VerticalCenter(MonitorInfo screen)
    {
        var dip1 = WpfHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
        var dip2 = WpfHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
        var top = (dip2.Y - QueryTextBox.ActualHeight) / 4 + dip1.Y;
        return top;
    }

    private double HorizonRight(MonitorInfo screen)
    {
        var dip1 = WpfHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
        var dip2 = WpfHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
        var left = (dip1.X + dip2.X - ActualWidth) - 10;
        return left;
    }

    private double HorizonLeft(MonitorInfo screen)
    {
        var dip1 = WpfHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
        var left = dip1.X + 10;
        return left;
    }

    public double VerticalTop(MonitorInfo screen)
    {
        var dip1 = WpfHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
        var top = dip1.Y + 10;
        return top;
    }

    #endregion

    #region QueryTextBox Event

    private void QueryTextBox_OnCopy(object sender, ExecutedRoutedEventArgs e)
    {
        var result = _vm.SelectedResults.SelectedItem?.Result;
        if (QueryTextBox.SelectionLength == 0 && result != null)
        {
            string copyText = result.CopyText;
            IPublicAPI.Instance.CopyToClipboard(copyText, directCopy: true);
        }
        else if (!string.IsNullOrEmpty(QueryTextBox.Text))
        {
            IPublicAPI.Instance.CopyToClipboard(QueryTextBox.SelectedText, showDefaultNotification: false);
        }
    }

    private void QueryTextBox_OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        try
        {
            if (e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
            {
                if (e.SourceDataObject.GetData(DataFormats.UnicodeText) is string text)
                {
                    // Normalize all Unix (\n), Mac (\r), and Windows (\r\n) line breaks to a single space
                    string cleanText = text.Replace("\r\n", " ")
                                        .Replace("\n", " ")
                                        .Replace("\r", " ");

                    DataObject data = new();
                    data.SetData(DataFormats.UnicodeText, cleanText);
                    e.DataObject = data;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to sanitize pasted text");
        }
    }

    private void QueryTextBox_OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
    }

    #endregion

    private void SetupResizeMode()
    {
        ResizeMode = _settings.FixedWindowSize ? ResizeMode.NoResize : ResizeMode.CanResize;
        if (WindowChrome.GetWindowChrome(this) is WindowChrome windowChrome)
        {
            if (_settings.FixedWindowSize)
                windowChrome.ResizeBorderThickness = new(0);
            else
                windowChrome.ResizeBorderThickness = SystemParameters.WindowResizeBorderThickness;
        }
    }

    private void QueryTextBox_TextChanged1(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            _vm.QueryText = tb.Text;
            _vm.Query();
        }
    }
}
