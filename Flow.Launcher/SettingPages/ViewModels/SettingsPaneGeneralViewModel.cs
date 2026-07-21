using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Core.Hotkeys;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.Interop.Hardware;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.WPF;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneGeneralViewModel(ISettingsAPI settings) : ObservableObject
{
    public ISettingsAPI Settings { get; } = settings;

    public IReadOnlyList<LocalizedEnumItem<DisplayType>> Display { get; } =
        EnumLocalization<DisplayType>.Items;

    public IReadOnlyList<LocalizedEnumItem<DisplayPosition>> DisplayPositions { get; } =
        EnumLocalization<DisplayPosition>.Items;

    public IReadOnlyList<LocalizedEnumItem<SearchPrecision>> SearchPrecisionScores { get; } =
        EnumLocalization<SearchPrecision>.Items;

    public List<int> ScreenNumbers
    {
        get
        {
            var screens = MonitorHelper.GetDisplayMonitors();
            var screenNumbers = new List<int>();
            for (var i = 1; i <= screens.Count; i++)
            {
                screenNumbers.Add(i);
            }

            return screenNumbers;
        }
    }

    public IReadOnlyList<LocalizedEnumItem<LastQueryMode>> LastQueryMode { get; } =
        EnumLocalization<LastQueryMode>.Items;

    public static string AlwaysPreviewToolTip
        => $"Always open preview panel when Flow activates. Press {DefaultHotkeys.TogglePreview.Hotkey} to toggle preview.";

    public bool AlwaysRunAsAdmin
    {
        get => Settings.AlwaysRunAsAdmin;
        set
        {
            if (AlwaysRunAsAdmin == value) return;

            Settings.AlwaysRunAsAdmin = value;
            OnPropertyChanged();
            CheckAdminChangeAndAskForRestart();
        }
    }

    private void CheckAdminChangeAndAskForRestart()
    {
        // When we change from non-admin to admin, we need to restart the app as administrator to apply the changes
        // Under non-administrator, we cannot delete or set the logon task which is run as administrator
        if (AlwaysRunAsAdmin && !Environment.IsPrivilegedProcess)
        {
            if (IPublicAPI.Instance.ShowMsgBox(
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
