using CommunityToolkit.Mvvm.ComponentModel;

namespace Flow.Launcher.Settings.Pages;

public abstract class BaseSettingsPageViewModel : ObservableObject
{
    public abstract string Title { get; }
    public abstract string IconPath { get; }
}
