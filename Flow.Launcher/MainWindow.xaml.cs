using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.ViewModel;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using DataObject = System.Windows.DataObject;
using Key = System.Windows.Input.Key;

namespace Flow.Launcher
{
    public partial class MainWindow : IDisposable
    {
        #region Public Property

        // Window Event: Close Event
        public bool CanClose { get; set; } = false;

        #endregion

        #region Private Fields

        // Class Name
        private static readonly string ClassName = nameof(MainWindow);

        // Dependency Injection
        private readonly Settings _settings;
        private readonly Theme _theme;

        private readonly MainViewModel _viewModel;

        // Window Event: Key Event
        private bool _isArrowKeyPressed = false;

        // Window WndProc
        private HwndSource _hwndSource;
        private int _initialWidth;
        private int _initialHeight;

        // ResultListbox
        private ScrollViewer _resultListboxScrollviewer;
        private double _resultListboxVerticalOffset = 0;

        // IDisposable
        private bool _disposed = false;

        #endregion

        #region Constructor

        public MainWindow()
        {
            _settings = Ioc.Default.GetRequiredService<Settings>();
            _theme = Ioc.Default.GetRequiredService<Theme>();
            _viewModel = Ioc.Default.GetRequiredService<MainViewModel>();
            DataContext = _viewModel;

            Topmost = _settings.ShowAtTopmost;

            InitializeComponent();
            UpdatePosition();

            DataObject.AddPastingHandler(QueryTextBox, QueryTextBox_OnPaste);
            _viewModel.ActualApplicationThemeChanged += ViewModel_ActualApplicationThemeChanged;
        }

        #endregion

        #region Window Event

#pragma warning disable VSTHRD100 // Avoid async void methods

        private void ViewModel_ActualApplicationThemeChanged(object sender, ActualApplicationThemeChangedEventArgs args)
        {
            _ = _theme.RefreshFrameAsync();
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            var handle = Win32Helper.GetWindowHandle(this, true);
            _hwndSource = HwndSource.FromHwnd(handle);
            _hwndSource.AddHook(WndProc);
            Win32Helper.HideFromAltTab(this);
            Win32Helper.DisableControlBox(this);
        }

        private void OnLoaded(object sender, RoutedEventArgs _)
        {
            // Check first launch
            if (_settings.FirstLaunch)
            {
                // Set First Launch to false
                _settings.FirstLaunch = false;

                // Save settings
                App.API.SaveAppAllSettings();
            }

            // Hide window if need
            UpdatePosition();
            if (_settings.HideOnStartup)
            {
                _viewModel.Hide();
            }
            else
            {
                _viewModel.Show();
            }

            // Initialize color scheme
            if (_settings.ColorScheme == Constant.Light)
            {
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            }
            else if (_settings.ColorScheme == Constant.Dark)
            {
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            }

            // Force update position
            UpdatePosition();

            // Initialize resize mode after refreshing frame
            SetupResizeMode();

            // Reset preview
            _viewModel.ResetPreview();

            // Since the default main window visibility is visible, so we need set focus during startup
            QueryTextBox.Focus();

            // Set the initial state of the QueryTextBoxCursorMovedToEnd property
            // Without this part, when shown for the first time, switching the context menu does not move the cursor to the end.
            _viewModel.QueryTextCursorMovedToEnd = false;

            // View model property changed event
            _viewModel.PropertyChanged += (o, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(MainViewModel.MainWindowVisibilityStatus):
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (_viewModel.MainWindowVisibilityStatus)
                                {
                                    // Update position & Activate
                                    UpdatePosition();
                                    Activate();

                                    // Reset preview
                                    _viewModel.ResetPreview();

                                    // Select last query if need
                                    if (!_viewModel.LastQuerySelected)
                                    {
                                        QueryTextBox.SelectAll();
                                        _viewModel.LastQuerySelected = true;
                                    }

                                    // Focus query box
                                    QueryTextBox.Focus();
                                }
                            });
                            break;
                        }
                    case nameof(MainViewModel.QueryTextCursorMovedToEnd):
                        if (_viewModel.QueryTextCursorMovedToEnd)
                        {
                            // QueryTextBox seems to be update with a DispatcherPriority as low as ContextIdle.
                            // To ensure QueryTextBox is up to date with QueryText from the View, we need to Dispatch with such a priority
                            Dispatcher.Invoke(() => QueryTextBox.CaretIndex = QueryTextBox.Text.Length);
                            _viewModel.QueryTextCursorMovedToEnd = false;
                        }
                        break;

