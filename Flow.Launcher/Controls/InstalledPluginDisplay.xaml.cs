using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Controls;

public partial class InstalledPluginDisplay
{
    private PluginViewModel? _vm => DataContext as PluginViewModel;

    public InstalledPluginDisplay()
    {
        InitializeComponent();
        DataContextChanged += (s, e) =>
        {
            if (e.NewValue is PluginViewModel vm && PluginExpander.IsExpanded)
                PluginSettingsControl.Content = vm.TryCreateSettingPanel();
            else
                PluginSettingsControl.Content = null;
        };

        PluginExpander.Expanded += (s, e) =>
        {
            if (_vm is not null && PluginSettingsControl.Content is null && PluginExpander.IsExpanded)
                PluginSettingsControl.Content = _vm.TryCreateSettingPanel();
        };
    }
}
