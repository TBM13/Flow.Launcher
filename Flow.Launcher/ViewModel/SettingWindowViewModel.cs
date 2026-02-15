using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Core;

namespace Flow.Launcher.ViewModel;

public partial class SettingWindowViewModel(Settings settings) : ObservableObject
{
    private readonly Settings _settings = settings;

    public bool SetPageType(Type pageType)
    {
        if (PageType == pageType)
            return false;

        PageType = pageType;
        return true;
    }

    [ObservableProperty]
    public partial Type PageType { get; set; }

    public double SettingWindowWidth
    {
        get => _settings.SettingWindowWidth;
        set => _settings.SettingWindowWidth = value;
    }

    public double SettingWindowHeight
    {
        get => _settings.SettingWindowHeight;
        set => _settings.SettingWindowHeight = value;
    }

    public double? SettingWindowTop
    {
        get => _settings.SettingWindowTop;
        set => _settings.SettingWindowTop = value;
    }

    public double? SettingWindowLeft
    {
        get => _settings.SettingWindowLeft;
        set => _settings.SettingWindowLeft = value;
    }
}
