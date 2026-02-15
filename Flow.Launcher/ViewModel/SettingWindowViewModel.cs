using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Core;

namespace Flow.Launcher.ViewModel;

public partial class SettingWindowViewModel(Settings settings) : ObservableObject
{
    public Settings Settings { get; } = settings;

    public bool SetPageType(Type pageType)
    {
        if (PageType == pageType)
            return false;

        PageType = pageType;
        return true;
    }

    [ObservableProperty]
    public partial Type PageType { get; set; }
}
