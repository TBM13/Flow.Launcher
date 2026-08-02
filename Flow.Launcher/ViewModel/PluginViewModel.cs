using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Image;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Logging;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;
using PluginSettingsObj = Flow.Launcher.Core.UserSettings.Plugin;

namespace Flow.Launcher.ViewModel
{
    public partial class PluginViewModel : ObservableObject
    {
        private readonly Logger<PluginViewModel> _logger;
        private readonly IImageLoader _imageLoader;
        private readonly PluginSettingsObj _pluginSettingsObj;

        public PluginMetadata PluginMetadata { get; }

        [ObservableProperty]
        public partial ImageSource Image { get; set; }

        public bool PluginState
        {
            get => !PluginMetadata.Disabled;
            set
            {
                PluginMetadata.Disabled = !value;
                _pluginSettingsObj.Disabled = !value;
                OnPropertyChanged();
            }
        }

        public bool PluginHomeState
        {
            get => !PluginMetadata.HomeDisabled;
            set
            {
                PluginMetadata.HomeDisabled = !value;
                _pluginSettingsObj.HomeDisabled = !value;
                OnPropertyChanged();
            }
        }

        public int Priority
        {
            get => PluginMetadata.Priority;
            set
            {
                PluginMetadata.Priority = value;
                _pluginSettingsObj.Priority = value;
                OnPropertyChanged();
            }
        }

        public string Version => "Version " + PluginMetadata.Version;
        public string ActionKeywordsText => string.Join(Query.TermSeparator, PluginMetadata.ActionKeywords);

        public PluginViewModel(
            Logger<PluginViewModel> logger, IImageLoader imageLoader, PluginMetadata plugin, PluginSettingsObj settingsObj)
        {
            _logger = logger;
            _imageLoader = imageLoader;
            PluginMetadata = plugin;
            _pluginSettingsObj = settingsObj;

            Image = _imageLoader.GenericProgramIcon;
            _ = LoadIconAsync();
        }

        private async Task LoadIconAsync()
        {
            Image = await _imageLoader.LoadAsync(PluginMetadata.IcoPath);
        }

        public Control? TryCreateSettingPanel()
        {
            if (PluginMetadata.Plugin is not ISettingProvider settingProvider)
                return null;

            try
            {
                return settingProvider.CreateSettingPanel();
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to create settings panel for {PluginMetadata.Name}");
                return null;
            }
        }

        public void OnActionKeywordsTextChanged()
        {
            OnPropertyChanged(nameof(ActionKeywordsText));
        }

        [RelayCommand]
        private void SetActionKeywords()
        {
            ActionKeywords changeKeywordsWindow = new(this);
            changeKeywordsWindow.ShowDialog();
        }
    }
}
