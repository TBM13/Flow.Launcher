using System;
using System.ComponentModel;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.ViewModel;
using Microsoft.Win32;
using ModernWpf.Controls;
using DataObject = System.Windows.DataObject;
using Key = System.Windows.Input.Key;
using MouseButtons = System.Windows.Forms.MouseButtons;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using Screen = System.Windows.Forms.Screen;

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

        // Window Notify Icon
        private NotifyIcon _notifyIcon;

        // Window Context Menu
        private readonly ContextMenu _contextMenu = new();
        private readonly MainViewModel _viewModel;

        // Window Event: Key Event
        private bool _isArrowKeyPressed = false;

        // Window WndProc
        private HwndSource _hwndSource;
        private int _initialWidth;
        private int _initialHeight;

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

                // Update release notes version
                _settings.ReleaseNotesVersion = Constant.Version;

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

            // Initialize context menu & notify icon
            InitializeContextMenu();
            InitializeNotifyIcon();

            // Initialize color scheme
            if (_settings.ColorScheme == Constant.Light)
            {
                ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Light;
            }
            else if (_settings.ColorScheme == Constant.Dark)
            {
                ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Dark;
            }

            // Initialize position
            InitProgressbarAnimation();

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

                                    // Update activate times
                                    _settings.ActivateTimes++;
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
                    case nameof(MainViewModel.GameModeStatus):
                        _notifyIcon.Icon = _viewModel.GameModeStatus
                            ? Properties.Resources.gamemode
                            : Properties.Resources.app;
                        break;
                }
            };

            // Settings property changed event
            _settings.PropertyChanged += (o, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(Settings.HideNotifyIcon):
                        _notifyIcon.Visible = !_settings.HideNotifyIcon;
                        break;
                    case nameof(Settings.Language):
                        UpdateNotifyIconText();
                        if (_settings.ShowHomePage && _viewModel.QueryResultsSelected() && string.IsNullOrEmpty(_viewModel.QueryText))
                        {
                            _viewModel.QueryResults();
                        }
                        break;
                    case nameof(Settings.Hotkey):
                        UpdateNotifyIconText();
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
                _notifyIcon.Visible = false;
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

            _viewModel.SearchIconOpacity = 0.0;

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
                            var queryWithoutActionKeyword =
                                QueryBuilder.Build(QueryTextBox.Text.Trim(), PluginManager.NonGlobalPlugins)?.Search;

                            if (FilesFolders.IsLocationPathString(queryWithoutActionKeyword))
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
                        var screen = Screen.FromHandle(new WindowInteropHelper(this).Handle);
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

        #region Window Notify Icon

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Text = Constant.FlowLauncherFullName,
                Icon = Constant.Version == "1.0.0" ? Properties.Resources.dev : Properties.Resources.app,
                Visible = !_settings.HideNotifyIcon
            };

            _notifyIcon.MouseClick += (o, e) =>
            {
                switch (e.Button)
                {
                    case MouseButtons.Left:
                        _viewModel.ToggleFlowLauncher();
                        break;
                    case MouseButtons.Right:

                        _contextMenu.IsOpen = true;
                        // Get context menu handle and bring it to the foreground
                        if (PresentationSource.FromVisual(_contextMenu) is HwndSource hwndSource)
                        {
                            Win32Helper.SetForegroundWindow(hwndSource.Handle);
                        }

                        _contextMenu.Focus();
                        break;
                }
            };
        }

        private void UpdateNotifyIconText()
        {
            var menu = _contextMenu;
            ((MenuItem)menu.Items[0]).Header = App.API.GetTranslation("iconTrayOpen") +
                                               " (" + _settings.Hotkey + ")";
            ((MenuItem)menu.Items[1]).Header = App.API.GetTranslation("GameMode");
            ((MenuItem)menu.Items[2]).Header = App.API.GetTranslation("PositionReset");
            ((MenuItem)menu.Items[3]).Header = App.API.GetTranslation("iconTraySettings");
            ((MenuItem)menu.Items[4]).Header = App.API.GetTranslation("iconTrayExit");
        }

        private void InitializeContextMenu()
        {
            var menu = _contextMenu;
            menu.Items.Clear();
            var openIcon = new FontIcon { Glyph = "\ue71e" };
            var open = new MenuItem
            {
                Header = App.API.GetTranslation("iconTrayOpen") + " (" + _settings.Hotkey + ")",
                Icon = openIcon
            };
            var gamemodeIcon = new FontIcon { Glyph = "\ue7fc" };
            var gamemode = new MenuItem
            {
                Header = App.API.GetTranslation("GameMode"),
                Icon = gamemodeIcon
            };
            var positionresetIcon = new FontIcon { Glyph = "\ue73f" };
            var positionreset = new MenuItem
            {
                Header = App.API.GetTranslation("PositionReset"),
                Icon = positionresetIcon
            };
            var settingsIcon = new FontIcon { Glyph = "\ue713" };
            var settings = new MenuItem
            {
                Header = App.API.GetTranslation("iconTraySettings"),
                Icon = settingsIcon
            };
            var exitIcon = new FontIcon { Glyph = "\ue7e8" };
            var exit = new MenuItem
            {
                Header = App.API.GetTranslation("iconTrayExit"),
                Icon = exitIcon
            };

            open.Click += (o, e) => _viewModel.ToggleFlowLauncher();
            gamemode.Click += (o, e) => _viewModel.ToggleGameMode();
            positionreset.Click += (o, e) => _ = PositionResetAsync();
            settings.Click += (o, e) => App.API.OpenSettingDialog();
            exit.Click += (o, e) => Close();

            gamemode.ToolTip = App.API.GetTranslation("GameModeToolTip");
            positionreset.ToolTip = App.API.GetTranslation("PositionResetToolTip");

            _contextMenu.Items.Add(open);
            _contextMenu.Items.Add(gamemode);
            _contextMenu.Items.Add(positionreset);
            _contextMenu.Items.Add(settings);
            _contextMenu.Items.Add(exit);
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

        private Screen SelectedScreen()
        {
            Screen screen;
            switch (_settings.SearchWindowScreen)
            {
                case SearchWindowScreens.Cursor:
                    screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
                    break;
                case SearchWindowScreens.Primary:
                    screen = Screen.PrimaryScreen;
                    break;
                case SearchWindowScreens.Focus:
                    var foregroundWindowHandle = Win32Helper.GetForegroundWindow();
                    screen = Screen.FromHandle(foregroundWindowHandle);
                    break;
                case SearchWindowScreens.Custom:
                    if (_settings.CustomScreenNumber <= Screen.AllScreens.Length)
                        screen = Screen.AllScreens[_settings.CustomScreenNumber - 1];
                    else
                        screen = Screen.AllScreens[0];
                    break;
                default:
                    screen = Screen.AllScreens[0];
                    break;
            }

            return screen ?? Screen.AllScreens[0];
        }

        private double HorizonCenter(Screen screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip2.X - ActualWidth) / 2 + dip1.X;
            return left;
        }

        private double VerticalCenter(Screen screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var dip2 = Win32Helper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            var top = (dip2.Y - QueryTextBox.ActualHeight) / 4 + dip1.Y;
            return top;
        }

        private double HorizonRight(Screen screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip1.X + dip2.X - ActualWidth) - 10;
            return left;
        }

        private double HorizonLeft(Screen screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var left = dip1.X + 10;
            return left;
        }

        public double VerticalTop(Screen screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var top = dip1.Y + 10;
            return top;
        }

        #endregion

        #region Window Animation

        private void InitProgressbarAnimation()
        {
            var progressBarStoryBoard = new Storyboard();

            var da = new DoubleAnimation(ProgressBar.X2, ActualWidth + 100,
                new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            var da1 = new DoubleAnimation(ProgressBar.X1, ActualWidth + 0,
                new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            Storyboard.SetTargetProperty(da, new PropertyPath("(Line.X2)"));
            Storyboard.SetTargetProperty(da1, new PropertyPath("(Line.X1)"));
            progressBarStoryBoard.Children.Add(da);
            progressBarStoryBoard.Children.Add(da1);
            progressBarStoryBoard.RepeatBehavior = RepeatBehavior.Forever;

            da.Freeze();
            da1.Freeze();

            const string progressBarAnimationName = "ProgressBarAnimation";
            var beginStoryboard = new BeginStoryboard
            {
                Name = progressBarAnimationName,
                Storyboard = progressBarStoryBoard
            };

            var stopStoryboard = new StopStoryboard()
            {
                BeginStoryboardName = progressBarAnimationName
            };

            var trigger = new Trigger
            {
                Property = VisibilityProperty,
                Value = Visibility.Visible
            };
            trigger.EnterActions.Add(beginStoryboard);
            trigger.ExitActions.Add(stopStoryboard);

            var progressStyle = new Style(typeof(Line))
            {
                BasedOn = FindResource("PendingLineStyle") as Style
            };
            progressStyle.RegisterName(progressBarAnimationName, beginStoryboard);
            progressStyle.Triggers.Add(trigger);

            ProgressBar.Style = progressStyle;

            _viewModel.ProgressBarVisibility = Visibility.Hidden;
        }
        #endregion

        #region QueryTextBox Event

        private void QueryTextBox_OnCopy(object sender, ExecutedRoutedEventArgs e)
        {
            var result = _viewModel.Results.SelectedItem?.Result;
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

        #region Search Delay

        private void QueryTextBox_TextChanged1(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            _viewModel.QueryText = textBox.Text;
            _viewModel.Query(_settings.SearchQueryResultsWithDelay);
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
                    _notifyIcon?.Dispose();
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