                    case nameof(MainViewModel.SelectedResults):
                        _resultListboxScrollviewer ??= WpfHelper.FindVisualChild<ScrollViewer>(ResultListBox);
                        if (_viewModel.QueryResultsSelected())
                        {
                            // Restore previous scroll position
                            _resultListboxScrollviewer.ScrollToVerticalOffset(_resultListboxVerticalOffset);
                        }
                        else
                        {
                            // Save current scroll position
                            _resultListboxVerticalOffset = _resultListboxScrollviewer.VerticalOffset;
                        }

                        break;

                    case nameof(MainViewModel.QueryText):
                        if (QueryTextBox.Text == _viewModel.QueryText)
                            return;

                        // By using BeginChange and EndChange we allow CTRL + Z to undo this query
                        QueryTextBox.BeginChange();
                        QueryTextBox.Text = _viewModel.QueryText;
                        QueryTextBox.EndChange();
                        break;
                }
            };

            // Settings property changed event
            _settings.PropertyChanged += (o, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(Settings.Language):
                        if (_settings.ShowHomePage && _viewModel.QueryResultsSelected() && string.IsNullOrEmpty(_viewModel.QueryText))
                        {
                            _viewModel.QueryResults();
                        }
                        break;
                    case nameof(Settings.WindowLeft):
                        Left = _settings.WindowLeft;
                        break;
                    case nameof(Settings.WindowTop):
                        Top = _settings.WindowTop;
                        break;
                    case nameof(Settings.KeepMaxResults):
                        SetupResizeMode();
                        break;
                    case nameof(Settings.ShowHomePage):
                        if (_viewModel.QueryResultsSelected() && string.IsNullOrEmpty(_viewModel.QueryText))
                        {
                            _viewModel.QueryResults();
                        }
                        break;
                    case nameof(Settings.ShowAtTopmost):
                        Topmost = _settings.ShowAtTopmost;
                        break;
                }
            };

            // Initialize query state
            if (_settings.ShowHomePage && string.IsNullOrEmpty(_viewModel.QueryText))
            {
                _viewModel.QueryResults();
            }
        }

        private async void OnClosing(object sender, CancelEventArgs e)
        {
            if (!CanClose)
            {
                CanClose = true;
                App.API.SaveAppAllSettings();
                e.Cancel = true;
                await PluginManager.DisposePluginsAsync();
                Notification.Uninstall();
                // After plugins are all disposed, we shutdown application to close app
                // We use this instead of Close() to avoid InvalidOperationException when calling Close() in OnClosing event
                Application.Current.Shutdown();
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            try
            {
                _hwndSource.RemoveHook(WndProc);
            }
            catch (Exception)
            {
                // Ignored
            }

            _hwndSource = null;
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            if (IsLoaded)
            {
                _settings.WindowLeft = Left;
                _settings.WindowTop = Top;
            }
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;

            // This condition stops extra hide call when animator is on,
            // which causes the toggling to occasional hide instead of show.
            if (_viewModel.MainWindowVisibilityStatus)
            {
                if (_settings.HideWhenDeactivated && !_viewModel.ExternalPreviewVisible)
                {
                    _viewModel.Hide();
                }
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            var specialKeyState = GlobalHotkey.CheckModifiers();
            switch (e.Key)
            {
                case Key.Down:
                    _isArrowKeyPressed = true;
                    _viewModel.SelectNextItemCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Up:
                    _isArrowKeyPressed = true;
                    _viewModel.SelectPrevItemCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.PageDown:
                    _viewModel.SelectNextPageCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.PageUp:
                    _viewModel.SelectPrevPageCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Right:
                    if (_viewModel.QueryResultsSelected()
                        && QueryTextBox.CaretIndex == QueryTextBox.Text.Length
                        && !string.IsNullOrEmpty(QueryTextBox.Text))
                    {
                        _viewModel.LoadContextMenuCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.Left:
                    if (!_viewModel.QueryResultsSelected() && QueryTextBox.CaretIndex == 0)
                    {
                        _viewModel.EscCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.Back:
                    if (specialKeyState.CtrlPressed)
                    {
                        if (_viewModel.QueryResultsSelected()
                            && QueryTextBox.Text.Length > 0
                            && QueryTextBox.CaretIndex == QueryTextBox.Text.Length)
                        {
                            if (QueryTextBox.Text.Contains('\\') || QueryTextBox.Text.Contains('/'))
                            {
                                _viewModel.BackspaceCommand.Execute(null);
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

        #region Window Boarder Event

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            // When the window is maximized via Snap,
            // dragging attempts will first switch the window from Maximized to Normal state,
            // and adjust the drag position accordingly.
            if (e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    if (WindowState == WindowState.Maximized)
                    {
                        // Calculate ratio based on maximized window dimensions
                        double maxWidth = ActualWidth;
                        double maxHeight = ActualHeight;
                        var mousePos = e.GetPosition(this);
                        double xRatio = mousePos.X / maxWidth;
                        double yRatio = mousePos.Y / maxHeight;

                        // Current monitor information
                        var screen = MonitorInfo.GetNearestDisplayMonitor(new WindowInteropHelper(this).Handle);
                        var workingArea = screen.WorkingArea;
                        var screenLeftTop = Win32Helper.TransformPixelsToDIP(this, workingArea.X, workingArea.Y);

                        // Switch to Normal state
                        WindowState = WindowState.Normal;

                        Application.Current?.Dispatcher.Invoke(new Action(() =>
                        {
                            double normalWidth = Width;
                            double normalHeight = Height;

                            // Apply ratio based on the difference between maximized and normal window sizes
                            Left = screenLeftTop.X + (maxWidth - normalWidth) * xRatio;
                            Top = screenLeftTop.Y + (maxHeight - normalHeight) * yRatio;

                            if (Mouse.LeftButton == MouseButtonState.Pressed)
                            {
                                DragMove();
                            }
                        }), DispatcherPriority.ApplicationIdle);
                    }
                    else
                    {
                        DragMove();
                    }
                }
                catch (InvalidOperationException)
                {
                    // Ignored - can occur if drag operation is already in progress
                }
            }
        }

        #endregion

        #region Window Context Menu Event

        private void OnContextMenusForSettingsClick(object sender, RoutedEventArgs e)
        {
            _viewModel.Hide();
            App.API.OpenSettingDialog();
        }

        #endregion

        #region Window WndProc

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case Win32Helper.WM_ENTERSIZEMOVE:
                    _initialWidth = (int)Width;
                    _initialHeight = (int)Height;
                    handled = true;
                    break;
                case Win32Helper.WM_EXITSIZEMOVE:
                    //Prevent updating the number of results when the window height is below the height of a single result item.
                    //This situation occurs not only when the user manually resizes the window, but also when the window is released from a side snap, as the OS automatically adjusts the window height.
                    //(Without this check, releasing from a snap can cause the window height to hit the minimum, resulting in only 2 results being shown.)
                    if (_initialHeight != (int)Height && Height > (_settings.WindowHeightSize + _settings.ItemHeightSize))
                    {
                        if (!_settings.KeepMaxResults)
                        {
                            // Get shadow margin
                            var shadowMargin = 0;
                            var useDropShadowEffect = _settings.UseDropShadowEffect;
                            if (useDropShadowEffect)
                            {
                                shadowMargin = 32;
                            }

                            // Calculate max results to show
                            var itemCount = (Height - (_settings.WindowHeightSize + 14) - shadowMargin) / _settings.ItemHeightSize;
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
                        if (!_settings.KeepMaxResults)
                        {
                            // Update width
                            _viewModel.MainWindowWidth = Width;
                        }

                        SizeToContent = SizeToContent.Height;
                    }

                    handled = true;
                    break;
                case Win32Helper.WM_NCLBUTTONDBLCLK: // Block the double click in frame
                    SizeToContent = SizeToContent.Height;
                    handled = true;
                    break;
                case Win32Helper.WM_SYSCOMMAND: // Block Maximize/Minimize by Win+Up and Win+Down Arrow
                    var command = wParam.ToInt32() & 0xFFF0;
                    if (command == Win32Helper.SC_MAXIMIZE || command == Win32Helper.SC_MINIMIZE)
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

        private void UpdatePosition()
        {
            // Initialize call twice to work around multi-display alignment issue- https://github.com/Flow-Launcher/Flow.Launcher/issues/2910
            InitializePosition();
            InitializePosition();
        }

        private async Task PositionResetAsync()
        {
            _viewModel.Show();
            await Task.Delay(300); // If don't give a time, Positioning will be weird.
            var screen = SelectedScreen();
            Left = HorizonCenter(screen);
            Top = VerticalCenter(screen);
        }

        private void InitializePosition()
        {
            // Initialize call twice to work around multi-display alignment issue- https://github.com/Flow-Launcher/Flow.Launcher/issues/2910
            InitializePositionInner();
            InitializePositionInner();
            return;

            void InitializePositionInner()
            {
                if (_settings.SearchWindowScreen == SearchWindowScreens.RememberLastLaunchLocation)
                {
                    var previousScreenWidth = _settings.PreviousScreenWidth;
                    var previousScreenHeight = _settings.PreviousScreenHeight;
                    GetDpi(out var previousDpiX, out var previousDpiY);

                    _settings.PreviousScreenWidth = SystemParameters.VirtualScreenWidth;
                    _settings.PreviousScreenHeight = SystemParameters.VirtualScreenHeight;
                    GetDpi(out var currentDpiX, out var currentDpiY);

                    if (previousScreenWidth != 0 && previousScreenHeight != 0 &&
                        previousDpiX != 0 && previousDpiY != 0 &&
                        (previousScreenWidth != SystemParameters.VirtualScreenWidth ||
                         previousScreenHeight != SystemParameters.VirtualScreenHeight ||
                         previousDpiX != currentDpiX || previousDpiY != currentDpiY))
                    {
                        AdjustPositionForResolutionChange();
                        return;
                    }

                    Left = _settings.WindowLeft;
                    Top = _settings.WindowTop;
                }
                else
                {
                    var screen = SelectedScreen();
                    switch (_settings.SearchWindowAlign)
                    {
                        case SearchWindowAligns.Center:
                            Left = HorizonCenter(screen);
                            Top = VerticalCenter(screen);
                            break;
                        case SearchWindowAligns.CenterTop:
                            Left = HorizonCenter(screen);
                            Top = VerticalTop(screen);
                            break;
                        case SearchWindowAligns.LeftTop:
                            Left = HorizonLeft(screen);
                            Top = VerticalTop(screen);
                            break;
                        case SearchWindowAligns.RightTop:
                            Left = HorizonRight(screen);
                            Top = VerticalTop(screen);
                            break;
                        case SearchWindowAligns.Custom:
                            var customLeft = Win32Helper.TransformPixelsToDIP(this,
                                screen.WorkingArea.X + _settings.CustomWindowLeft, 0);
                            var customTop = Win32Helper.TransformPixelsToDIP(this, 0,
                                screen.WorkingArea.Y + _settings.CustomWindowTop);
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

            var previousLeft = _settings.WindowLeft;
            var previousTop = _settings.WindowTop;
            GetDpi(out var previousDpiX, out var previousDpiY);

            var widthRatio = screenWidth / _settings.PreviousScreenWidth;
            var heightRatio = screenHeight / _settings.PreviousScreenHeight;
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

        private MonitorInfo SelectedScreen()
        {
            MonitorInfo screen;
            switch (_settings.SearchWindowScreen)
            {
                case SearchWindowScreens.Cursor:
                    screen = MonitorInfo.GetCursorDisplayMonitor();
                    break;
                case SearchWindowScreens.Focus:
                    screen = MonitorInfo.GetNearestDisplayMonitor(Win32Helper.GetForegroundWindow());
                    break;
                case SearchWindowScreens.Primary:
                    screen = MonitorInfo.GetPrimaryDisplayMonitor();
                    break;
                case SearchWindowScreens.Custom:
                    var allScreens = MonitorInfo.GetDisplayMonitors();
                    if (_settings.CustomScreenNumber <= allScreens.Count)
                        screen = allScreens[_settings.CustomScreenNumber - 1];
                    else
                        screen = allScreens[0];
                    break;
                default:
                    screen = MonitorInfo.GetDisplayMonitors()[0];
                    break;
            }

            return screen ?? MonitorInfo.GetDisplayMonitors()[0];
        }

        private double HorizonCenter(MonitorInfo screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip2.X - ActualWidth) / 2 + dip1.X;
            return left;
        }

        private double VerticalCenter(MonitorInfo screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var dip2 = Win32Helper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            var top = (dip2.Y - QueryTextBox.ActualHeight) / 4 + dip1.Y;
            return top;
        }

        private double HorizonRight(MonitorInfo screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip1.X + dip2.X - ActualWidth) - 10;
            return left;
        }

        private double HorizonLeft(MonitorInfo screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var left = dip1.X + 10;
            return left;
        }

        public double VerticalTop(MonitorInfo screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var top = dip1.Y + 10;
            return top;
        }

        #endregion

        #region QueryTextBox Event

        private void QueryTextBox_OnCopy(object sender, ExecutedRoutedEventArgs e)
        {
            var result = _viewModel.SelectedResults.SelectedItem?.Result;
            if (QueryTextBox.SelectionLength == 0 && result != null)
            {
                string copyText = result.CopyText;
                App.API.CopyToClipboard(copyText, directCopy: true);
            }
            else if (!string.IsNullOrEmpty(QueryTextBox.Text))
            {
                App.API.CopyToClipboard(QueryTextBox.SelectedText, showDefaultNotification: false);
            }
        }

        private void QueryTextBox_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            try
            {
                var isText = e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
                if (isText)
                {
                    var text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
                    text = text.Replace(Environment.NewLine, " ");
                    DataObject data = new DataObject();
                    data.SetData(DataFormats.UnicodeText, text);
                    e.DataObject = data;
                }
            }
            catch (Exception ex)
            {
                App.API.LogException(ClassName, "Failed to paste text", ex);
            }
        }

        private void QueryTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (_viewModel.QueryText != QueryTextBox.Text)
            {
                BindingExpression be = QueryTextBox.GetBindingExpression(TextBox.TextProperty);
                be.UpdateSource();
            }
        }

        private void QueryTextBox_OnPreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        #endregion

        #region Resize Mode

        private void SetupResizeMode()
        {
            ResizeMode = _settings.KeepMaxResults ? ResizeMode.NoResize : ResizeMode.CanResize;
            if (WindowChrome.GetWindowChrome(this) is WindowChrome windowChrome)
            {
                _theme.SetResizeBorderThickness(windowChrome, _settings.KeepMaxResults);
            }
        }

        #endregion

        #region Search

        private void QueryTextBox_TextChanged1(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            _viewModel.QueryText = textBox.Text;
            _viewModel.Query(false);
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _hwndSource?.Dispose();
                    _viewModel.ActualApplicationThemeChanged -= ViewModel_ActualApplicationThemeChanged;
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
