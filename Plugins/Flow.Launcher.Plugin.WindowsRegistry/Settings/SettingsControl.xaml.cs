using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.WindowsRegistry.Settings;

public partial class SettingsControl : UserControl
{
    private readonly SettingsViewModel _viewModel;

    public SettingsControl(SettingsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Select regjump.exe"
        };

        if (!string.IsNullOrWhiteSpace(_viewModel.Settings.RegJumpPath))
        {
            string? initialDir = Path.GetDirectoryName(_viewModel.Settings.RegJumpPath);
            if (Directory.Exists(initialDir))
                dialog.InitialDirectory = initialDir;
        }

        if (dialog.ShowDialog() == true)
        {
            _viewModel.Settings.RegJumpPath = dialog.FileName;
        }
    }
}
