using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Plugin.WindowsSettings.Classes;
using Flow.Launcher.Plugin.WindowsSettings.Properties;

namespace Flow.Launcher.Plugin.WindowsSettings.Helper
{
    internal static class TranslationHelper
    {
        /// <summary>
        /// Translate all settings of the given list with <see cref="WindowsSetting"/>.
        /// </summary>
        /// <param name="settingsList">The list that contains <see cref="WindowsSetting"/> to translate.</param>
        internal static IEnumerable<WindowsSetting> TranslateAllSettings(IPublicAPI api, in IEnumerable<WindowsSetting>? settingsList)
        {
            if (settingsList is null)
                return [];

            var translatedSettings = new List<WindowsSetting>();
            foreach (var settings in settingsList)
            {
                var area = Resources.ResourceManager.GetString($"Area{settings.Area}");
                var name = Resources.ResourceManager.GetString(settings.Name);
                var type = Resources.ResourceManager.GetString(settings.Type);

                if (string.IsNullOrEmpty(area))
                {
                    api.LogWarn(typeof(TranslationHelper).FullName, $"Resource string for [Area{settings.Area}] not found");
                }
                if (string.IsNullOrEmpty(name))
                {
                    api.LogWarn(typeof(TranslationHelper).FullName, $"Resource string for [{settings.Name}] not found");
                }
                if (string.IsNullOrEmpty(type))
                {
                    api.LogWarn(typeof(TranslationHelper).FullName, $"Resource string for [{settings.Type}] not found");
                }

                if (!string.IsNullOrEmpty(settings.Note))
                {
                    var note = Resources.ResourceManager.GetString(settings.Note);
                    settings.Note = note ?? settings.Note ?? string.Empty;
                }

                List<string>? translatedAltNames = null;
                if (settings.AltNames is not null && settings.AltNames.Any())
                {
                    translatedAltNames = [];
                    foreach (var altName in settings.AltNames)
                    {
                        if (string.IsNullOrWhiteSpace(altName))
                            continue;

                        var translatedAltName = Resources.ResourceManager.GetString(altName);
                        translatedAltNames.Add(translatedAltName ?? altName);
                    }

                }
                var translatedSetting = settings with
                {
                    Area = area ?? settings.Area,
                    Name = name ?? settings.Name,
                    DisplayType = type ?? settings.Type,
                    AltNames = translatedAltNames
                };

                translatedSettings.Add(translatedSetting);
            }
            return translatedSettings;
        }
    }
}
