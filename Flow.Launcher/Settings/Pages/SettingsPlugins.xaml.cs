using System.Windows.Data;
using System.Windows.Input;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Settings.Pages;

public partial class SettingsPlugins
{
    private SettingsPluginsViewModel? _vm => DataContext as SettingsPluginsViewModel;

    public SettingsPlugins()
    {
        InitializeComponent();
    }

    private void SettingsPanePlugins_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers is ModifierKeys.Control && e.Key is not Key.F)
            PluginFilterTextbox.Focus();
    }

    private void PluginCollectionView_OnFilter(object sender, FilterEventArgs e)
    {
        if (e.Item is not PluginViewModel plugin)
        {
            e.Accepted = false;
            return;
        }

        e.Accepted = _vm?.SatisfiesFilter(plugin) ?? false;
    }
}
