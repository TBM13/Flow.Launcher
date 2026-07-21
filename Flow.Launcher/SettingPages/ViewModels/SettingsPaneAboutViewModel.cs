using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core;
using Flow.Launcher.Core.UserSettings;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Logging;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneAboutViewModel : ObservableObject
{
    // TODO: Check if there is any better alternative
    private readonly Logger<SettingsPaneAboutViewModel> _logger
        = Ioc.Default.GetRequiredService<Logger<SettingsPaneAboutViewModel>>();

    public string CacheFolderSize
    {
        get
        {
            var size = GetCacheFiles().Sum(file => file.Length);
            return $"Clear Caches ({BytesToReadableString(size)})";
        }
    }

    public string Version => Constant.Version switch
    {
        "1.0.0" => Constant.Dev,
        _ => Constant.Version
    };

    [RelayCommand]
    private void AskClearCacheFolderConfirmation()
    {
        var confirmResult = IPublicAPI.Instance.ShowMsgBox(
            "Are you sure you want to delete all caches?",
            "Clear Caches",
            MessageBoxButton.YesNo
        );

        if (confirmResult == MessageBoxResult.Yes)
        {
            if (!ClearCacheFolder())
            {
                IPublicAPI.Instance.ShowMsgBox("Failed to clear part of folders and files. Please see log file for more information");
            }
        }
    }

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        IPublicAPI.Instance.OpenDirectory(DataLocation.SettingsDirectory);
    }

    [RelayCommand]
    private void OpenCacheFolder()
    {
        IPublicAPI.Instance.OpenDirectory(DataLocation.CacheDirectory);
    }

    private bool ClearCacheFolder()
    {
        var success = true;
        var cacheDirectory = GetCacheDir();
        var pluginCacheDirectory = GetPluginCacheDir();
        var cacheFiles = GetCacheFiles();

        cacheFiles.ForEach(f =>
        {
            try
            {
                f.Delete();
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to delete cache file: {f.Name}");
                success = false;
            }
        });

        // Check if plugin cache directory exists before attempting to delete
        // Or it will throw DirectoryNotFoundException in `pluginCacheDirectory.EnumerateDirectories`
        if (pluginCacheDirectory.Exists)
        {
            // Firstly, delete plugin cache directories
            pluginCacheDirectory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                .ToList()
                .ForEach(dir =>
                {
                    try
                    {
                        // Plugin may create directories in its cache directory
                        dir.Delete(recursive: true);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, $"Failed to delete cache directory: {dir.Name}");
                        success = false;
                    }
                });

            // Then, delete plugin directory
            var dir = pluginCacheDirectory;
            try
            {
                dir.Delete(recursive: false);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to delete cache directory: {dir.Name}");
                success = false;
            }
        }

        // Raise regardless to cover scenario where size needs to be recalculated if the folder is manually removed on disk.
        OnPropertyChanged(nameof(CacheFolderSize));

        return success;
    }

    private static DirectoryInfo GetCacheDir()
    {
        return new DirectoryInfo(DataLocation.CacheDirectory);
    }

    private static DirectoryInfo GetPluginCacheDir()
    {
        return new DirectoryInfo(DataLocation.PluginCacheDirectory);
    }

    private static List<FileInfo> GetCacheFiles()
    {
        return GetCacheDir().EnumerateFiles("*", SearchOption.AllDirectories).ToList();
    }

    private static string BytesToReadableString(long bytes)
    {
        const int scale = 1024;
        string[] orders = { "GB", "MB", "KB", "B" };
        var max = (long)Math.Pow(scale, orders.Length - 1);

        foreach (var order in orders)
        {
            if (bytes > max) return $"{decimal.Divide(bytes, max):##.##} {order}";

            max /= scale;
        }

        return "0 B";
    }
}
