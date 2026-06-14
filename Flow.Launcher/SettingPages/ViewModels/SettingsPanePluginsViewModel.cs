using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.Infrastructure.WPF;
using Flow.Launcher.ViewModel;
using iNKORE.UI.WPF.Modern.Controls;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPanePluginsViewModel(ISettingsAPI settings) : ObservableObject
{
    private readonly ISettingsAPI _settings = settings;

    public IReadOnlyList<LocalizedEnumItem<DisplayMode>> DisplayModes { get; } =
        EnumLocalization<DisplayMode>.Items;

    public DisplayMode SelectedDisplayMode
    {
        get => field;
        set
        {
            SetProperty(ref field, value);
            UpdateDisplayModeFromSelection();
        }
    }

    [ObservableProperty]
    public partial bool IsOnOffSelected { get; set; }

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
    public List<PluginViewModel> PluginViewModels => _pluginViewModels ??= App.API.GetAllPlugins()
        .OrderBy(plugin => plugin.Disabled)
        .ThenBy(plugin => plugin.Name)
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
            App.API.FuzzySearch(FilterText, plugin.PluginMetadata.Name).IsSearchPrecisionScoreMet ||
            App.API.FuzzySearch(FilterText, plugin.PluginMetadata.Description).IsSearchPrecisionScoreMet;
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
                        Text = (string)Application.Current.Resources["priority_tips"],
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = (string)Application.Current.Resources["homeTitle"],
                        FontSize = 18,
                        Margin = new Thickness(0, 24, 0, 10),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = (string)Application.Current.Resources["homeTips"],
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            },
            PrimaryButtonText = (string)Application.Current.Resources["commonOK"],
            CornerRadius = new CornerRadius(8),
            Style = (Style)Application.Current.Resources["ContentDialog"]
        };

        await helpDialog.ShowAsync();
    }

    private void UpdateDisplayModeFromSelection()
    {
        switch (SelectedDisplayMode)
        {
            case DisplayMode.Priority:
                IsOnOffSelected = false;
                IsPrioritySelected = true;
                IsHomeOnOffSelected = false;
                break;
            case DisplayMode.HomeOnOff:
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

public enum DisplayMode
{
    [Description("Enabled")]
    OnOff,
    [Description("Priority")]
    Priority,
    [Description("Home Page")]
    HomeOnOff
}
