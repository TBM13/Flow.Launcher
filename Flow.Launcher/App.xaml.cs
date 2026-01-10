using System;
using System.Diagnostics;
using System.Drawing.Imaging.Effects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;
using iNKORE.UI.WPF.Modern.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.Threading;

namespace Flow.Launcher
{
    public partial class App : IDisposable, ISingleInstanceApp
    {
        #region Public Properties

        public static IPublicAPI API { get; private set; }
        public static bool LoadingOrExiting => _mainWindow == null || _mainWindow.CanClose;

        #endregion

        #region Private Fields

        private static readonly string ClassName = nameof(App);

        private static bool _disposed;
        private static Settings _settings;
        private static MainWindow _mainWindow;
        private readonly MainViewModel _mainVM;
        private readonly Internationalization _internationalization;

        // To prevent two disposals running at the same time.
        private static readonly object _disposingLock = new();

        #endregion

        #region Constructor

        public App()
        {
            // Check if the application is running as administrator
            if (_settings.AlwaysRunAsAdministrator && !Win32Helper.IsAdministrator())
            {
                // We don't want to restart as admin if we are debugging in Visual Studio
                if (!Debugger.IsAttached)
                {
                    RestartApp(true);
                    return;
                }
            }

            // Do not use bitmap cache since it can cause WPF second window freezing issue
            ShadowAssist.UseBitmapCache = false;

            // Configure the dependency injection container
            try
            {
                var host = Host.CreateDefaultBuilder()
                    .UseContentRoot(AppContext.BaseDirectory)
                    .ConfigureServices(services => services
                        .AddSingleton(_ => _settings)
                        .AddSingleton<Internationalization>()
                        .AddSingleton<IPublicAPI, PublicAPIInstance>()
                        .AddSingleton<Plugin.IPublicAPI, PublicAPIInstance>()
                        .AddSingleton<Theme>()
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
                        // Use transient instance for dialog view models because
                        // settings will change and we need to recreate them
                        .AddTransient<SelectFileManagerViewModel>()
                    ).Build();
                Ioc.Default.ConfigureServices(host.Services);
            }
            catch (Exception e)
            {
                ShowErrorMsgBoxAndFailFast("Cannot configure dependency injection container, please open new issue in Flow.Launcher", e);
                return;
            }

            // Initialize the public API and Settings first
            try
            {
                API = Ioc.Default.GetRequiredService<IPublicAPI>();
                _settings.Initialize();
                _mainVM = Ioc.Default.GetRequiredService<MainViewModel>();
                _internationalization = Ioc.Default.GetRequiredService<Internationalization>();
            }
            catch (Exception e)
            {
                ShowErrorMsgBoxAndFailFast("Cannot initialize api and settings, please open new issue in Flow.Launcher", e);
                return;
            }
        }

        #endregion

        #region Restart

        /// <summary>
        /// Restart the application without changing the user privileges.
        /// </summary>
        /// <param name="forceAdmin">
        /// If true, the application will be restarted as administrator.
        /// If false, it will be restarted with the same privileges as the current user.
        /// </param>
        public static void RestartApp(bool forceAdmin = false)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Constant.ExecutablePath,
                Arguments = "--restart",
                UseShellExecute = true,
                Verb = Win32Helper.IsAdministrator() || forceAdmin ? "runas" : ""
            };
            // No need to de-elevate since we are restarting Flow Launcher which cannot bring security risks
            Process.Start(startInfo);
            Thread.Sleep(500);

            Current.Shutdown();
        }

        #endregion

        #region Main

        [STAThread]
        public static void Main()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && args[1] == "--restart")
            {
                // Wait until the previous instance closes
                SingleInstance<App>.WaitUntilWeAreFirstInstance();
            }

            // Initialize settings so that we can get language code
            try
            {
                var storage = new FlowLauncherJsonStorage<Settings>();
                _settings = storage.Load();
                _settings.SetStorage(storage);
            }
            catch (Exception e)
            {
                ShowErrorMsgBoxAndFailFast("Cannot load setting storage, please check local data directory", e);
                return;
            }

            // Start the application as a single instance
            if (SingleInstance<App>.InitializeAsFirstInstance())
            {
                using var application = new App();
                application.InitializeComponent();
                application.Run();
            }
        }

        #endregion

        #region Fail Fast

        private static void ShowErrorMsgBoxAndFailFast(string message, Exception e)
        {
            // Firstly show users the message
            MessageBox.Show(e.ToString(), message, MessageBoxButton.OK, MessageBoxImage.Error);

            // Flow cannot construct its App instance, so ensure Flow crashes w/ the exception info.
            Environment.FailFast(message, e);
        }

        #endregion

        #region App Events

