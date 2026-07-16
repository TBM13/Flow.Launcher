using Flow.Launcher.Plugin.Explorer.ViewModels;

namespace Flow.Launcher.Plugin.Explorer.Views;

public partial class ExplorerSettings
{
    public ExplorerSettings(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private void AllowOnlyNumericInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        e.Handled = e.Text.ToCharArray().Any(c => !char.IsDigit(c));
    }
}
