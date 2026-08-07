using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.Settings.Pages;

namespace Flow.Launcher.Settings;

public partial class SettingViewModel(ISettingsAPI settings,
    SettingsGeneralViewModel generalVm, SettingsPluginsViewModel pluginVm,
    SettingsThemeViewModel themeVm, SettingsHotkeyViewModel hotkeyVm,
    SettingsAboutViewModel aboutVm) : ObservableObject
{
    public ISettingsAPI Settings { get; } = settings;

    [ObservableProperty]
    public partial BaseSettingsPageViewModel CurrentPage { get; set; } = generalVm;

    public IReadOnlyList<BaseSettingsPageViewModel> Pages { get; } =
    [
        generalVm,
        pluginVm,
        themeVm,
        hotkeyVm,
        aboutVm
    ];

    public void SaveAllSettings()
    {
        Settings.Save();
        IPublicAPI.Instance.SavePluginSettings();
    }
}
