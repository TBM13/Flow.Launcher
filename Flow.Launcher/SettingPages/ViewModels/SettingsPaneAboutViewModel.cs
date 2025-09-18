using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneAboutViewModel : BaseModel
{
    private static readonly string ClassName = nameof(SettingsPaneAboutViewModel);

    private readonly Settings _settings;

    public string CacheFolderSize
    {
        get
        {
            var size = GetCacheFiles().Sum(file => file.Length);
            return $"{App.API.GetTranslation("clearcachefolder")} ({BytesToReadableString(size)})";
        }
    }

    public string Website => Constant.Website;
    public string SponsorPage => Constant.SponsorPage;
    public string Documentation => Constant.Documentation;
    public string Docs => Constant.Docs;
    public string Github => Constant.GitHub;

    public string Version => Constant.Version switch
    {
        "1.0.0" => Constant.Dev,
        _ => Constant.Version
    };

    public SettingsPaneAboutViewModel(Settings settings)
    {
        _settings = settings;
    }

    [RelayCommand]
    private void AskClearCacheFolderConfirmation()
    {
        var confirmResult = App.API.ShowMsgBox(
            App.API.GetTranslation("clearcachefolderMessage"),
            App.API.GetTranslation("clearcachefolder"),
            MessageBoxButton.YesNo
        );

        if (confirmResult == MessageBoxResult.Yes)
        {
            if (!ClearCacheFolder())
            {
                App.API.ShowMsgBox(App.API.GetTranslation("clearfolderfailMessage"));
            }
        }
    }

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        App.API.OpenDirectory(DataLocation.SettingsDirectory);
    }

    [RelayCommand]
    private void OpenParentOfSettingsFolder(object parameter)
    {
        string settingsFolderPath = Path.Combine(DataLocation.SettingsDirectory);
        string parentFolderPath = Path.GetDirectoryName(settingsFolderPath);
        App.API.OpenDirectory(parentFolderPath);
    }

    [RelayCommand]
    private void OpenCacheFolder()
    {
        App.API.OpenDirectory(DataLocation.CacheDirectory);
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
                App.API.LogException(ClassName, $"Failed to delete cache file: {f.Name}", e);
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
                        App.API.LogException(ClassName, $"Failed to delete cache directory: {dir.Name}", e);
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
                App.API.LogException(ClassName, $"Failed to delete cache directory: {dir.Name}", e);
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
        long max = (long)Math.Pow(scale, orders.Length - 1);

        foreach (string order in orders)
        {
            if (bytes > max) return $"{decimal.Divide(bytes, max):##.##} {order}";

            max /= scale;
        }

        return "0 B";
    }
}
