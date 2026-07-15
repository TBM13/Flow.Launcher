using Flow.Launcher.Plugin.WindowsSettings.Classes;
using Flow.Launcher.Plugin.WindowsSettings.Helper;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;

namespace Flow.Launcher.Plugin.WindowsSettings
{
    public static class PluginMetadataDefinition
    {
        public static readonly PluginMetadata Metadata = new()
        {
            ID = "5043CETYU6A748679OPA02D27D99677A",
            ActionKeywords = ["*"],
            Name = "Windows Settings",
            Description = "Search settings inside Control Panel and Settings App",
            Author = "TobiasSekan",
            Version = "1.0.0",
            IcoPath = "Images/Plugin.WindowsSettings.png",

            Plugin = new Main()
        };
    }

    public sealed class Main : IPlugin
    {
        private IEnumerable<WindowsSetting>? _settingsList;

        internal static PluginInitContext Context { get; private set; } = null!;

        public void Init(PluginInitContext context)
        {
            Context = context;
            _settingsList = JsonSettingsListHelper.ReadAllPossibleSettings();
            _settingsList = UnsupportedSettingsHelper.FilterByBuild(Context.Logger, _settingsList);
            TranslationHelper.TranslateAllSettings(_settingsList);
        }

        public List<Result> Query(Query query)
        {
            var newList = ResultHelper.GetResultList(Context.API, _settingsList!, query);
            return newList;
        }
    }
}
