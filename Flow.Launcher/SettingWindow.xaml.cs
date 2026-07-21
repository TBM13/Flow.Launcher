using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.Resources.Controls;
using Flow.Launcher.SettingPages.Views;
using Flow.Launcher.ViewModel;
using iNKORE.UI.WPF.Modern.Controls;

namespace Flow.Launcher;

public partial class SettingWindow
{
    private readonly ISettingsAPI _settings;
    private readonly SettingWindowViewModel _viewModel;
    public SettingWindow()
    {
        _settings = Ioc.Default.GetRequiredService<ISettingsAPI>();
        _viewModel = Ioc.Default.GetRequiredService<SettingWindowViewModel>();
        DataContext = _viewModel;
        UpdateWindowState();
        InitializeComponent();
    }

    #region Window Events

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateWindowState();

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    // Sometimes the navigation is not triggered by button click,
    // so we need to update the selected item here
    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingWindowViewModel.PageType):
                var selectedIndex = _viewModel.PageType?.Name switch
                {
                    nameof(SettingsPaneGeneral) => 0,
                    nameof(SettingsPanePlugins) => 1,
                    nameof(SettingsPaneTheme) => 2,
                    nameof(SettingsPaneHotkey) => 3,
                    nameof(SettingsPaneAbout) => 4,
                    _ => 0
                };
                NavView.SelectedItem = NavView.MenuItems[selectedIndex];
                break;
        }
    }

    private void OnClosed(object sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;

        // If app is exiting, settings save is not needed because main window closing event will handle this
        if (App.App.LoadingOrExiting) return;
        // Save settings when window is closed
        _settings.Save();
        IPublicAPI.Instance.SavePluginSettings();
    }

    private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        Close();
    }

    private void window_MouseDown(object sender, MouseButtonEventArgs e) /* for close hotkey popup */
    {
        if (Keyboard.FocusedElement is not TextBox textBox) return;
        var tRequest = new TraversalRequest(FocusNavigationDirection.Next);
        textBox.MoveFocus(tRequest);
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (IsLoaded && WindowState != WindowState.Minimized)
            _settings.SettingWindowState = WindowState;
    }

    #endregion

    public void UpdateWindowState()
    {
        WindowState = _settings.SettingWindowState == WindowState.Minimized
            ? WindowState.Normal
            : _settings.SettingWindowState;
    }

    #region Navigation View Events

    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            _viewModel.SetPageType(typeof(SettingsPaneGeneral));
            ContentFrame.Navigate(typeof(SettingsPaneGeneral));
        }
        else
        {
            var selectedItem = (NavigationViewItem)args.SelectedItem;
            if (selectedItem == null)
            {
                NavView_Loaded(sender, null); /* Reset First Page */
                return;
            }

            var pageType = selectedItem.Name switch
            {
                nameof(General) => typeof(SettingsPaneGeneral),
                nameof(Plugins) => typeof(SettingsPanePlugins),
                nameof(Theme) => typeof(SettingsPaneTheme),
                nameof(Hotkey) => typeof(SettingsPaneHotkey),
                nameof(About) => typeof(SettingsPaneAbout),
                _ => typeof(SettingsPaneGeneral)
            };
            // Only navigate if the page type changes to fix navigation forward/back issue
            if (_viewModel.SetPageType(pageType))
            {
                ContentFrame.Navigate(pageType);
            }
        }
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.IsLoaded)
        {
            ContentFrame_Loaded(sender, e);
        }
        else
        {
            ContentFrame.Loaded += ContentFrame_Loaded;
        }
    }

    private void ContentFrame_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.SetPageType(null);
        NavView.SelectedItem = NavView.MenuItems[0]; /* Set First Page */
    }

    #endregion
}
