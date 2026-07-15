using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.Infrastructure.Helpers;
using Flow.Launcher.Infrastructure.WPF;

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
        => Localize.AlwaysPreviewToolTip(DefaultHotkeys.TogglePreview.Hotkey.ToString());

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
            if (App.App.API.ShowMsgBox(
                App.App.API.GetTranslation("runAsAdministratorChangeAndRestart"),
                App.App.API.GetTranslation("runAsAdministratorChange"),
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                // Restart the app as administrator
                App.App.API.RestartAppAsAdmin();
            }
        }
    }
}
