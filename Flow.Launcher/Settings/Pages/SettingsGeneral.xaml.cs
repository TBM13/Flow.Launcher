namespace Flow.Launcher.Settings.Pages;

public partial class SettingsGeneral
{
    private SettingsGeneralViewModel? _vm => DataContext as SettingsGeneralViewModel;

    public SettingsGeneral()
    {
        InitializeComponent();
    }

    private void RunAsAdmin_Toggled(object sender, System.Windows.RoutedEventArgs e)
    {
        if (IsLoaded) // Ignore initial value change
            _vm?.CheckAdminChangeAndAskForRestart();
    }
}
