using System.Collections.Generic;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Core;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Helpers;
using Flow.Launcher.Infrastructure.WPF;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneGeneralViewModel(Settings settings) : ObservableObject
{
    public Settings Settings { get; } = settings;

    public List<LocalizedEnumItem<SearchWindowScreens>> SearchWindowScreens { get; } =
        EnumLocalization.GetLocalizedEnumItems<SearchWindowScreens>();

    public List<LocalizedEnumItem<SearchWindowAligns>> SearchWindowAligns { get; } =
        EnumLocalization.GetLocalizedEnumItems<SearchWindowAligns>();

    public List<LocalizedEnumItem<SearchPrecisionScore>> SearchPrecisionScores { get; } =
        EnumLocalization.GetLocalizedEnumItems<SearchPrecisionScore>();

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

    public List<LocalizedEnumItem<LastQueryModes>> LastQueryModes { get; } =
        EnumLocalization.GetLocalizedEnumItems<LastQueryModes>();

    public static string AlwaysPreviewToolTip
        => Localize.AlwaysPreviewToolTip(DefaultHotkeys.TogglePreview.Hotkey.ToString());

    public bool AlwaysRunAsAdministrator
    {
        get => Settings.AlwaysRunAsAdministrator;
        set
        {
            if (AlwaysRunAsAdministrator == value) return;

            Settings.AlwaysRunAsAdministrator = value;
            OnPropertyChanged();
            CheckAdminChangeAndAskForRestart();
        }
    }

    private void CheckAdminChangeAndAskForRestart()
    {
        // When we change from non-admin to admin, we need to restart the app as administrator to apply the changes
        // Under non-administrator, we cannot delete or set the logon task which is run as administrator
        if (AlwaysRunAsAdministrator && !Win32Helper.IsAdministrator())
        {
            if (App.API.ShowMsgBox(
                App.API.GetTranslation("runAsAdministratorChangeAndRestart"),
                App.API.GetTranslation("runAsAdministratorChange"),
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                // Restart the app as administrator
                App.API.RestartAppAsAdmin();
            }
        }
    }
}
