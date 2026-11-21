using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Flow.Launcher.Plugin.Program.Settings;

public partial class SettingsControl : UserControl
{
    public SettingsControl(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void CustomSuffixesTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is SettingsViewModel vm)
        {
            vm.ApplyCustomSuffixesCommand.Execute(null);
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void CustomSuffixesTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.ApplyCustomSuffixesCommand.Execute(null);
        }
    }

    private void CustomDirectories_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop)
            && e.Data.GetData(DataFormats.FileDrop) is string[] paths
            && paths.Any(Directory.Exists))
        {
            e.Effects = DragDropEffects.Link;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void CustomDirectories_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is SettingsViewModel vm
            && e.Data.GetDataPresent(DataFormats.FileDrop)
            && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            if (vm.AddDirectories(paths))
            {
                CustomDirectoriesExpander.IsExpanded = true;
            }
        }

        e.Handled = true;
    }
}
