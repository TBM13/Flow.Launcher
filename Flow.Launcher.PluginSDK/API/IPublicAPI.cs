using System.Windows;
using System.Windows.Media;
using Flow.Launcher.PluginSDK.Plugins;

namespace Flow.Launcher.PluginSDK.API;

/// <summary>
/// Public APIs that plugin can use
/// </summary>
public interface IPublicAPI
{
#pragma warning disable CS8618
    // TODO: Check if we can remove this
    public static IPublicAPI Instance { get; internal set; }
#pragma warning restore CS8618

    /// <summary>
    /// Change Flow.Launcher query.
    /// When current results are from context menu or history, it will go back to query results before changing query.
    /// </summary>
    /// <param name="query">query text</param>
    /// <param name="requery">
    /// Force requery. By default, Flow Launcher will not fire query if your query is same with existing one.
    /// Set this to <see langword="true"/> to force Flow Launcher re-querying
    /// </param>
    void ChangeQuery(string query, bool requery = false);

    /// <summary>
    /// Restart Flow Launcher
    /// Restart Flow Launcher without changing the user privileges.
    /// </summary>
    void RestartApp();

    /// <summary>
    /// Restart Flow Launcher as administrator.
    /// </summary>
    void RestartAppAsAdmin();

    /// <summary>
    /// Copies the passed in text and shows a message indicating whether the operation was completed successfully.
    /// When directCopy is set to true and passed in text is the path to a file or directory,
    /// the actual file/directory will be copied to clipboard. Otherwise the text itself will still be copied to clipboard.
    /// </summary>
    /// <param name="text">Text to save on clipboard</param>
    /// <param name="directCopy">When true it will directly copy the file/folder from the path specified in text</param>
    /// <param name="showDefaultNotification">Whether to show the default notification from this method after copy is done.
    ///                                         It will show file/folder/text is copied successfully.
    ///                                         Turn this off to show your own notification after copy is done.</param>>
    public void CopyToClipboard(string text, bool directCopy = false, bool showDefaultNotification = true);

    /// <summary>
    /// Save everything, all of Flow Launcher and plugins' data and settings
    /// </summary>
    void SaveAppAllSettings();

    /// <summary>
    /// Save all Flow's plugins settings
    /// </summary>
    void SavePluginSettings();

    /// <summary>
    /// Reloads any Plugins that have the
    /// IReloadable implemented. It refeshes
    /// Plugin's in memory data with new content
    /// added by user.
    /// </summary>
    Task ReloadAllPluginData();

    /// <summary>
    /// Show the error message using Flow's standard error icon.
    /// </summary>
    /// <param name="title">Message title</param>
    /// <param name="subTitle">Optional message subtitle</param>
    void ShowMsgError(string title, string subTitle = "");

    /// <summary>
    /// Show the error message using Flow's standard error icon.
    /// </summary>
    /// <param name="title">Message title</param>
    /// <param name="buttonText">Message button content</param>
    /// <param name="buttonAction">Message button action</param>
    /// <param name="subTitle">Optional message subtitle</param>
    void ShowMsgErrorWithButton(string title, string buttonText, Action buttonAction, string subTitle = "");

    /// <summary>
    /// Show the MainWindow when hiding
    /// </summary>
    void ShowMainWindow();

    /// <summary>
    /// Focus the query text box in the main window
    /// </summary>
    void FocusQueryTextBox();

    /// <summary>
    /// Hide MainWindow
    /// </summary>
    void HideMainWindow();

    /// <summary>
    /// Representing whether the main window is visible
    /// </summary>
    /// <returns></returns>
    bool IsMainWindowVisible();

    /// <summary>
    /// Invoked when the visibility of the main window has changed. Currently, the plugin will continue to be subscribed even if it is turned off.
    /// </summary>
    event VisibilityChangedEventHandler VisibilityChanged;

    /// <summary>
    /// Show message box
    /// </summary>
    /// <param name="title">Message title</param>
    /// <param name="subTitle">Message subtitle</param>
    /// <param name="iconPath">Message icon path (relative path to your plugin folder)</param>
    void ShowMsg(string title, string subTitle = "", string iconPath = "");

    /// <summary>
    /// Show message box
    /// </summary>
    /// <param name="title">Message title</param>
    /// <param name="subTitle">Message subtitle</param>
    /// <param name="iconPath">Message icon path (relative path to your plugin folder)</param>
    /// <param name="useMainWindowAsOwner">when true will use main windows as the owner</param>
    void ShowMsg(string title, string subTitle, string iconPath, bool useMainWindowAsOwner = true);

