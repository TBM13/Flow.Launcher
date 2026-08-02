using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core;
using Flow.Launcher.Core.UserSettings;
using Flow.Launcher.Interop.Shell;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Logging;

namespace Flow.Launcher.Settings.Pages;

public partial class SettingsAboutViewModel(Logger<SettingsAboutViewModel> logger) : BaseSettingsPageViewModel
{
    private readonly Logger<SettingsAboutViewModel> _logger = logger;

    public override string Title => "About";
    public override string IconPath => "pack://application:,,,/Images/info.png";

#pragma warning disable CA1822 // Binding stops working if marked as static
    public string CacheFolderSize
#pragma warning restore CA1822
    {
        get
        {
            long size = GetCacheFiles().Sum(file => file.Length);
            return $"Clear Caches ({BytesToReadableString(size)})";
        }
    }

    public static string Version => Constant.Version;

    [RelayCommand]
    private void AskClearCacheFolderConfirmation()
    {
        MessageBoxResult res = MessageBox.Show(
            "Are you sure you want to delete all caches?",
            "Clear Caches",
            MessageBoxButton.YesNo
        );

        if (res == MessageBoxResult.Yes)
        {
            if (!ClearCacheFolder())
                MessageBox.Show(
                    "Failed to clear one or more files/folders. Check the log for more info");
        }
    }

    [RelayCommand]
    private static void OpenSettingsFolder()
    {
        FileExplorerHelper.OpenFolder(DataLocation.SettingsDirectory);
    }

    [RelayCommand]
    private static void OpenCacheFolder()
    {
        FileExplorerHelper.OpenFolder(DataLocation.CacheDirectory);
    }

    private bool ClearCacheFolder()
    {
        var success = true;

        // Clear cache files
        var cacheFiles = GetCacheFiles();
        foreach (var file in cacheFiles)
        {
            try
            {
                file.Delete();
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to delete cache file: {file.Name}");
                success = false;
            }
        }

        // Clear plugin cache contents (one by one to continue on error)
        var pluginCacheDir = new DirectoryInfo(DataLocation.PluginCacheDirectory);
        if (pluginCacheDir.Exists)
        {
            // Delete plugin subdirectories
            foreach (var dir in pluginCacheDir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    dir.Delete(recursive: true);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to delete cache directory: {dir.Name}");
                    success = false;
                }
            }

            // Delete loose files inside the plugin directory before deleting the directory itself
            foreach (var file in pluginCacheDir.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    file.Delete();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to delete cache file in plugin folder: {file.Name}");
                    success = false;
                }
            }

            // Delete the root plugin directory
            try
            {
                pluginCacheDir.Delete(recursive: false);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to delete main plugin cache directory: {pluginCacheDir.Name}");
                success = false;
            }
        }

        OnPropertyChanged(nameof(CacheFolderSize));
        return success;
    }

    private static List<FileInfo> GetCacheFiles()
    {
        return [.. new DirectoryInfo(DataLocation.CacheDirectory)
            .EnumerateFiles("*", SearchOption.AllDirectories)];
    }

    private static string BytesToReadableString(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB", "PB"];
        int index = 0;
        double size = bytes;

        while (Math.Abs(size) >= 1024 && index < suffixes.Length - 1)
        {
            size /= 1024;
            index++;
        }

        return $"{size:0.##} {suffixes[index]}";
    }
}
