using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.API;
using Flow.Launcher.Infrastructure.Helpers;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Plugins;
using Flow.Launcher.Infrastructure.Plugins.Interfaces;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Interop.Programs;
using Flow.Launcher.ViewModel;
using iNKORE.UI.WPF.Modern;

namespace Flow.Launcher
{
    public class PublicAPIInstance : Plugin.IPublicAPI
    {
        private static readonly string ClassName = nameof(PublicAPIInstance);

        private readonly Settings _settings;
        private readonly MainViewModel _mainVM;

        private readonly object _saveSettingsLock = new();

        public PublicAPIInstance(Settings settings, MainViewModel mainVM)
        {
            _settings = settings;
            _mainVM = mainVM;

            IPublicAPI.Instance = this;
        }

        #region Public API

        public void ChangeQuery(string query, bool requery = false)
        {
            _mainVM.ChangeQueryText(query, requery);
        }

        public void RestartApp() => RestartApp(false);
        public void RestartAppAsAdmin() => RestartApp(true);
        private void RestartApp(bool runAsAdmin)
        {
            _mainVM.Hide();
            App.RestartApp(runAsAdmin);
        }

        public void ShowMainWindow() => _mainVM.Show();

        public void FocusQueryTextBox() => _mainVM.FocusQueryTextBox();

        public void HideMainWindow() => _mainVM.Hide();

        public bool IsMainWindowVisible() => _mainVM.MainWindowVisibilityStatus;

        public event VisibilityChangedEventHandler VisibilityChanged
        {
            add => _mainVM.VisibilityChanged += value;
            remove => _mainVM.VisibilityChanged -= value;
        }

        public void SaveAppAllSettings()
        {
            lock (_saveSettingsLock)
            {
                _settings.Save();
                PluginManager.Save();
                _mainVM.Save();
            }
        }

        public Task ReloadAllPluginData() => PluginManager.ReloadDataAsync();

        public void ShowMsgError(string title, string subTitle = "") =>
            ShowMsg(title, subTitle, Constant.ErrorIcon, true);

        public void ShowMsgErrorWithButton(string title, string buttonText, Action buttonAction, string subTitle = "") =>
            ShowMsgWithButton(title, buttonText, buttonAction, subTitle, Constant.ErrorIcon, true);

        public void ShowMsg(string title, string subTitle = "", string iconPath = "") =>
            ShowMsg(title, subTitle, iconPath, true);

        public void ShowMsg(string title, string subTitle, string iconPath, bool useMainWindowAsOwner = true)
        {
            Notification.Show(title, subTitle, iconPath);
        }

        public void ShowMsgWithButton(string title, string buttonText, Action buttonAction, string subTitle = "", string iconPath = "") =>
            ShowMsgWithButton(title, buttonText, buttonAction, subTitle, iconPath, true);

        public void ShowMsgWithButton(string title, string buttonText, Action buttonAction, string subTitle, string iconPath, bool useMainWindowAsOwner = true)
        {
            Notification.ShowWithButton(title, buttonText, buttonAction, subTitle, iconPath);
        }

