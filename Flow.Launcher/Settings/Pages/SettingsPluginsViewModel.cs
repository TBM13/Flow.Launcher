using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.PluginSDK.UI;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.ViewModel;
using iNKORE.UI.WPF.Modern.Controls;
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
    private async Task OpenHelperAsync(Button button)
    {
        ContentDialog helpDialog = new()
        {
            Owner = Window.GetWindow(button),
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = (string)Application.Current.Resources["priority"],
                        FontSize = 18,
                        Margin = new Thickness(0, 0, 0, 10),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = "Greater the number, the higher the result will be ranked. Try setting it as 5. If you want the results to be lower than any other plugin's, provide a negative number",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = "Home Page",
                        FontSize = 18,
                        Margin = new Thickness(0, 24, 0, 10),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = "Enable the plugin home page state if you like to show the plugin results when query is empty.",
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            },
            PrimaryButtonText = "OK",
            CornerRadius = new CornerRadius(8),
        };

        await helpDialog.ShowAsync();
    }
}
