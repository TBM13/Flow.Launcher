using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Plugin.WindowsSettings.Classes;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.WindowsSettings.Helper
{
    internal static class UnsupportedSettingsHelper
    {
        private const string KEY_PATH = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion";
        private const string KEY_BUILD = "CurrentBuild";
        private const string KEY_BUILD_NUMBER = "CurrentBuildNumber";

        /// <summary>
        /// Removes all the <see cref="WindowsSetting"/>(s) not available on the current Windows build.
        /// </summary>
        internal static IEnumerable<WindowsSetting> FilterByBuild(IPublicAPI api, in IEnumerable<WindowsSetting>? settingsList)
        {
            if (settingsList is null)
                return [];

            var currentBuild = GetNumericRegistryValue(KEY_PATH, KEY_BUILD);
            var currentBuildNumber = GetNumericRegistryValue(KEY_PATH, KEY_BUILD_NUMBER);
            if (currentBuild != currentBuildNumber)
            {
                var usedValueName = currentBuild != uint.MinValue ? KEY_BUILD : KEY_BUILD_NUMBER;
                var warningMessage =
                    $"Detecting the Windows version in registry ({KEY_PATH}) leads to an inconclusive"
                    + $" result ({KEY_BUILD}={currentBuild}, {KEY_BUILD_NUMBER}={currentBuildNumber})!"
                    + $" For resolving the conflict we use the value of '{usedValueName}'.";

                api.LogWarn(typeof(UnsupportedSettingsHelper).FullName, warningMessage);
            }

            var currentWindowsBuild = currentBuild != uint.MinValue
                ? currentBuild
                : currentBuildNumber;

            var filteredSettingsList = settingsList.Where(found
                => (found.DeprecatedInBuild == null || currentWindowsBuild < found.DeprecatedInBuild)
                && (found.IntroducedInBuild == null || currentWindowsBuild >= found.IntroducedInBuild));

            return filteredSettingsList.OrderBy(found => found.Name);
        }

        /// <summary>
        /// Return an unsigned numeric value from given registry value name inside the given registry key.
        /// </summary>
        /// <param name="registryKey">The registry key.</param>
        /// <param name="valueName">The name of the registry value.</param>
        /// <returns>A registry value or <see cref="uint.MinValue"/> on error.</returns>
        private static uint GetNumericRegistryValue(in string registryKey, in string valueName)
        {
            object? registryValueData = Registry.GetValue(registryKey, valueName, uint.MinValue);
            return uint.TryParse(registryValueData as string, out var buildNumber)
                ? buildNumber
                : uint.MinValue;
        }
    }
}
