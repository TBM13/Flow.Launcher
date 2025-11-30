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
using Flow.Launcher.Plugin.WindowsSettings.Classes;
using Flow.Launcher.Plugin.WindowsSettings.Properties;

namespace Flow.Launcher.Plugin.WindowsSettings.Helper
{
    internal static class TranslationHelper
    {
        internal static void TranslateAllSettings(IEnumerable<WindowsSetting> settingsList)
        {
            foreach (var setting in settingsList)
            {
                setting.Name = Resources.ResourceManager.GetString(setting.Name) ?? setting.Name;
                setting.DisplayType = Resources.ResourceManager.GetString(setting.DisplayType) ?? setting.DisplayType;

                if (setting.Areas is not null && setting.Areas.Any())
                {
                    List<string> translatedAreas = [];
                    foreach (var area in setting.Areas)
                    {
                        if (string.IsNullOrWhiteSpace(area))
                            continue;

                        var translatedArea = Resources.ResourceManager.GetString(area);
                        translatedAreas.Add(translatedArea ?? area);
                    }

                    setting.Areas = translatedAreas;
                }

                if (setting.AltNames is not null && setting.AltNames.Any())
                {
                    List<string> translatedAltNames = [];
                    foreach (var altName in setting.AltNames)
                    {
                        if (string.IsNullOrWhiteSpace(altName))
                            continue;

                        var translatedAltName = Resources.ResourceManager.GetString(altName);
                        translatedAltNames.Add(translatedAltName ?? altName);
                    }

                    setting.AltNames = translatedAltNames;
                }

                if (!string.IsNullOrWhiteSpace(setting.Note))
                {
                    var note = Resources.ResourceManager.GetString(setting.Note);
                    setting.Note = note ?? setting.Note;
                }
            }
        }
    }
}
