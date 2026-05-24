using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.API;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Interop;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;
using iNKORE.UI.WPF.Modern.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using ZLogger;

namespace Flow.Launcher;

public partial class App : Application
{
    [Obsolete("TODO: Remove this")]
    public static IPublicAPI API { get; private set; }
    public static bool LoadingOrExiting => _mainWindow is null || _mainWindow.CanClose;

    private static MainWindow _mainWindow;
    private IHost? _host;
    private readonly Settings _settings;
    private readonly PluginSDK.Logging.Logger<App>? _logger;

    public static readonly string RuntimeInfo = "\n\n" +
        $"Version: {Constant.Version}\n" +
        $"IntPtr Length: {IntPtr.Size}\n" +
        $"x64: {Environment.Is64BitOperatingSystem}";

    [STAThread]
    public static void Main()
    {
        bool isRestart = Environment.GetCommandLineArgs().Contains("--restart");
        if (!SingleInstance.Initialize(waitIfOccupied: isRestart))
        {
            MessageBox.Show("Another instance of Flow Launcher is already running.");
            return;
        }

        Settings settings;
        try
        {
            FlowLauncherJsonStorage<Settings> storage = new();
            settings = storage.Load();
            settings.SetStorage(storage);
        }
        catch (Exception e)
        {
            ShowErrorMsgboxAndFailFast("Failed to load settings", e);
            throw;
        }

        // Restart as admin if needed
        if (settings.AlwaysRunAsAdministrator && !Environment.IsPrivilegedProcess)
        {
            // Only restart when we are not debugging on Visual Studio
            if (!Debugger.IsAttached)
            {
                RestartApp(true);
                return;
            }
        }

        try
        {
            App application = new(settings);
            application.InitializeComponent();
            application.Run();
        }
        finally
        {
            SingleInstance.Cleanup();
        }
    }

    /// <summary>
    /// Restarts the application.
    /// </summary>
    /// <param name="forceAdmin"> If true, the app will be restarted as administrator.</param>
    public static void RestartApp(bool forceAdmin = false)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = Environment.ProcessPath,
            Arguments = "--restart",
            UseShellExecute = true,
            Verb = Environment.IsPrivilegedProcess || forceAdmin ? "runas" : ""
        };

        Process.Start(startInfo);
        // Application.Current is null when this is called from Main()
        Current?.Shutdown();
    }

    public App(Settings settings)
    {
        SetupErrorHandling();
        _settings = settings;

        // Do not use bitmap cache since it can cause WPF second window freezing issue
        ShadowAssist.UseBitmapCache = false;

        // Configure DI container
        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    // TODO: Log to file

                    if (Debugger.IsAttached)
                        logging.AddDebug();
#if DEBUG
                    logging.AddZLoggerConsole();
#endif
                })
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureServices(services => services
                    .AddTransient(typeof(PluginSDK.Logging.Logger<>))
                    .AddSingleton(_settings)
                    .AddSingleton<Internationalization>()
                    .AddSingleton<IPublicAPI, PublicAPIInstance>()
                    .AddSingleton<Plugin.IPublicAPI, PublicAPIInstance>()
                    .AddSingleton<Theme>()
                    .AddSingleton<HotkeyManager>()
                    .AddSingleton<ImageLoader>()
                    // Use one instance for main window view model because we only have one main window
                    .AddSingleton<MainViewModel>()
                    .AddSingleton<SettingWindowViewModel>()
                    // Use transient instance for setting window page view models because
                    // pages in setting window need to be recreated when setting window is closed
                    .AddTransient<SettingsPaneAboutViewModel>()
                    .AddTransient<SettingsPaneGeneralViewModel>()
                    .AddTransient<SettingsPaneHotkeyViewModel>()
                    .AddTransient<SettingsPanePluginsViewModel>()
                    .AddTransient<SettingsPaneThemeViewModel>()
                ).Build();

            Ioc.Default.ConfigureServices(_host.Services);
        }
        catch (Exception e)
        {
            ShowErrorMsgboxAndFailFast("Failed to configure dependency injection container", e);
            throw;
        }

        // Ensure logs are flushed even if OnExit is not called (e.g. Environment.Exit is used)
        AppDomain.CurrentDomain.ProcessExit += (s, ev) => CleanUpAndFlush();

        try
        {
            _logger = Ioc.Default.GetRequiredService<PluginSDK.Logging.Logger<App>>();

            // Initialize the API and Settings first
            // TODO: Check if this is needed
            API = Ioc.Default.GetRequiredService<IPublicAPI>();
            _settings.Initialize();
        }
        catch (Exception e)
        {
            ShowErrorMsgboxAndFailFast("Failed to initialize basic services", e);
            throw;
        }
    }

    private static void ShowErrorMsgboxAndFailFast(string message, Exception e)
    {
        MessageBox.Show(e.ToString(), message, MessageBoxButton.OK, MessageBoxImage.Error);
        Environment.FailFast(message, e);
    }

#pragma warning disable VSTHRD100
    // TODO: Double-check whether async here is a good idea
    protected override async void OnStartup(StartupEventArgs e)