#pragma warning disable VSTHRD100 // Avoid async void methods

        private async void OnStartup(object sender, StartupEventArgs e)
        {
            // Because new message box api uses MessageBoxEx window,
            // if it is created and closed before main window is created, it will cause the application to exit.
            // So set to OnExplicitShutdown to prevent the application from shutting down before main window is created
            Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Initialize notification system before any notification api is called
            Notification.Install();

            // Enable Win32 dark mode if the system is in dark mode before creating all windows
            Win32Helper.EnableWin32DarkMode(_settings.ColorScheme);

            // Initialize language before portable clean up since it needs translations
            await _internationalization.InitializeLanguageAsync();

            API.LogInfo(ClassName, "Begin Flow Launcher startup ----------------------------------------------------");
            API.LogInfo(ClassName, $"Runtime info:{ErrorReporting.RuntimeInfo()}");

            RegisterAppDomainExceptions();
            RegisterDispatcherUnhandledException();
            RegisterTaskSchedulerUnhandledException();

            await ImageLoader.InitializeAsync();

            _mainWindow = new MainWindow();

            Current.MainWindow = _mainWindow;
            Current.MainWindow.Title = Constant.FlowLauncher;

            // Initialize hotkey mapper instantly after main window is created because
            // it will steal focus from main window which causes window hide
            HotKeyMapper.Initialize();

            // Initialize theme for main window
            Ioc.Default.GetRequiredService<Theme>().ChangeTheme();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            RegisterExitEvents();

            API.SaveAppAllSettings();
            API.LogInfo(ClassName, "End Flow Launcher startup ------------------------------------------------------");

            API.LogInfo(ClassName, "Begin plugin initialization ----------------------------------------------------");
            PluginManager.LoadPlugins(_settings.PluginSettings);
            await PluginManager.InitializePluginsAsync();

            // Refresh home page after plugins are initialized because users may open main window during plugin initialization
            // And home page is created without full plugin list
            if (_settings.ShowHomePage && _mainVM.QueryResultsSelected() && string.IsNullOrEmpty(_mainVM.QueryText))
            {
                _mainVM.QueryResults();
            }

            // Save all settings since we possibly update the plugin environment paths
            API.SaveAppAllSettings();

            API.LogInfo(ClassName, "End plugin initialization ------------------------------------------------------");
        }

#pragma warning restore VSTHRD100 // Avoid async void methods

        #endregion

        #region Register Events

        private void RegisterExitEvents()
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                API.LogInfo(ClassName, "Process Exit");
                Dispose();
            };

            Current.Exit += (s, e) =>
            {
                API.LogInfo(ClassName, "Application Exit");
                Dispose();
            };

            Current.SessionEnding += (s, e) =>
            {
                API.LogInfo(ClassName, "Session Ending");
                Dispose();
            };
        }

        /// <summary>
        /// Let exception throw as normal is better for Debug
        /// </summary>
        [Conditional("RELEASE")]
        private void RegisterDispatcherUnhandledException()
        {
            DispatcherUnhandledException += ErrorReporting.DispatcherUnhandledException;
        }

        /// <summary>
        /// Let exception throw as normal is better for Debug
        /// </summary>
        [Conditional("RELEASE")]
        private static void RegisterAppDomainExceptions()
        {
            AppDomain.CurrentDomain.UnhandledException += ErrorReporting.UnhandledException;
        }

        /// <summary>
        /// Let exception throw as normal is better for Debug
        /// </summary>
        private static void RegisterTaskSchedulerUnhandledException()
        {
            TaskScheduler.UnobservedTaskException += ErrorReporting.TaskSchedulerUnobservedTaskException;
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            // Prevent two disposes at the same time.
            lock (_disposingLock)
            {
                if (!disposing)
                {
                    return;
                }

                if (_disposed)
                {
                    return;
                }

                // If we call Environment.Exit(0), the application dispose will be called before _mainWindow.Close()
                // Accessing _mainWindow?.Dispatcher will cause the application stuck
                // So here we need to check it and just return so that we will not acees _mainWindow?.Dispatcher
                if (!_mainWindow.CanClose)
                {
                    return;
                }

                _disposed = true;
            }

            API.LogInfo(ClassName, "Begin Flow Launcher dispose ----------------------------------------------------");
            if (disposing)
            {
                // Dispose needs to be called on the main Windows thread,
                // since some resources owned by the thread need to be disposed.
                _mainWindow?.Dispatcher.Invoke(_mainWindow.Dispose);
                _mainVM?.Dispose();
            }
            API.LogInfo(ClassName, "End Flow Launcher dispose ----------------------------------------------------");
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region ISingleInstanceApp

        public void OnSecondAppStarted()
        {
            API.ShowMainWindow();
        }

        #endregion
    }
}
