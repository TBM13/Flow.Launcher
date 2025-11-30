using System.Collections.Generic;
using Flow.Launcher.Plugin.WindowsSettings.Classes;
using Flow.Launcher.Plugin.WindowsSettings.Helper;

namespace Flow.Launcher.Plugin.WindowsSettings
{
    public sealed class Main : IPlugin
    {
        private IEnumerable<WindowsSetting>? _settingsList;

        internal static PluginInitContext Context { get; private set; } = null!;

        public void Init(PluginInitContext context)
        {
            Context = context;
            _settingsList = JsonSettingsListHelper.ReadAllPossibleSettings();
            _settingsList = UnsupportedSettingsHelper.FilterByBuild(Context.API, _settingsList);
            TranslationHelper.TranslateAllSettings(_settingsList);
        }

        public List<Result> Query(Query query)
        {
            var newList = ResultHelper.GetResultList(Context.API, _settingsList!, query);
            return newList;
        }
    }
}
