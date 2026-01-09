/*The MIT License

Copyright (c) Microsoft Corporation. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin.WindowsSettings.Classes;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.WindowsSettings.Helper
{
    internal static class UnsupportedSettingsHelper
    {
        private const string KEY_PATH = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion";
        private const string KEY_BUILD = "CurrentBuild";
        private const string KEY_BUILD_NUMBER = "CurrentBuildNumber";

        private static readonly string CLASS = typeof(UnsupportedSettingsHelper).FullName ?? nameof(UnsupportedSettingsHelper);

        /// <summary>
        /// Removes all the <see cref="WindowsSetting"/>(s) not available on the current Windows build.
        /// </summary>
        internal static IEnumerable<WindowsSetting> FilterByBuild(IPublicAPI api, in IEnumerable<WindowsSetting> settingsList)
        {
            var currentBuild = GetNumericRegistryValue(KEY_PATH, KEY_BUILD);
            var currentBuildNumber = GetNumericRegistryValue(KEY_PATH, KEY_BUILD_NUMBER);

            if (currentBuild != currentBuildNumber)
            {
                var usedValueName = currentBuild != uint.MinValue ? KEY_BUILD : KEY_BUILD_NUMBER;
                var warningMessage =
                    $"Detecting the Windows version in registry ({KEY_PATH}) leads to an inconclusive"
                    + $" result ({KEY_BUILD}={currentBuild}, {KEY_BUILD_NUMBER}={currentBuildNumber})!"
                    + $" For resolving the conflict we use the value of '{usedValueName}'.";

                api.LogWarn(CLASS, warningMessage);
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
