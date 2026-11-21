using System.Windows.Controls;

namespace Flow.Launcher.Plugin.WindowsDevices.Settings;

public partial class SettingsControl : UserControl
{
    public SettingsControl(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
