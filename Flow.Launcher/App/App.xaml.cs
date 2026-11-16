using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.AdminHelper.IPC;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Hotkeys;
using Flow.Launcher.Core.Image;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.Core.Text;
using Flow.Launcher.Interop;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.Settings;
using Flow.Launcher.Settings.Pages;
using Flow.Launcher.ViewModel;
using Flow.Launcher.Windows;
using iNKORE.UI.WPF.Modern.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
#if DEBUG
using ZLogger;
#endif

namespace Flow.Launcher.App;

public partial class App : Application
{
    private static Process? _adminHelperProcess;
    private IHost? _host;
    private readonly ISettingsAPI _settings;
    private readonly PluginSDK.Logging.Logger<App>? _logger;
    private readonly bool _initialized;

    public static readonly string RuntimeInfo = "\n\n" +
        $"Version: {Constant.Version}\n" +
        $"IntPtr Length: {IntPtr.Size}\n" +
        $"x64: {Environment.Is64BitOperatingSystem}";

    [STAThread]
    public static int Main()
    {
        if (Environment.IsPrivilegedProcess)
        {
            MessageBox.Show("Flow Launcher must run without administrator privileges.",
                Constant.FlowLauncher, MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }

        bool isRestart = Environment.GetCommandLineArgs().Contains("--restart");

        // Ensure only one instance of the app is running
        if (!SingleInstance.TryAcquire(
            @"Local\Flow.Launcher_UniqueAppMutex",
            out var instance, isRestart ? TimeSpan.FromMilliseconds(5000) : TimeSpan.Zero))
        {
            MessageBox.Show("Another instance of Flow Launcher is already running.");
            return 1;
        }

        try
        {
            // Setup early logger (the real one isn't available until after DI is configured)
            EarlyLoggerFactory earlyLoggerFactory = new();

            // Load settings
            ISettingsAPI settings;
            try
            {
                settings = SettingsFactory.LoadSettings(earlyLoggerFactory);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Failed to load settings:\n\n{e}",
                    Constant.FlowLauncher, MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }

            // Start the admin helper so we can connect to it
            try
            {
                StartAdminHelper();
            }
            catch (Exception e)
            {
                MessageBox.Show(
                    $"Flow Launcher could not start AdminHelper with administrator privileges." +
                    $" Global hotkeys require AdminHelper.\n\n{e.Message}",
                    Constant.FlowLauncher, MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }

            // Setup DI container
            IHost host;
            try
            {
                host = BuildHost(settings);
                // Handoff early logs to the real logger factory
                earlyLoggerFactory.FlushAndHandoff(Ioc.Default.GetRequiredService<ILoggerFactory>());
            }
            catch (Exception e)
            {
                MessageBox.Show($"Failed to build DI container:\n\n{e}",
                    Constant.FlowLauncher, MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }

            // Create & initialize the app
            App application;
            try
            {
                application = new(host, settings);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Failed to initialize application:\n\n{e}",
                    Constant.FlowLauncher, MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }

            application.InitializeComponent();
            return application.Run();
        }
        finally
        {
            StopAdminHelper();
            instance.Dispose();
        }
    }

    private static void StartAdminHelper()
    {
        string helperPath = AdminHelper.IPC.Const.AdminHelperExecutable;
        if (!File.Exists(helperPath))
            throw new FileNotFoundException("AdminHelper was not found next to Flow Launcher", helperPath);

        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string userSid = identity.User?.Value
            ?? throw new InvalidOperationException("Flow Launcher has no user SID");
        using Process currentProcess = Process.GetCurrentProcess();

        ProcessStartInfo startInfo = new()
        {
            FileName = helperPath,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add("--client-sid");
        startInfo.ArgumentList.Add(userSid);
        startInfo.ArgumentList.Add("--session-id");
        startInfo.ArgumentList.Add(currentProcess.SessionId.ToString());

        _adminHelperProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to launch AdminHelper");
    }

    private static void StopAdminHelper()
    {
        if (_adminHelperProcess is null)
            return;

        try
        {
            _adminHelperProcess.WaitForExit(3000);
        }
        catch { }
        finally
        {
            _adminHelperProcess.Dispose();
            _adminHelperProcess = null;
        }
    }

    /// <summary>
    /// Restarts the application.
    /// </summary>
    public static void RestartApp()
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = Environment.ProcessPath,
            Arguments = "--restart",
            UseShellExecute = true
        };

        Process.Start(startInfo);
        Current.Shutdown();
    }

    /// <summary>
    /// Creates and configures the DI container.
    /// </summary>
    private static IHost BuildHost(ISettingsAPI settings)
    {
        // Configure DI container
        IHost host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                // TODO: Log to file

                if (Debugger.IsAttached)
                {
                    logging.AddDebug();
#if DEBUG
                    // TODO: Use a log level setting instead
                    logging.SetMinimumLevel(LogLevel.Trace);
#endif
                }
#if DEBUG
                logging.AddZLoggerConsole();
#endif
            })
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices(services => services
                // Core services
                .AddTransient(typeof(PluginSDK.Logging.Logger<>))
                .AddSingleton<IPublicAPI, PublicAPIInstance>()
                .AddSingleton(settings)
                .AddSingleton<HotkeyManager>()
                .AddSingleton<IImageLoader, ImageLoader>()
                .AddSingleton<PluginManager>()
                .AddSingleton<Notification>()
                .AddSingleton<IStringMatcher, StringMatcher>()

                // Main Window
                .AddSingleton<MainWindow>()
                .AddSingleton<MainViewModel>()

                // Settings
                .AddTransient<SettingWindow>()
                .AddTransient<SettingViewModel>()
                .AddTransient<SettingsGeneralViewModel>()
                .AddTransient<SettingsThemeViewModel>()
                .AddTransient<SettingsPluginsViewModel>()
                .AddTransient<SettingsHotkeyViewModel>()
                .AddTransient<SettingsAboutViewModel>()
            ).Build();

        Ioc.Default.ConfigureServices(host.Services);
        return host;
    }

