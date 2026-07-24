using System.Windows;

namespace Flow.Launcher.Settings;

public partial class SettingWindow : Window
{
    private readonly SettingViewModel _vm;
    public SettingWindow(SettingViewModel vm)
    {
        DataContext = _vm = vm;
        WindowState = _vm.Settings.SettingWindowMaximized ? WindowState.Maximized : WindowState.Normal;
        InitializeComponent();
    }

    private void OnClosed(object sender, EventArgs e)
    {
        _vm.SaveAllSettings();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (IsActive && WindowState != WindowState.Minimized)
            _vm.Settings.SettingWindowMaximized = WindowState == WindowState.Maximized;
    }

    private void NavigationView_SelectionChanged(iNKORE.UI.WPF.Modern.Controls.NavigationView sender, iNKORE.UI.WPF.Modern.Controls.NavigationViewSelectionChangedEventArgs args)
    {
        if (IsLoaded)
            PageScrollViewer.ScrollToTop();
    }
}
