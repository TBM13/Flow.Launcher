using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.ViewModel;
using iNKORE.UI.WPF.Modern.Controls;

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


public partial class SettingsPluginsViewModel(ISettingsAPI settings) : BaseSettingsPageViewModel
{
    private readonly ISettingsAPI _settings = settings;

    public override string Title => "Plugins";
    public override string IconPath => "pack://application:,,,/Images/plugins.png";

    public PluginDisplayMode SelectedDisplayMode
    {
        get;
        set
        {
            SetProperty(ref field, value);
            UpdateDisplayModeFromSelection();
        }
    }

    [ObservableProperty]
    public partial bool IsOnOffSelected { get; set; } = true;

    [ObservableProperty]
    public partial bool IsPrioritySelected { get; set; }
    [ObservableProperty]
    public partial bool IsHomeOnOffSelected { get; set; }

    [ObservableProperty]
    public partial string FilterText { get; set; }

    private List<PluginViewModel>? _pluginViewModels;
    // Get all plugins: Initializing & Initialized & Init failed plugins
    // Include init failed ones so that we can uninstall them
    // Include initializing ones so that we can change related settings like action keywords, etc.
    public List<PluginViewModel> PluginViewModels => _pluginViewModels ??= IPublicAPI.Instance.GetAllPlugins()
        .OrderBy(plugin => plugin.Name)
        .Select(plugin => new PluginViewModel
        {
            PluginMetadata = plugin,
            PluginSettingsObject = _settings.PluginSettings.GetPluginSettings(plugin.ID)
        })
        .Where(plugin => plugin.PluginSettingsObject != null)
        .ToList();

    public bool SatisfiesFilter(PluginViewModel plugin)
    {
        return string.IsNullOrEmpty(FilterText) ||
            IPublicAPI.Instance.FuzzySearch(FilterText, plugin.PluginMetadata.Name).IsSearchPrecisionScoreMet ||
            IPublicAPI.Instance.FuzzySearch(FilterText, plugin.PluginMetadata.Description).IsSearchPrecisionScoreMet;
    }

    [RelayCommand]
    private async Task OpenHelperAsync(Button button)
    {
        var helpDialog = new ContentDialog()
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

    private void UpdateDisplayModeFromSelection()
    {
        switch (SelectedDisplayMode)
        {
            case PluginDisplayMode.Priority:
                IsOnOffSelected = false;
                IsPrioritySelected = true;
                IsHomeOnOffSelected = false;
                break;
            case PluginDisplayMode.HomeOnOff:
                IsOnOffSelected = false;
                IsPrioritySelected = false;
                IsHomeOnOffSelected = true;
                break;
            default:
                IsOnOffSelected = true;
                IsPrioritySelected = false;
                IsHomeOnOffSelected = false;
                break;
        }
    }
}
