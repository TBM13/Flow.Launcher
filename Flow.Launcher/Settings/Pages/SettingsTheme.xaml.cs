using System.Windows.Controls;
using Flow.Launcher.Core.Settings;

namespace Flow.Launcher.Settings.Pages;

public partial class SettingsTheme
{
    private SettingsThemeViewModel? _vm => DataContext as SettingsThemeViewModel;

    public SettingsTheme()
    {
        InitializeComponent();
    }

    private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && e.OriginalSource is ComboBox themeCombobox)
            _vm?.ChangeScheme((ColorScheme)themeCombobox.SelectedValue);
    }
}
