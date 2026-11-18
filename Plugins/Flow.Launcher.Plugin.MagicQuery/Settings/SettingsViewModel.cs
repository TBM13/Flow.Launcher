using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.PluginSDK.UI;

namespace Flow.Launcher.Plugin.MagicQuery.Settings;

public partial class SettingsViewModel(Settings settings) : ObservableObject
{
    public Settings Settings { get; } = settings;

    public static IReadOnlyList<LocalizedEnumItem<MatchType>> MatchTypeItems => EnumLocalization<MatchType>.Items;

    [RelayCommand]
    private void AddRule()
    {
        Settings.Rules.Add(new MagicQueryRule());
    }

    [RelayCommand]
    private void RemoveRule(MagicQueryRule? rule)
    {
        if (rule is not null)
            Settings.Rules.Remove(rule);
    }

    [RelayCommand]
    private void MoveRuleUp(MagicQueryRule? rule)
    {
        if (rule is null)
            return;

        int index = Settings.Rules.IndexOf(rule);
        if (index > 0)
            Settings.Rules.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveRuleDown(MagicQueryRule? rule)
    {
        if (rule is null)
            return;

        int index = Settings.Rules.IndexOf(rule);
        if (index >= 0 && index < Settings.Rules.Count - 1)
            Settings.Rules.Move(index, index + 1);
    }
}
