using System.Collections.Generic;
using Flow.Launcher.Plugin.WindowsSettings.Classes;
using Flow.Launcher.Plugin.WindowsSettings.Helper;

namespace Flow.Launcher.Plugin.WindowsSettings
{
    public sealed class Main : IPlugin
    {
        private const string CONTROL_PANEL_ICON = "Images/ControlPanel_Small.png";
        private const string WINDOWS_SETTING_ICON = "Images/WindowsSettings.light.png";

        private IEnumerable<WindowsSetting>? _settingsList;

        internal static PluginInitContext Context { get; private set; } = null!;

        public void Init(PluginInitContext context)
        {
            Context = context;
            _settingsList = JsonSettingsListHelper.ReadAllPossibleSettings();
            _settingsList = UnsupportedSettingsHelper.FilterByBuild(Context.API, _settingsList);
            _settingsList = TranslationHelper.TranslateAllSettings(Context.API, _settingsList);
        }

        public List<Result> Query(Query query)
        {
            var newList = ResultHelper.GetResultList(Context.API, _settingsList!, query, WINDOWS_SETTING_ICON, CONTROL_PANEL_ICON);
            return newList;
        }
    }
}
