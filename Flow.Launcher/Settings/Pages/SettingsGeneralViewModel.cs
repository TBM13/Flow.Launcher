using System.Windows;
using Flow.Launcher.Core.Hotkeys;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.Interop.Hardware;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Logging;

namespace Flow.Launcher.Settings.Pages;

public partial class SettingsGeneralViewModel(
    Logger<SettingsGeneralViewModel> logger, ISettingsAPI settings) : BaseSettingsPageViewModel
{
    private readonly Logger<SettingsGeneralViewModel> _logger = logger;

    public override string Title => "General";
    public override string IconPath => "pack://application:,,,/Images/settings.png";

    public ISettingsAPI Settings { get; } = settings;

    public List<int> ScreenNumbers
    {
        get
        {
            try
            {
                return [.. MonitorHelper.GetDisplayMonitors().Select((_, index) => index + 1)];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get screen numbers");
                return [];
            }
        }
    }

    public static string AlwaysPreviewToolTip
        => $"Always open preview panel when Flow activates. Press {DefaultHotkeys.TogglePreview.Hotkey} to toggle preview.";

    /// <summary>
    /// If "Always run as admin" is on and we are not running with admin privileges,
    /// asks the user if they want to restart the app as admin.
    /// </summary>
    public void CheckAdminChangeAndAskForRestart()
    {
        if (Settings.AlwaysRunAsAdmin && !Environment.IsPrivilegedProcess)
        {
            if (MessageBox.Show(
                "Do you want to restart as administrator to apply this change? Otherwise, you will need to run as administrator manually on the next start.",
                "Administrator Mode Change",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                // Restart the app as administrator
                IPublicAPI.Instance.RestartAppAsAdmin();
            }
        }
    }
}