    public App(IHost host, ISettingsAPI settings)
    {
        _host = host;
        _settings = settings;
        _logger = Ioc.Default.GetRequiredService<PluginSDK.Logging.Logger<App>>();

        // Do not use bitmap cache since it can cause WPF second window freezing issue
        ShadowAssist.UseBitmapCache = false;

        // Initialize the API and pretty much everything
        // TODO: Check if this is needed
        Ioc.Default.GetRequiredService<IPublicAPI>();

        // Now that everything is initialized we can be more relaxed with exception handling
        SetupErrorHandling();

        _initialized = true;
    }

    // TODO: Double-check whether async here is a good idea
    protected override async void OnStartup(StartupEventArgs e)
    {
        // Ensure the constructor has completed successfully
        // since this method might still be called even if it threw an exception
        if (!_initialized)
            return;

        base.OnStartup(e);

        // Because new message box api uses MessageBoxEx window,
        // if it is created and closed before main window is created, it will cause the application to exit.
        // So set to OnExplicitShutdown to prevent the application from shutting down before main window is created
        Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Enable Win32 dark mode if the system is in dark mode before creating all windows
        ApplicationHelper.SetAppMode(_settings.ColorScheme switch
        {
            ColorScheme.System => AppMode.AllowDark,
            ColorScheme.Light => AppMode.ForceLight,
            ColorScheme.Dark => AppMode.ForceDark,
            _ => throw new InvalidOperationException($"Unknown color scheme: {_settings.ColorScheme}")
        });

        _logger!.LogInfo($"Begin Flow Launcher startup ----------------------------------------------------");
        _logger.LogInfo($"Runtime info:{RuntimeInfo}");

        // Initialize MainWindow and HotkeyManager
        Ioc.Default.GetRequiredService<MainWindow>();
        Ioc.Default.GetRequiredService<HotkeyManager>();

        // Ensure we support reading files with old encodings such as Windows-1252
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        IPublicAPI.Instance.SaveAppAllSettings();
        _logger.LogInfo($"End Flow Launcher startup ------------------------------------------------------");

        _logger.LogInfo($"Begin plugin initialization ----------------------------------------------------");
        PluginManager pluginManager = Ioc.Default.GetRequiredService<PluginManager>();
        pluginManager.LoadPlugins(_settings.PluginSettings);
        await pluginManager.InitializePluginsAsync();

        // Refresh home page after plugins are initialized because users may open main window during plugin initialization
        // And home page is created without full plugin list
        MainViewModel mainVM = Ioc.Default.GetRequiredService<MainViewModel>();
        if (_settings.ShowHomePage && !mainVM.ContextMenuSelected && string.IsNullOrEmpty(mainVM.QueryText))
        {
            mainVM.QueryResults();
        }

        // Save all settings since we possibly update the plugin environment paths
        IPublicAPI.Instance.SaveAppAllSettings();

        _logger.LogInfo($"End plugin initialization ------------------------------------------------------");
    }

    private void SetupErrorHandling()
    {
        void HandleException(Exception? ex, string message)
        {
            if (_logger is not null)
            {
                if (ex is not null)
                    _logger.LogCritical(ex, $"{message}");
                else
                    _logger.LogCritical($"{message}");
            }
            else
            {
                Trace.WriteLine($"{message}\n{ex}");
                Console.Error.WriteLine($"{message}\n{ex}");
            }

            Exception exceptionToReport = ex ?? new($"{message} (No exception provided)");
            MessageBox.Show($"{message}\n\n{exceptionToReport}", Constant.FlowLauncher,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                HandleException(ex, "Unhandled exception");
            else
                HandleException(null, "Unhandled exception");

            CleanUpAndFlushAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        };

        DispatcherUnhandledException += (s, e) =>
        {
            // Prevent app from crashing immediately
            e.Handled = true;

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
        _logger?.LogInfo($"Exiting -------------------------------------------------------");
        CleanUpAndFlushAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        base.OnExit(e);
    }

    private async Task CleanUpAndFlushAsync()
    {
        // Ensure dispose is not called by multiple threads at the same time
        IHost? host = Interlocked.Exchange(ref _host, null);
        if (host is null)
            return;

        try
        {
            await host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            // We should not use the logger service here since it may have already been disposed
            string msg = $"Failed to stop host: {e}";
            Trace.WriteLine(msg);
            Console.Error.WriteLine(msg);
        }

        try
        {
            if (host is IAsyncDisposable asyncHost)
                await asyncHost.DisposeAsync().ConfigureAwait(false);
            else
                host.Dispose();
        }
        catch (Exception e)
        {
            string msg = $"Failed to dispose host: {e}";
            Trace.WriteLine(msg);
            Console.Error.WriteLine(msg);
        }
    }
}
