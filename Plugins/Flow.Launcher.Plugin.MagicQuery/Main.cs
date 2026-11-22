using System.ComponentModel;
using System.IO;
using System.Windows.Controls;
using Flow.Launcher.Interop;
using Flow.Launcher.Interop.Programs;
using Flow.Launcher.Plugin.MagicQuery.Settings;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;

namespace Flow.Launcher.Plugin.MagicQuery;

public static class PluginMetadataDefinition
{
    public static readonly PluginMetadata Metadata = new()
    {
        ID = "8f4b2c71-3d9e-4a1b-9c6e-2f5a8d1e4b3c",
        ActionKeywords = [],
        Name = "Magic Query",
        Description = "Configure custom magic queries based on the active window's title and process path.",
        Author = "TBM13",
        Version = "1.0.0",
        IcoPath = "Images/Plugin.MagicQuery.png",

        Plugin = new Main()
    };
}

public sealed class Main : IPlugin, ISettingProvider, IMagicQueryProvider
{
    private Settings.Settings _settings = null!;
    private SettingsViewModel _viewModel = null!;

    public Control CreateSettingPanel() => new SettingsControl(_viewModel);

    public ValueTask InitAsync(PluginInitContext context)
    {
        _settings = context.API.LoadSettingJsonStorage<Settings.Settings>();
        _viewModel = new SettingsViewModel(_settings);
        return ValueTask.CompletedTask;
    }

    public string? GenerateMagicQuery()
    {
        if (_settings.Rules.Count == 0)
            return null;

        nint hwnd = WindowHelper.GetForegroundWindow();
        if (hwnd == 0)
            return null;

        uint pid;
        try
        {
            _ = WindowHelper.GetWindowThreadProcessId(hwnd, out pid);
            uint currentPid = (uint)Environment.ProcessId;
            while (pid == currentPid)
            {
                hwnd = WindowHelper.GetNextVisibleWindow(hwnd);
                if (hwnd == 0)
                    return null;

                _ = WindowHelper.GetWindowThreadProcessId(hwnd, out pid);
            }
        }
        catch (Win32Exception)
        {
            return null;
        }

        string? windowTitle = null;
        try
        {
            windowTitle = WindowHelper.GetWindowTitle(hwnd);
        }
        catch (Win32Exception)
        {
        }

        string? processPath = null;
        try
        {
            processPath = ProcessHelper.GetProcessFileName(pid);
        }
        catch (Exception ex) when (ex is Win32Exception or PathTooLongException)
        {
        }

        foreach (MagicQueryRule rule in _settings.Rules)
        {
            string? result = rule.TryMatch(windowTitle, processPath);
            if (result is not null)
                return result;
        }

        return null;
    }
}
