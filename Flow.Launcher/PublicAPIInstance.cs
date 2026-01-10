using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Helpers;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Plugins;
using Flow.Launcher.Infrastructure.Plugins.Interfaces;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
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
            GlobalHotkey.hookedKeyboardCallback = KListener_hookedKeyboardCallback;

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

        public void ShellRun(string cmd, string filename = "cmd.exe")
        {
            var args = filename == "cmd.exe" ? $"/C {cmd}" : $"{cmd}";

            StartProcess(filename, arguments: args, createNoWindow: true);
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

        public void LogDebug(string className, string message, [CallerMemberName] string methodName = "") =>
            Log.Debug(className, message, methodName);

        public void LogInfo(string className, string message, [CallerMemberName] string methodName = "") =>
            Log.Info(className, message, methodName);

        public void LogWarn(string className, string message, [CallerMemberName] string methodName = "") =>
            Log.Warn(className, message, methodName);

        public void LogError(string className, string message, [CallerMemberName] string methodName = "") =>
            Log.Error(className, message, methodName);

        public void LogException(string className, string message, Exception e, [CallerMemberName] string methodName = "") =>
            Log.Exception(className, message, e, methodName);

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

        public void OpenDirectory(string directoryPath, string fileNameOrFilePath = null)
        {
            try
            {
                var explorerInfo = _settings.CustomExplorer;
                var explorerPath = explorerInfo.Path.Trim().ToLowerInvariant();
                var targetPath = fileNameOrFilePath is null
                    ? directoryPath
                    : Path.IsPathRooted(fileNameOrFilePath)
                        ? fileNameOrFilePath
                        : Path.Combine(directoryPath, fileNameOrFilePath);

                if (Path.GetFileNameWithoutExtension(explorerPath) == "explorer")
                {
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
                        Win32Helper.OpenFolderAndSelectFile(targetPath);
                    }
                }
                else
                {
                    // Custom File Manager
                    using var explorer = new Process();
                    explorer.StartInfo = new ProcessStartInfo
                    {
                        FileName = explorerInfo.Path.Replace("%d", directoryPath),
                        UseShellExecute = true,
                        Arguments = fileNameOrFilePath is null
                            ? explorerInfo.DirectoryArgument.Replace("%d", directoryPath)
                            : explorerInfo.FileArgument
                                .Replace("%d", directoryPath)
                                .Replace("%f", targetPath)
                    };
                    explorer.Start();
                }
            }
            catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80004004))
            {
                /*
                 * The COMException with HResult 0x80004004 is E_ABORT (operation aborted).
                 * Shell APIs often return this when the operation is canceled or the shell cannot complete it cleanly.
                 * It most likely comes from Win32Helper.OpenFolderAndSelectFile(targetPath).
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
            if (uri.IsFile && !FilesFolders.FileOrLocationExists(uri.LocalPath))
            {
                ShowMsgError(Localize.errorTitle(), Localize.fileNotFoundError(uri.LocalPath));
                return;
            }

            if (forceBrowser || uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            {
                try
                {
                    if (openInTab)
                    {
                        uri.AbsoluteUri.OpenInBrowserTab(string.Empty, inPrivate);
                    }
                    else
                    {
                        uri.AbsoluteUri.OpenInBrowserWindow(string.Empty, inPrivate);
                    }
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
                    StartProcess(uri.AbsoluteUri, arguments: string.Empty, useShellExecute: true);
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

        private readonly List<Func<int, int, SpecialKeyState, bool>> _globalKeyboardHandlers = new();

        public void RegisterGlobalKeyboardCallback(Func<int, int, SpecialKeyState, bool> callback) =>
            _globalKeyboardHandlers.Add(callback);

        public void RemoveGlobalKeyboardCallback(Func<int, int, SpecialKeyState, bool> callback) =>
            _globalKeyboardHandlers.Remove(callback);

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

        public string GetDataDirectory() => DataLocation.DataDirectory();

        public bool StartProcess(string fileName, string workingDirectory = "", string arguments = "", bool useShellExecute = false, string verb = "", bool createNoWindow = false)
        {
            try
            {
                workingDirectory = string.IsNullOrEmpty(workingDirectory) ? Environment.CurrentDirectory : workingDirectory;

                // Use command executer to run the process as desktop user if running as admin
                if (Win32Helper.IsAdministrator())
                {
                    var result = Win32Helper.RunAsDesktopUser(
                        Constant.CommandExecutablePath,
                        Environment.CurrentDirectory,
                        $"-StartProcess " +
                        $"-FileName {AddDoubleQuotes(fileName)} " +
                        $"-WorkingDirectory {AddDoubleQuotes(workingDirectory)} " +
                        $"-Arguments {AddDoubleQuotes(arguments)} " +
                        $"-UseShellExecute {useShellExecute} " +
                        $"-Verb {AddDoubleQuotes(verb)} " +
                        $"-CreateNoWindow {createNoWindow}",
                        false,
                        true, // Do not show the command window
                        out var errorInfo);
                    if (!string.IsNullOrEmpty(errorInfo))
                    {
                        LogError(ClassName, $"Failed to start process {fileName} with arguments {arguments} under {workingDirectory}: {errorInfo}");
                    }

                    return result;
                }

                var info = new ProcessStartInfo
                {
                    FileName = fileName,
                    WorkingDirectory = workingDirectory,
                    Arguments = arguments,
                    UseShellExecute = useShellExecute,
                    Verb = verb,
                    CreateNoWindow = createNoWindow
                };
                Process.Start(info)?.Dispose();
                return true;
            }
            catch (Exception e)
            {
                LogException(ClassName, $"Failed to start process {fileName} with arguments {arguments} under {workingDirectory}", e);
                return false;
            }
        }

        public bool StartProcess(string fileName, string workingDirectory = "", Collection<string> argumentList = null, bool useShellExecute = false, string verb = "", bool createNoWindow = false) =>
            StartProcess(fileName, workingDirectory, JoinArgumentList(argumentList), useShellExecute, verb, createNoWindow);

        private static string AddDoubleQuotes(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return "\"\"";

            // If already wrapped in double quotes, return as is
            if (arg.Length >= 2 && arg[0] == '"' && arg[^1] == '"')
                return arg;

            return $"\"{arg}\"";
        }

        private static string JoinArgumentList(Collection<string> args)
        {
            if (args == null || args.Count == 0)
                return string.Empty;

            return string.Join(" ", args.Select(arg =>
            {
                if (string.IsNullOrEmpty(arg))
                    return "\"\"";

                // Add double quotes
                return AddDoubleQuotes(arg);
            }));
        }
        #endregion

        #region Private Methods

        private bool KListener_hookedKeyboardCallback(KeyEvent keyevent, int vkcode, SpecialKeyState state)
        {
            var continueHook = true;
            foreach (var x in _globalKeyboardHandlers)
            {
                continueHook &= x((int)keyevent, vkcode, state);
            }

            return continueHook;
        }

        #endregion
    }
}
