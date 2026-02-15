using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Plugins;
using Flow.Launcher.Infrastructure.Plugins.Interfaces;
using Flow.Launcher.Infrastructure.Results;
using Flow.Launcher.Resources.Controls;

namespace Flow.Launcher.ViewModel
{
    public partial class PluginViewModel : ObservableObject
    {
        private static readonly string ClassName = nameof(PluginViewModel);

        private static readonly Settings Settings = Ioc.Default.GetRequiredService<Settings>();

        private static readonly Thickness SettingPanelMargin = (Thickness)Application.Current.FindResource("SettingPanelMargin");
        private static readonly Thickness SettingPanelItemTopBottomMargin = (Thickness)Application.Current.FindResource("SettingPanelItemTopBottomMargin");

        public PluginMetadata PluginMetadata { get; init; }

        private async Task LoadIconAsync()
        {
            Image = await App.API.LoadImageAsync(PluginMetadata.IcoPath);
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
            set => _image = value;
        }

        public bool PluginState
        {
            get => !PluginMetadata.Disabled;
            set
            {
                PluginMetadata.Disabled = !value;
                PluginSettingsObject.Disabled = !value;
            }
        }

        public bool PluginHomeState
        {
            get => !PluginMetadata.HomeDisabled;
            set
            {
                PluginMetadata.HomeDisabled = !value;
                PluginSettingsObject.HomeDisabled = !value;
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(SettingControl));
            }
        }

        public int Priority
        {
            get => PluginMetadata.Priority;
            set
            {
                PluginMetadata.Priority = value;
                PluginSettingsObject.Priority = value;
            }
        }

        private Control _settingControl;
        private bool _isExpanded;

        private Control _bottomPart1;
        public Control BottomPart1 => IsExpanded ? _bottomPart1 ??= new InstalledPluginDisplayKeyword() : null;

        private Control _bottomPart2;
        public Control BottomPart2 => IsExpanded ? _bottomPart2 ??= new InstalledPluginDisplayBottomData() : null;

        public bool HasSettingControl =>
            // Here we do not check if the plugin is initialized successfully
            // So we can let users change settings for initializing or initialization failed plugins
            PluginMetadata.Plugin is ISettingProvider;

        public Control SettingControl
            => IsExpanded
                ? _settingControl
                    ??= HasSettingControl
                        ? TryCreateSettingPanel(PluginMetadata)
                        : null
                : null;
        private ImageSource _image = ImageLoader.MissingImage;

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
                App.API.LogException(ClassName, $"Failed to create setting panel for {metadata.Name}", e);

                // Show error message in UI
                var errorMsg = Localize.errorCreatingSettingPanel(metadata.Name, Environment.NewLine, e.Message);
                return CreateErrorSettingPanel(errorMsg);
            }
        }

        public string Version => Localize.plugin_query_version() + " " + PluginMetadata.Version;
        public string ActionKeywordsText => string.Join(Query.TermSeparator, PluginMetadata.ActionKeywords);
        public Infrastructure.UserSettings.Plugin PluginSettingsObject { get; init; }
        public bool HomeEnabled => Settings.ShowHomePage && PluginManager.IsHomePlugin(PluginMetadata.ID);

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
                Margin = SettingPanelMargin
            };
            var textBox = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                TextWrapping = TextWrapping.Wrap,
                Margin = SettingPanelItemTopBottomMargin
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