#pragma warning restore VSTHRD100
    {
        base.OnStartup(e);

        // Because new message box api uses MessageBoxEx window,
        // if it is created and closed before main window is created, it will cause the application to exit.
        // So set to OnExplicitShutdown to prevent the application from shutting down before main window is created
        Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Initialize notification system before any notification api is called
        Notification.Install();

        // Enable Win32 dark mode if the system is in dark mode before creating all windows
        ApplicationHelper.SetWin32DarkMode(_settings.ColorScheme);

        // Initialize language before portable clean up since it needs translations
        await Ioc.Default.GetRequiredService<Internationalization>().InitializeLanguageAsync();

        _logger!.LogInfo($"Begin Flow Launcher startup ----------------------------------------------------");
        _logger.LogInfo($"Runtime info:{RuntimeInfo}");

        await Ioc.Default.GetRequiredService<ImageLoader>().InitializeAsync();

        _mainWindow = new MainWindow();
        Current.MainWindow = _mainWindow;
        Current.MainWindow.Title = Constant.FlowLauncher;

        // Initialize hotkey mapper instantly after main window is created because
        // it will steal focus from main window which causes window hide
        Ioc.Default.GetRequiredService<HotkeyManager>().Initialize();

        // Initialize theme for main window
        Ioc.Default.GetRequiredService<Theme>().ChangeTheme();

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        API.SaveAppAllSettings();
        _logger.LogInfo($"End Flow Launcher startup ------------------------------------------------------");

        _logger.LogInfo($"Begin plugin initialization ----------------------------------------------------");
        PluginManager.LoadPlugins(_settings.PluginSettings);
        await PluginManager.InitializePluginsAsync();

        // Refresh home page after plugins are initialized because users may open main window during plugin initialization
        // And home page is created without full plugin list
        MainViewModel mainVM = Ioc.Default.GetRequiredService<MainViewModel>(); ;
        if (_settings.ShowHomePage && mainVM.QueryResultsSelected() && string.IsNullOrEmpty(mainVM.QueryText))
        {
            mainVM.QueryResults();
        }

        // Save all settings since we possibly update the plugin environment paths
        API.SaveAppAllSettings();

        _logger.LogInfo($"End plugin initialization ------------------------------------------------------");
    }

    private void SetupErrorHandling()
    {
        void HandleException(Exception? ex, string message)
        {
            if (_logger is not null)
            {
                if (ex is not null)
                    _logger?.LogCritical(ex, $"{message}");
                else
                    _logger?.LogCritical($"{message}");
            }
            else
            {
                Trace.WriteLine($"{message}\n{ex}");
                Console.Error.WriteLine($"{message}\n{ex}");
            }

            Exception exceptionToReport = ex
                ?? new InvalidOperationException($"{message} (No exception provided)");

            if (Current?.Dispatcher?.CheckAccess() == true)
            {
                ReportWindow reportWindow = new(exceptionToReport);
                reportWindow.ShowDialog();
            }
            else
            {
                // We are on a background thread, show a simple MessageBox
                MessageBox.Show($"{message}\n\n{exceptionToReport}", Constant.FlowLauncher,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                HandleException(ex, "Unhandled exception");
            else
                HandleException(null, "Unhandled exception");

            // Ensure all logs are flushed
            CleanUpAndFlush();
        };

        DispatcherUnhandledException += (s, e) =>
        {
            // Prevent app from crashing immediately
            e.Handled = true;

            // Workaround for issue https://github.com/Flow-Launcher/Flow.Launcher/issues/4016
            // The crash occurs in PresentationFramework.dll, not necessarily when the Runner UI is visible, originating from this line:
            // https://github.com/dotnet/wpf/blob/3439f20fb8c685af6d9247e8fd2978cac42e74ac/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Shell/WindowChromeWorker.cs#L1005
            // Many bug reports because users see the "Error report UI" after the crash with System.Runtime.InteropServices.COMException 0xD0000701 or 0x80263001.
            // However, displaying this "Error report UI" during WPF crashes, especially when DWM composition is changing, is not ideal; some users reported it hangs for up to a minute before the it appears.
            // This change modifies the behavior to log the exception instead of showing the "Error report UI".
            if (ExceptionHelper.IsRecoverableDwmCompositionException(e.Exception))
            {
                _logger?.LogWarn(e.Exception, $"Ignoring DWM Composition exception");
                return;
            }

            HandleException(e.Exception, "WPF dispatcher unhandled exception");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            HandleException(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CleanUpAndFlush();
        base.OnExit(e);
    }

    private void CleanUpAndFlush()
    {
        // Ensure dispose is not called by multiple threads at the same time
        IHost? host = Interlocked.Exchange(ref _host, null);
        try
        {
            host?.Dispose();
        }
        catch (Exception e)
        {
            // We should not use the logger service here since it may have already been disposed
            string msg = $"Failed to dispose host: {e}";
            Trace.WriteLine(msg);
            Console.Error.WriteLine(msg);
        }
    }
}