    /// <summary>
    /// Show message box with button
    /// </summary>
    /// <param name="title">Message title</param>
    /// <param name="buttonText">Message button content</param>
    /// <param name="buttonAction">Message button action</param>
    /// <param name="subTitle">Message subtitle</param>
    /// <param name="iconPath">Message icon path (relative path to your plugin folder)</param>
    void ShowMsgWithButton(string title, string buttonText, Action buttonAction, string subTitle = "", string iconPath = "");

    /// <summary>
    /// Show message box with button
    /// </summary>
    /// <param name="title">Message title</param>
    /// <param name="buttonText">Message button content</param>
    /// <param name="buttonAction">Message button action</param>
    /// <param name="subTitle">Message subtitle</param>
    /// <param name="iconPath">Message icon path (relative path to your plugin folder)</param>
    /// <param name="useMainWindowAsOwner">when true will use main windows as the owner</param>
    void ShowMsgWithButton(string title, string buttonText, Action buttonAction, string subTitle, string iconPath, bool useMainWindowAsOwner = true);

    /// <summary>
    /// Open setting dialog
    /// </summary>
    void OpenSettingDialog();

    /// <summary>
    /// Get all loaded plugins
    /// </summary>
    /// <remarks>
    /// Will also return any plugins not fully initialized yet
    /// </remarks>
    /// <returns></returns>
    List<PluginMetadata> GetAllPlugins();

    /// <summary>
    /// Get all initialized plugins
    /// </summary>
    /// <param name="includeFailed">
    /// Whether to include plugins that failed to initialize
    /// </param>
    /// <returns></returns>
    List<PluginMetadata> GetAllInitializedPlugins(bool includeFailed);

    /// <summary>
    /// Fuzzy Search the string with the given query. This is the core search mechanism Flow uses
    /// </summary>
    /// <param name="query">Query string</param>
    /// <param name="stringToCompare">The string that will be compared against the query</param>
    /// <returns>Match results</returns>
    MatchResult FuzzySearch(string query, string stringToCompare);

    /// <summary>
    /// Add ActionKeyword and update action keyword metadata for specific plugin.
    /// Before adding, please check if action keyword is already assigned by <see cref="ActionKeywordAssigned"/>
    /// </summary>
    /// <param name="pluginId">ID for plugin that needs to add action keyword</param>
    /// <param name="newActionKeyword">The actionkeyword that is supposed to be added</param>
    /// <remarks>
    /// If new action keyword contains any whitespace, FL will still add it but it will not work for users.
    /// So plugin should check the whitespace before calling this function.
    /// </remarks>
    void AddActionKeyword(string pluginId, string newActionKeyword);

    /// <summary>
    /// Remove ActionKeyword and update action keyword metadata for specific plugin
    /// </summary>
    /// <param name="pluginId">ID for plugin that needs to remove action keyword</param>
    /// <param name="oldActionKeyword">The actionkeyword that is supposed to be removed</param>
    void RemoveActionKeyword(string pluginId, string oldActionKeyword);

    /// <summary>
    /// Check whether specific ActionKeyword is assigned to any of the plugin
    /// </summary>
    /// <param name="actionKeyword">The actionkeyword for checking</param>
    /// <returns>True if the actionkeyword is already assigned, False otherwise</returns>
    bool ActionKeywordAssigned(string actionKeyword);

    /// <summary>
    /// Load JsonStorage for current plugin's setting. This is the method used to load settings from json in Flow.
    /// When the file is not exist, it will create a new instance for the specific type.
    /// </summary>
    /// <typeparam name="T">Type for deserialization</typeparam>
    /// <returns></returns>
    T LoadSettingJsonStorage<T>() where T : class, new();

    /// <summary>
    /// Save JsonStorage for current plugin's setting. This is the method used to save settings to json in Flow.
    /// This method will save the original instance loaded with LoadJsonStorage.
    /// This API call is for manually Save.
    /// Flow will automatically save all setting type that has called <see cref="LoadSettingJsonStorage"/> or <see cref="SaveSettingJsonStorage"/> previously.
    /// </summary>
    /// <typeparam name="T">Type for Serialization</typeparam>
    /// <returns></returns>
    void SaveSettingJsonStorage<T>() where T : class, new();

    /// <summary>
    /// Open directory in an explorer configured by user via Flow's Settings. The default is Windows Explorer
    /// </summary>
    /// <param name="DirectoryPath">Directory Path to open</param>
    /// <param name="FileNameOrFilePath">Extra FileName Info</param>
    public void OpenDirectory(string DirectoryPath, string? FileNameOrFilePath = null);

    /// <summary>
    /// Opens the URL using the browser with the given Uri object, even if the URL is a local file.
    /// </summary>
    public void OpenWebUrl(Uri url, bool inPrivate = false, bool inTab = true);

