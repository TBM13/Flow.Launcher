using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flow.Launcher.Plugin.MagicQuery.Settings;

public partial class Settings : ObservableObject
{
    public ObservableCollection<MagicQueryRule> Rules { get; set; } = [];
}
