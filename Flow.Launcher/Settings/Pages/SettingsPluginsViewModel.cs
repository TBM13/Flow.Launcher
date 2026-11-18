using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.UI;
using Flow.Launcher.ViewModel;
using Microsoft.Extensions.Logging;

namespace Flow.Launcher.Settings.Pages;

public enum PluginDisplayMode
{
    [Description("Enabled")]
    OnOff,
    [Description("Priority")]
    Priority,
    [Description("Home Page")]
    HomeOnOff
}


public partial class SettingsPluginsViewModel(
    ILoggerFactory loggerFactory, ISettingsAPI settings, IImageLoader imageLoader,
    PluginManager pluginManager) : BaseSettingsPageViewModel
{
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly ISettingsAPI _settings = settings;
    private readonly IImageLoader _imageLoader = imageLoader;
    private readonly PluginManager _pluginManager = pluginManager;

    public override string Title => "Plugins";
    public override string IconPath => "pack://application:,,,/Images/plugins.png";

    public IEnumerable<PluginViewModel> Plugins => _pluginManager.GetAllLoadedPlugins()
        .OrderBy(p => p.Name)
        .Select(plugin => new PluginViewModel(
            new(_loggerFactory), _imageLoader, plugin, _settings.PluginSettings.GetPluginSettings(plugin.ID)));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnOffMode))]
    [NotifyPropertyChangedFor(nameof(IsPriorityMode))]
    [NotifyPropertyChangedFor(nameof(IsHomeOnOffMode))]
    public partial PluginDisplayMode SelectedDisplayMode { get; set; }

    public IReadOnlyList<LocalizedEnumItem<PluginDisplayMode>> DisplayModeItems => EnumLocalization<PluginDisplayMode>.Items;

    public bool IsOnOffMode => SelectedDisplayMode == PluginDisplayMode.OnOff;
    public bool IsPriorityMode => SelectedDisplayMode == PluginDisplayMode.Priority;
    public bool IsHomeOnOffMode => SelectedDisplayMode == PluginDisplayMode.HomeOnOff;

    [RelayCommand]
    private static void OpenHelper(Button button)
    {
        MessageBox.Show(
            Window.GetWindow(button),
            "Priority:\nGreater the number, the higher the result will be ranked. Try setting it as 5. If you want the results to be lower than any other plugin's, provide a negative number.\n\nHome Page:\nEnable the plugin home page state if you like to show the plugin results when query is empty.",
            "Advanced Settings",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