    /// <summary>
    /// Opens the URL with the given Uri object in browser if scheme is Http or Https.
    /// If the URL is a local file, it will instead be opened with the default application for that file type.
    /// </summary>
    public void OpenUrl(Uri url, bool inPrivate = false, bool inTab = true);

    /// <summary>
    /// Opens the application URI with the given Uri object, e.g. obsidian://search-query-example
    /// </summary>
    public void OpenAppUri(Uri appUri);

    /// <summary>
    /// Toggles Game Mode. off -> on and backwards
    /// </summary>
    public void ToggleGameMode();

    /// <summary>
    /// Switches Game Mode to given value
    /// </summary>
    /// <param name="value">New Game Mode status</param>
    public void SetGameMode(bool value);

    /// <summary>
    /// Representing Game Mode status
    /// </summary>
    /// <returns></returns>
    public bool IsGameModeOn();

    /// <summary>
    /// Reloads the query.
    /// When current results are from context menu or history, it will go back to query results before re-querying.
    /// </summary>
    /// <param name="reselect">Choose the first result after reload if true; keep the last selected result if false. Default is true.</param>
    public void ReQuery(bool reselect = true);

    /// <summary>
    /// Back to the query results.
    /// This method should run when selected item is from context menu or history.
    /// </summary>
    public void BackToQueryResults();

    /// <summary>
    /// Displays a standardised Flow message box.
    /// </summary>
    /// <param name="messageBoxText">The message of the message box.</param>
    /// <param name="caption">The caption of the message box.</param>
    /// <param name="button">Specifies which button or buttons to display.</param>
    /// <param name="icon">Specifies the icon to display.</param>
    /// <param name="defaultResult">Specifies the default result of the message box.</param>
    /// <returns>Specifies which message box button is clicked by the user.</returns>
    public MessageBoxResult ShowMsgBox(string messageBoxText, string caption = "", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.OK);

    /// <summary>
    /// Save all Flow's plugins caches
    /// </summary>
    void SavePluginCaches();

    /// <summary>
    /// Load BinaryStorage for current plugin's cache. This is the method used to load cache from binary in Flow.
    /// When the file is not exist, it will create a new instance for the specific type.
    /// </summary>
    /// <typeparam name="T">Type for deserialization</typeparam>
    /// <param name="cacheName">Cache file name</param>
    /// <param name="cacheDirectory">Cache directory from plugin metadata</param>
    /// <param name="defaultData">Default data to return</param>
    /// <returns></returns>
    /// <remarks>
    /// BinaryStorage utilizes MemoryPack, which means the object must be MemoryPackSerializable <see href="https://github.com/Cysharp/MemoryPack"/>
    /// </remarks>
    Task<T> LoadCacheBinaryStorageAsync<T>(string cacheName, string cacheDirectory, T defaultData) where T : class, new();

    /// <summary>
    /// Save BinaryStorage for current plugin's cache. This is the method used to save cache to binary in Flow.
    /// This method will save the original instance loaded with LoadCacheBinaryStorageAsync.
    /// This API call is for manually Save.
    /// Flow will automatically save all cache type that has called <see cref="LoadCacheBinaryStorageAsync"/> or <see cref="SaveCacheBinaryStorage"/> previously.
    /// </summary>
    /// <typeparam name="T">Type for Serialization</typeparam>
    /// <param name="cacheName">Cache file name</param>
    /// <param name="cacheDirectory">Cache directory from plugin metadata</param>
    /// <returns></returns>
    /// <remarks>
    /// BinaryStorage utilizes MemoryPack, which means the object must be MemoryPackSerializable <see href="https://github.com/Cysharp/MemoryPack"/>
    /// </remarks>
    void SaveCacheBinaryStorage<T>(string cacheName, string cacheDirectory) where T : class, new();

    /// <summary>
    /// Load image from path.
    /// Support local, remote and data:image url.
    /// Support png, jpg, jpeg, gif, bmp, tiff, ico, image files.
    /// If image path is missing, it will return a missing icon.
    /// </summary>
    /// <param name="path">The path of the image.</param>
    /// <param name="loadFullImage">
    /// Load full image or not.
    /// </param>
    /// <returns></returns>
    ValueTask<ImageSource> LoadImageAsync(string path, bool loadFullImage = false);

    /// <summary>
    /// Representing whether the application is using a dark theme
    /// </summary>
    /// <returns></returns>
    bool IsApplicationDarkTheme();

    /// <summary>
    /// Invoked when the actual theme of the application has changed. Currently, the plugin will continue to be subscribed even if it is turned off.
    /// </summary>
    event ActualApplicationThemeChangedEventHandler ActualApplicationThemeChanged;

    /// <summary>
    /// Get the user data directory of Flow Launcher.
    /// </summary>
    /// <returns></returns>
    string GetDataDirectory();
}
