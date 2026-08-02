using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Image;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.Core.Storage;
using Flow.Launcher.Core.Text;
using Flow.Launcher.Core.UserSettings;
using Flow.Launcher.Interop;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;
using Flow.Launcher.Settings;
using Flow.Launcher.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flow.Launcher;

public class PublicAPIInstance : IPublicAPI
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly PluginSDK.Logging.Logger<PublicAPIInstance> _logger;
    private readonly ISettingsAPI _settings;
    private readonly MainViewModel _mainVM;
    private readonly ImageLoader _imageLoader;
    private readonly PluginManager _pluginManager;
    private readonly Notification _notification;
    private readonly StringMatcher _stringMatcher;
    private Window? _settingWindow;
    private readonly Lock _saveSettingsLock = new();

    public PublicAPIInstance(ILoggerFactory loggerFactory,
        MainViewModel mainVM, ISettingsAPI settings,
        ImageLoader imageLoader, PluginManager pluginManager, Notification notification,
        StringMatcher stringMatcher)
    {
        _loggerFactory = loggerFactory;
        _logger = new(loggerFactory);
        _mainVM = mainVM;
        _settings = settings;
        _imageLoader = imageLoader;
        _pluginManager = pluginManager;
        _notification = notification;
        _stringMatcher = stringMatcher;

        IPublicAPI.Instance = this;
    }

    public void ChangeQuery(string query, bool requery = false)
    {
        _mainVM.ChangeQueryText(query, requery);
    }

    public void RestartApp() => RestartApp(false);
    public void RestartAppAsAdmin() => RestartApp(true);
    private void RestartApp(bool runAsAdmin)
    {
        _mainVM.Hide();
        App.App.RestartApp(runAsAdmin);
    }

    public void ShowMainWindow() => _mainVM.Show();

    public void HideMainWindow() => _mainVM.Hide();

    public bool IsMainWindowVisible() => _mainVM.MainWindowVisibilityStatus;

    public void SaveAppAllSettings()
    {
        lock (_saveSettingsLock)
        {
            _settings.Save();
            _pluginManager.Save();
            _mainVM.TrySave();
        }
    }

    public Task ReloadAllPluginData() => _pluginManager.ReloadDataAsync();

    public void ShowMsgError(string title, string subTitle = "") =>
        ShowMsg(title, subTitle, Constant.ErrorIcon, true);

    public void ShowMsgErrorWithButton(string title, string buttonText, Action buttonAction, string subTitle = "") =>
        ShowMsgWithButton(title, buttonText, buttonAction, subTitle, Constant.ErrorIcon, true);

    public void ShowMsg(string title, string subTitle = "", string iconPath = "") =>
        ShowMsg(title, subTitle, iconPath, true);

    public void ShowMsg(string title, string subTitle, string iconPath, bool useMainWindowAsOwner = true)
    {
        _notification.Show(title, subTitle, iconPath);
    }

    public void ShowMsgWithButton(string title, string buttonText, Action buttonAction, string subTitle = "", string iconPath = "") =>
        ShowMsgWithButton(title, buttonText, buttonAction, subTitle, iconPath, true);

    public void ShowMsgWithButton(string title, string buttonText, Action buttonAction, string subTitle, string iconPath, bool useMainWindowAsOwner = true)
    {
        _notification.ShowWithButton(title, buttonText, buttonAction, subTitle, iconPath);
    }

    public void OpenSettingDialog()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_settingWindow is null)
            {
                IServiceScope scope = Ioc.Default.CreateScope();
                _settingWindow = scope.ServiceProvider.GetRequiredService<SettingWindow>();

                EventHandler closedHandler = null!;
                closedHandler = (s, e) =>
                {
                    _settingWindow.Closed -= closedHandler;
                    _settingWindow = null;
                    scope.Dispose();
                };

                _settingWindow.Closed += closedHandler;
                _settingWindow.Show();
                return;
            }

            if (_settingWindow.WindowState == WindowState.Minimized)
                _settingWindow.WindowState = _settings.SettingWindowMaximized ? WindowState.Maximized : WindowState.Normal;
            _settingWindow.Activate();
            _settingWindow.Focus();
        });
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
    public async void CopyToClipboard(string stringToCopy, bool directCopy = false, bool showDefaultNotification = true)
    {
        if (string.IsNullOrEmpty(stringToCopy))
        {
            return;
        }

        var isFile = File.Exists(stringToCopy);
        if (directCopy && (isFile || Directory.Exists(stringToCopy)))
        {
            // Sometimes the clipboard is locked and cannot be accessed,
            // we need to retry a few times before giving up
            var exception = await RetryActionOnSTAThreadAsync(() =>
            {
                var paths = new StringCollection
                {
                    stringToCopy
                };

                Clipboard.SetFileDropList(paths);
            });

            if (exception == null)
            {
                if (showDefaultNotification)
                {
                    ShowMsg(
                        $"Copy {(isFile ? "File" : "Folder")}",
                        "Completed successfully");
                }
            }
            else
            {
                _logger.LogError(exception, $"Failed to copy file/folder to clipboard");
                ShowMsgError("Failed to copy");
            }
        }
        else
        {
            // Sometimes the clipboard is locked and cannot be accessed,
            // we need to retry a few times before giving up
            var exception = await RetryActionOnSTAThreadAsync(() =>
            {
                // We should use SetText instead of SetDataObject to avoid the clipboard being locked by other applications
                Clipboard.SetText(stringToCopy);
            });

            if (exception == null)
            {
                if (showDefaultNotification)
                {
                    ShowMsg(
                        $"Copy Text",
                        "Completed successfully");
                }
            }
            else
            {
                _logger.LogError(exception, $"Failed to copy text to clipboard");
                ShowMsgError("Failed to copy");
            }
        }
    }

    private static async Task<Exception> RetryActionOnSTAThreadAsync(Action action, int retryCount = 6, int retryDelay = 150)
    {
        for (var i = 0; i < retryCount; i++)
        {
            try
            {
                await ApplicationHelper.StartSTATaskAsync(action).ConfigureAwait(false);
                break;
            }
            catch (Exception e)
            {
                if (i == retryCount - 1)
                {
                    return e;
                }
                await Task.Delay(retryDelay);
            }
        }
        return null;
    }

    public List<PluginMetadata> GetAllPlugins() => _pluginManager.GetAllLoadedPlugins();

    public List<PluginMetadata> GetAllInitializedPlugins(bool includeFailed) =>
        _pluginManager.GetAllInitializedPlugins(includeFailed);

    // TODO: Should StringMatcher be a service or should we make it static and pass the query precision config here?
    public MatchResult FuzzySearch(string query, string stringToCompare) =>
        _stringMatcher.FuzzyMatch(query, stringToCompare);

    public void AddActionKeyword(string pluginId, string newActionKeyword) =>
        _pluginManager.AddActionKeyword(pluginId, newActionKeyword);

    public bool ActionKeywordAssigned(string actionKeyword) => _pluginManager.ActionKeywordRegistered(actionKeyword);

    public void RemoveActionKeyword(string pluginId, string oldActionKeyword) =>
        _pluginManager.RemoveActionKeyword(pluginId, oldActionKeyword);

    private readonly ConcurrentDictionary<Type, ISavable> _pluginJsonStorages = new();

    public void SavePluginSettings()
    {
        foreach (var savable in _pluginJsonStorages.Values)
        {
            savable.TrySave();
        }
    }

    public T LoadSettingJsonStorage<T>() where T : class, new()
    {
        Type type = typeof(T);
        if (!_pluginJsonStorages.TryGetValue(type, out ISavable? value))
        {
            string assemblyName = type.Assembly.GetName().Name
                ?? throw new NullReferenceException("Plugin's assembly name was null");

            value = new JsonStorage<T>(
                _loggerFactory, Path.Combine(DataLocation.PluginSettingsDirectory, assemblyName, $"{type.Name}.json"));
            _pluginJsonStorages[type] = value;
        }

        return ((JsonStorage<T>)value).TryLoad();
    }

    public void SaveSettingJsonStorage<T>() where T : class, new()
    {
        Type type = typeof(T);
        if (!_pluginJsonStorages.TryGetValue(type, out ISavable? value))
        {
            string assemblyName = type.Assembly.GetName().Name
                ?? throw new NullReferenceException("Plugin's assembly name was null");

            value = new JsonStorage<T>(
                _loggerFactory, Path.Combine(DataLocation.PluginSettingsDirectory, assemblyName, $"{type.Name}.json"));
            _pluginJsonStorages[type] = value;
        }

        value.TrySave();
    }

    public void ReQuery(bool reselect = true) => _mainVM.ReQuery(reselect);

    public void BackToQueryResults() => _mainVM.BackToQueryResults();

    public MessageBoxResult ShowMsgBox(string messageBoxText, string caption = "",
        MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.OK) =>
        MessageBoxEx.Show(_imageLoader, messageBoxText, caption, button, icon, defaultResult);

    private readonly ConcurrentDictionary<(string, string, Type), ISavable> _pluginBinaryStorages = new();

    public void SavePluginCaches()
    {
        foreach (var savable in _pluginBinaryStorages.Values)
        {
            savable.TrySave();
        }
    }

    public async Task<T> LoadCacheBinaryStorageAsync<T>(string cacheName, string cacheDirectory, T defaultData) where T : class, new()
    {
        Type type = typeof(T);
        if (!_pluginBinaryStorages.TryGetValue((cacheName, cacheDirectory, type), out ISavable? value))
        {
            value = new BinaryStorage<T>(
                _loggerFactory, Path.Combine(DataLocation.PluginCacheDirectory, cacheDirectory, $"{cacheName}.cache"));
            _pluginBinaryStorages[(cacheName, cacheDirectory, type)] = value;
        }

        return await ((BinaryStorage<T>)value).TryLoadAsync();
    }

    public void SaveCacheBinaryStorage<T>(string cacheName, string cacheDirectory) where T : class, new()
    {
        Type type = typeof(T);
        if (!_pluginBinaryStorages.TryGetValue((cacheName, cacheDirectory, type), out ISavable? value))
        {
            value = new BinaryStorage<T>(
                _loggerFactory, Path.Combine(DataLocation.PluginCacheDirectory, cacheDirectory, $"{cacheName}.cache"));
            _pluginBinaryStorages[(cacheName, cacheDirectory, type)] = value;
        }

        value.TrySave();
    }

    public ValueTask<ImageSource> LoadImageAsync(string path, bool loadFullImage = false) =>
        _imageLoader.LoadAsync(path, loadFullImage);
}
