using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Image;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Logging;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;

namespace Flow.Launcher.ViewModel
{
    public partial class PluginViewModel : ObservableObject
    {
        // TODO: Check if there is any better alternative
        private static readonly Logger<PluginViewModel> _logger = Ioc.Default.GetRequiredService<Logger<PluginViewModel>>();
        private static readonly ImageLoader _imageLoader = Ioc.Default.GetRequiredService<ImageLoader>();
        private static readonly PluginManager _pluginManager = Ioc.Default.GetRequiredService<PluginManager>();
        private static readonly ISettingsAPI _settings = Ioc.Default.GetRequiredService<ISettingsAPI>();
        private static readonly Thickness _settingPanelMargin = (Thickness)Application.Current.FindResource("SettingPanelMargin");
        private static readonly Thickness _settingPanelItemTopBottomMargin = (Thickness)Application.Current.FindResource("SettingPanelItemTopBottomMargin");

        public required PluginMetadata PluginMetadata { get; init; }

        private async Task LoadIconAsync()
        {
            Image = await IPublicAPI.Instance.LoadImageAsync(PluginMetadata.IcoPath);
            OnPropertyChanged(nameof(Image));
        }

        private bool _imageLoaded = false;
        public ImageSource Image
        {
            get
            {
                if (!_imageLoaded)
                {
                    _imageLoaded = true;
                    _ = LoadIconAsync();
                }

                return _image;
            }
            set => SetProperty(ref _image, value);
        }

        public bool PluginState
        {
            get => !PluginMetadata.Disabled;
            set
            {
                PluginMetadata.Disabled = !value;
                PluginSettingsObject.Disabled = !value;
                OnPropertyChanged();
            }
        }

        public bool PluginHomeState
        {
            get => !PluginMetadata.HomeDisabled;
            set
            {
                PluginMetadata.HomeDisabled = !value;
                PluginSettingsObject.HomeDisabled = !value;
                OnPropertyChanged();
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SettingControl))]
        public partial bool IsExpanded { get; set; }

        public int Priority
        {
            get => PluginMetadata.Priority;
            set
            {
                PluginMetadata.Priority = value;
                PluginSettingsObject.Priority = value;
                OnPropertyChanged();
            }
        }

        private Control? _settingControl;

        public bool HasSettingControl =>
            // Here we do not check if the plugin is initialized successfully
            // So we can let users change settings for initializing or initialization failed plugins
            PluginMetadata.Plugin is ISettingProvider;

        public Control? SettingControl
            => IsExpanded
                ? _settingControl
                    ??= HasSettingControl
                        ? TryCreateSettingPanel(PluginMetadata)
                        : null
                : null;
        private ImageSource _image = _imageLoader.GenericProgramIcon;

        private static Control TryCreateSettingPanel(PluginMetadata metadata)
        {
            try
            {
                // We can safely cast here as we already check this in HasSettingControl
                return ((ISettingProvider)metadata.Plugin).CreateSettingPanel();
            }
            catch (Exception e)
            {
                // Log exception
                _logger.LogError(e, $"Failed to create setting panel for {metadata.Name}");

                // Show error message in UI
                string errorMsg = $"Error creating setting panel for plugin {metadata.Name}:\n{e.Message}";
                return CreateErrorSettingPanel(errorMsg);
            }
        }

        public string Version => "Version " + PluginMetadata.Version;
        public string ActionKeywordsText => string.Join(Query.TermSeparator, PluginMetadata.ActionKeywords);
        public Core.UserSettings.Plugin? PluginSettingsObject { get; init; }
        public bool HomeEnabled => _settings.ShowHomePage && _pluginManager.IsHomePlugin(PluginMetadata.ID);

        public void OnActionKeywordsTextChanged()
        {
            OnPropertyChanged(nameof(ActionKeywordsText));
        }

        [RelayCommand]
        private void SetActionKeywords()
        {
            var changeKeywordsWindow = new ActionKeywords(this);
            changeKeywordsWindow.ShowDialog();
        }

        private static UserControl CreateErrorSettingPanel(string text)
        {
            var grid = new Grid()
            {
                Margin = _settingPanelMargin
            };
            var textBox = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                TextWrapping = TextWrapping.Wrap,
                Margin = _settingPanelItemTopBottomMargin
            };
            textBox.SetResourceReference(TextBox.ForegroundProperty, "Color04B");
            grid.Children.Add(textBox);
            return new UserControl
            {
                Content = grid
            };
        }
    }
}
