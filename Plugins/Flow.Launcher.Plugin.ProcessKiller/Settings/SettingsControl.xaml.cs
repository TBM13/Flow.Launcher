using System.Windows.Controls;

namespace Flow.Launcher.Plugin.ProcessKiller.Settings;

public partial class SettingsControl : UserControl
{
    public SettingsControl(SettingsViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
    }
}
