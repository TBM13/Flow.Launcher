using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.MagicQuery.Settings;

public partial class SettingsControl : UserControl
{
    public SettingsControl(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MagicQueryRule rule })
            return;

        OpenFileDialog dialog = new()
        {
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Select process executable"
        };

        if (!string.IsNullOrWhiteSpace(rule.ProcessPathPattern))
        {
            try
            {
                string? initialDir = Path.GetDirectoryName(rule.ProcessPathPattern);
                if (Directory.Exists(initialDir))
                    dialog.InitialDirectory = initialDir;
            }
            catch (ArgumentException)
            {
            }
        }

        if (dialog.ShowDialog() == true)
        {
            rule.ProcessPathPattern = dialog.FileName;
        }
    }
}
