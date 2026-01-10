using System;
using System.Collections.Generic;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Helpers;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneGeneralViewModel : BaseModel
{
    public Settings Settings { get; }

    public SettingsPaneGeneralViewModel(Settings settings)
    {
        Settings = settings;
        UpdateEnumDropdownLocalizations();
    }

    public class SearchWindowScreenData : DropdownDataGeneric<SearchWindowScreens> { }
    public class SearchWindowAlignData : DropdownDataGeneric<SearchWindowAligns> { }
    public class SearchPrecisionData : DropdownDataGeneric<SearchPrecisionScore> { }
    public class LastQueryModeData : DropdownDataGeneric<LastQueryMode> { }

    public List<SearchWindowScreenData> SearchWindowScreens { get; } =
        DropdownDataGeneric<SearchWindowScreens>.GetValues<SearchWindowScreenData>("SearchWindowScreen");

    public List<SearchWindowAlignData> SearchWindowAligns { get; } =
        DropdownDataGeneric<SearchWindowAligns>.GetValues<SearchWindowAlignData>("SearchWindowAlign");

    public List<SearchPrecisionData> SearchPrecisionScores { get; } =
        DropdownDataGeneric<SearchPrecisionScore>.GetValues<SearchPrecisionData>("SearchPrecision");

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

    public List<LastQueryModeData> LastQueryModes { get; } =
        DropdownDataGeneric<LastQueryMode>.GetValues<LastQueryModeData>("LastQuery");

    private void UpdateEnumDropdownLocalizations()
    {
        DropdownDataGeneric<SearchWindowScreens>.UpdateLabels(SearchWindowScreens);
        DropdownDataGeneric<SearchWindowAligns>.UpdateLabels(SearchWindowAligns);
        DropdownDataGeneric<SearchPrecisionScore>.UpdateLabels(SearchPrecisionScores);
        DropdownDataGeneric<LastQueryMode>.UpdateLabels(LastQueryModes);
        // Since we are using Binding instead of DynamicResource, we need to manually trigger the update
        OnPropertyChanged(nameof(AlwaysPreviewToolTip));
        Settings.CustomExplorer.OnDisplayNameChanged();
    }

    public string AlwaysPreviewToolTip => Localize.AlwaysPreviewToolTip(Settings.PreviewHotkey);

    [RelayCommand]
    private void SelectFileManager()
    {
        var fileManagerChangeWindow = new SelectFileManagerWindow();
        fileManagerChangeWindow.ShowDialog();
    }

    public bool AlwaysRunAsAdministrator
    {
        get => Settings.AlwaysRunAsAdministrator;
        set
        {
            if (AlwaysRunAsAdministrator == value) return;

            Settings.AlwaysRunAsAdministrator = value;
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
