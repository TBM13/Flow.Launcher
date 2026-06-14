using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Core.Settings;

namespace Flow.Launcher.ViewModel;

public partial class SettingWindowViewModel(ISettingsAPI settings) : ObservableObject
{
    public ISettingsAPI Settings { get; } = settings;

    public bool SetPageType(Type? pageType)
    {
        if (PageType == pageType)
            return false;

        PageType = pageType;
        return true;
    }

    [ObservableProperty]
    public partial Type? PageType { get; set; }
}
