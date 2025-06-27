using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneGeneralViewModel : BaseModel
{
    public Settings Settings { get; }

    private readonly Internationalization _translater;

    public SettingsPaneGeneralViewModel(Settings settings, Internationalization translater)
    {
        Settings = settings;
        _translater = translater;
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
            var screens = Screen.AllScreens;
            var screenNumbers = new List<int>();
            for (int i = 1; i <= screens.Length; i++)
            {
                screenNumbers.Add(i);
            }

            return screenNumbers;
        }
    }

    public List<LastQueryModeData> LastQueryModes { get; } =
        DropdownDataGeneric<LastQueryMode>.GetValues<LastQueryModeData>("LastQuery");

    public int SearchDelayTimeValue
    {
        get => Settings.SearchDelayTime;
        set
        {
            if (Settings.SearchDelayTime != value)
            {
                Settings.SearchDelayTime = value;
                OnPropertyChanged();
            }
        }
    }

    public int MaxHistoryResultsToShowValue
    {
        get => Settings.MaxHistoryResultsToShowForHomePage;
        set
        {
            if (Settings.MaxHistoryResultsToShowForHomePage != value)
            {
                Settings.MaxHistoryResultsToShowForHomePage = value;
                OnPropertyChanged();
            }
        }
    }

    private void UpdateEnumDropdownLocalizations()
    {
        DropdownDataGeneric<SearchWindowScreens>.UpdateLabels(SearchWindowScreens);
        DropdownDataGeneric<SearchWindowAligns>.UpdateLabels(SearchWindowAligns);
        DropdownDataGeneric<SearchPrecisionScore>.UpdateLabels(SearchPrecisionScores);
        DropdownDataGeneric<LastQueryMode>.UpdateLabels(LastQueryModes);
        // Since we are using Binding instead of DynamicResource, we need to manually trigger the update
        OnPropertyChanged(nameof(AlwaysPreviewToolTip));
    }

    public string Language
    {
        get => Settings.Language;
        set
        {
            _translater.ChangeLanguage(value);

            UpdateEnumDropdownLocalizations();
        }
    }

    #region Korean IME

    // The new Korean IME used in Windows 11 has compatibility issues with WPF. This issue is difficult to resolve within
    // WPF itself, but it can be avoided by having the user switch to the legacy IME at the system level. Therefore,
    // we provide guidance and a direct button for users to make this change themselves. If the relevant registry key does
    // not exist (i.e., the Korean IME is not installed), this setting will not be shown at all.

    public bool LegacyKoreanIMEEnabled
    {
        get => Win32Helper.IsLegacyKoreanIMEEnabled();
        set
        {
            if (Win32Helper.SetLegacyKoreanIMEEnabled(value))
            {
                OnPropertyChanged();
                OnPropertyChanged(nameof(KoreanIMERegistryValueIsZero));
            }
            else
            {
                //Since this is rarely seen text, language support is not provided.
                App.API.ShowMsg("Failed to change Korean IME setting", "Please check your system registry access or contact support.");
            }
        }
    }

    public bool KoreanIMERegistryKeyExists
    {
        get
        {
            var registryKeyExists = Win32Helper.IsKoreanIMEExist();
            var koreanLanguageInstalled = InputLanguage.InstalledInputLanguages.Cast<InputLanguage>().Any(lang => lang.Culture.Name.StartsWith("ko"));
            var isWindows11 = Win32Helper.IsWindows11();

            // Return true if Windows 11 with Korean IME installed, or if the registry key exists
            return (isWindows11 && koreanLanguageInstalled) || registryKeyExists;
        }
    }

    public bool KoreanIMERegistryValueIsZero
    {
        get
        {
            var value = Win32Helper.GetLegacyKoreanIMERegistryValue();
            if (value is int intValue)
            {
                return intValue == 0;
            }
            else if (value != null && int.TryParse(value.ToString(), out var parsedValue))
            {
                return parsedValue == 0;
            }

            return false;
        }
    }

    [RelayCommand]
    private void OpenImeSettings()
    {
        Win32Helper.OpenImeSettings();
    }

    #endregion

    public string AlwaysPreviewToolTip => string.Format(
        App.API.GetTranslation("AlwaysPreviewToolTip"),
        Settings.PreviewHotkey
    );

    [RelayCommand]
    private void SelectFileManager()
    {
        var fileManagerChangeWindow = new SelectFileManagerWindow();
        fileManagerChangeWindow.ShowDialog();
    }

    [RelayCommand]
    private void SelectBrowser()
    {
        var browserWindow = new SelectBrowserWindow();
        browserWindow.ShowDialog();
    }
}