        public void OpenSettingDialog()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SettingWindow sw = SingletonWindowOpener.Open<SettingWindow>();
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
                            $"{Localize.copy()} {(isFile ? Localize.fileTitle() : Localize.folderTitle())}",
                            Localize.completedSuccessfully());
                    }
                }
                else
                {
                    LogException(nameof(PublicAPIInstance), "Failed to copy file/folder to clipboard", exception);
                    ShowMsgError(Localize.failedToCopy());
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
                            $"{Localize.copy()} {Localize.textTitle()}",
                            Localize.completedSuccessfully());
                    }
                }
                else
                {
                    LogException(nameof(PublicAPIInstance), "Failed to copy text to clipboard", exception);
                    ShowMsgError(Localize.failedToCopy());
                }
            }
        }

        private static async Task<Exception> RetryActionOnSTAThreadAsync(Action action, int retryCount = 6, int retryDelay = 150)
        {
            for (var i = 0; i < retryCount; i++)
            {
                try
                {
                    await Win32Helper.StartSTATaskAsync(action).ConfigureAwait(false);
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

        public string GetTranslation(string key) => Internationalization.GetTranslation(key);

        public List<PluginMetadata> GetAllPlugins() => PluginManager.GetAllLoadedPlugins();

        public List<PluginMetadata> GetAllInitializedPlugins(bool includeFailed) =>
            PluginManager.GetAllInitializedPlugins(includeFailed);

        public MatchResult FuzzySearch(string query, string stringToCompare) =>
            StringMatcher.FuzzySearch(query, stringToCompare);

        public void AddActionKeyword(string pluginId, string newActionKeyword) =>
            PluginManager.AddActionKeyword(pluginId, newActionKeyword);

        public bool ActionKeywordAssigned(string actionKeyword) => PluginManager.ActionKeywordRegistered(actionKeyword);

        public void RemoveActionKeyword(string pluginId, string oldActionKeyword) =>
            PluginManager.RemoveActionKeyword(pluginId, oldActionKeyword);

        [Obsolete("TODO: Remove me, use new logging system directly")]
        public void LogDebug(string className, string message, [CallerMemberName] string methodName = "") { }

        [Obsolete("TODO: Remove me, use new logging system directly")]
        public void LogInfo(string className, string message, [CallerMemberName] string methodName = "") { }

        [Obsolete("TODO: Remove me, use new logging system directly")]
        public void LogWarn(string className, string message, [CallerMemberName] string methodName = "") { }

        [Obsolete("TODO: Remove me, use new logging system directly")]
        public void LogError(string className, string message, [CallerMemberName] string methodName = "") { }

        [Obsolete("TODO: Remove me, use new logging system directly")]
        public void LogException(string className, string message, Exception e, [CallerMemberName] string methodName = "") { }

        private readonly ConcurrentDictionary<Type, ISavable> _pluginJsonStorages = new();

        public void SavePluginSettings()
        {
            foreach (var savable in _pluginJsonStorages.Values)
            {
                savable.Save();
            }
        }

        public T LoadSettingJsonStorage<T>() where T : new()
        {
            var type = typeof(T);
            if (!_pluginJsonStorages.ContainsKey(type))
                _pluginJsonStorages[type] = new PluginJsonStorage<T>();

            return ((PluginJsonStorage<T>)_pluginJsonStorages[type]).Load();
        }

        public void SaveSettingJsonStorage<T>() where T : new()
        {
            var type = typeof(T);
            if (!_pluginJsonStorages.ContainsKey(type))
                _pluginJsonStorages[type] = new PluginJsonStorage<T>();

            ((PluginJsonStorage<T>)_pluginJsonStorages[type]).Save();
        }

        public void OpenDirectory(string directoryPath, string? fileNameOrFilePath = null)
        {
            try
            {
                var targetPath = fileNameOrFilePath is null
                    ? directoryPath
                    : Path.IsPathRooted(fileNameOrFilePath)
                        ? fileNameOrFilePath
                        : Path.Combine(directoryPath, fileNameOrFilePath);

                // Windows File Manager
                if (fileNameOrFilePath is null)
                {
                    // Only Open the directory
                    using var explorer = new Process();
                    explorer.StartInfo = new ProcessStartInfo
                    {
                        FileName = directoryPath,
                        UseShellExecute = true
                    };
                    explorer.Start();
                }
                else
                {
                    // Open the directory and select the file
                    FileExplorerHelper.OpenFolderAndSelectFile(targetPath);
                }
            }
            catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80004004))
            {
                /*
                 * The COMException with HResult 0x80004004 is E_ABORT (operation aborted).
                 * Shell APIs often return this when the operation is canceled or the shell cannot complete it cleanly.
                 * It most likely comes from FileExplorerHelper.OpenFolderAndSelectFile(targetPath).
                 * Typical triggers:
                 * The target file/folder was deleted/moved between computing targetPath and the shell call.
                 * The folder is on an offline network/removable drive.
                 * Explorer is restarting/busy and aborts the request.
                 * A selection request to a new/closing Explorer window is canceled.
                 * Because it is commonly user- or environment-driven and not actionable,
                 * we should treat it as expected noise and ignore it to avoid bothering users.
                 */
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
            {
                LogException(ClassName, "File Manager not found", ex);
                ShowMsgError(
                    Localize.fileManagerNotFoundTitle(),
                    Localize.fileManagerNotFound()
                );
            }
            catch (Exception ex)
            {
                LogException(ClassName, "Failed to open folder", ex);
                ShowMsgError(
                    Localize.errorTitle(),
                    Localize.folderOpenError()
                );
            }
        }

        private void OpenUri(Uri uri, bool inPrivate = false, bool forceBrowser = false, bool openInTab = true)
        {
            if (uri.IsFile
                && !File.Exists(uri.LocalPath)
                && !Directory.Exists(uri.LocalPath))
            {
                ShowMsgError(Localize.errorTitle(), Localize.fileNotFoundError(uri.LocalPath));
                return;
            }

            if (forceBrowser || uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            {
                try
                {
                    if (openInTab)
                        BrowserHelper.OpenInNewTab(uri);
                    else
                        BrowserHelper.OpenInNewWindow(uri);
                }
                catch (Exception e)
                {
                    var tabOrWindow = openInTab ? "tab" : "window";
                    LogException(ClassName, $"Failed to open URL in browser {tabOrWindow}: {inPrivate}", e);
                    ShowMsgError(
                        Localize.errorTitle(),
                        Localize.browserOpenError()
                    );
                }
            }
            else
            {
                try
                {
                    ProcessHelper.StartProcess(uri.AbsoluteUri, useShellExecute: true);
                }
                catch (Exception e)
                {
                    LogException(ClassName, $"Failed to open: {uri.AbsoluteUri}", e);
                    ShowMsgError(Localize.errorTitle(), e.Message);
                }
            }
        }

        public void OpenWebUrl(Uri url, bool inPrivate = false, bool inTab = true)
        {
            OpenUri(url, inPrivate, forceBrowser: true, openInTab: inTab);
        }

        public void OpenUrl(Uri url, bool inPrivate = false, bool inTab = true)
        {
            OpenUri(url, inPrivate, openInTab: inTab);
        }

        public void OpenAppUri(Uri appUri)
        {
            OpenUri(appUri);
        }

        public void ToggleGameMode()
        {
            _mainVM.ToggleGameMode();
        }

        public void SetGameMode(bool value)
        {
            _mainVM.GameModeStatus = value;
        }

        public bool IsGameModeOn()
        {
            return _mainVM.GameModeStatus;
        }

        public void ReQuery(bool reselect = true) => _mainVM.ReQuery(reselect);

        public void BackToQueryResults() => _mainVM.BackToQueryResults();

        public MessageBoxResult ShowMsgBox(string messageBoxText, string caption = "",
            MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.OK) =>
            MessageBoxEx.Show(messageBoxText, caption, button, icon, defaultResult);

        public Task ShowProgressBoxAsync(string caption, Func<Action<double>, Task> reportProgressAsync,
            Action? cancelProgress = null) => ProgressBoxEx.ShowAsync(caption, reportProgressAsync, cancelProgress);

        private readonly ConcurrentDictionary<(string, string, Type), ISavable> _pluginBinaryStorages = new();

        public void SavePluginCaches()
        {
            foreach (var savable in _pluginBinaryStorages.Values)
            {
                savable.Save();
            }
        }

        public async Task<T> LoadCacheBinaryStorageAsync<T>(string cacheName, string cacheDirectory, T defaultData) where T : new()
        {
            var type = typeof(T);
            if (!_pluginBinaryStorages.ContainsKey((cacheName, cacheDirectory, type)))
                _pluginBinaryStorages[(cacheName, cacheDirectory, type)] = new PluginBinaryStorage<T>(cacheName, cacheDirectory);

            return await ((PluginBinaryStorage<T>)_pluginBinaryStorages[(cacheName, cacheDirectory, type)]).TryLoadAsync(defaultData);
        }

        public async Task SaveCacheBinaryStorageAsync<T>(string cacheName, string cacheDirectory) where T : new()
        {
            var type = typeof(T);
            if (!_pluginBinaryStorages.ContainsKey((cacheName, cacheDirectory, type)))
                _pluginBinaryStorages[(cacheName, cacheDirectory, type)] = new PluginBinaryStorage<T>(cacheName, cacheDirectory);

            await ((PluginBinaryStorage<T>)_pluginBinaryStorages[(cacheName, cacheDirectory, type)]).SaveAsync();
        }

        public ValueTask<ImageSource> LoadImageAsync(string path, bool loadFullImage = false, bool cacheImage = true) =>
            ImageLoader.LoadAsync(path, loadFullImage, cacheImage);

        public bool IsApplicationDarkTheme()
        {
            return ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark;
        }

        public event ActualApplicationThemeChangedEventHandler ActualApplicationThemeChanged
        {
            add => _mainVM.ActualApplicationThemeChanged += value;
            remove => _mainVM.ActualApplicationThemeChanged -= value;
        }

        public string GetDataDirectory() => DataLocation.DataDirectory;
        #endregion
    }
}
