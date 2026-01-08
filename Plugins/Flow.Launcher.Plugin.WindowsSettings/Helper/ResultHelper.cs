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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Flow.Launcher.Plugin.WindowsSettings.Classes;
using Flow.Launcher.Plugin.WindowsSettings.Properties;

namespace Flow.Launcher.Plugin.WindowsSettings.Helper
{
    internal static class ResultHelper
    {
        /// <summary>
        /// Score penalty given to results where the query matched with the area/alternative names but not the setting's name.
        /// </summary>
        private const int NON_NAME_MATCH_PENALTY = -25;
        /// <summary>
        /// Score penalty given to results of type <see cref="WindowsSettingType.AppMMC"/>.
        /// </summary>
        private const int MMC_PENALTY = -25;
        /// <summary>
        /// Score penalty given to results of type <see cref="WindowsSettingType.AppControlPanel"/>.
        /// </summary>
        private const int CONTROL_PANEL_PENALTY = -25;
        /// <summary>
        /// Score penalty given to results without a glyph. We assume results with glyphs are more common and thus more important.
        /// </summary>
        private const int NO_GLYPH_PENALTY = -10;

        private static readonly string WINDOWS_SETTINGS_ICON_PATH = PluginMetadataDefinition.Metadata.IcoPath;
        private static readonly string CONTROL_PANEL_ICON_PATH = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\control.exe");
        private static readonly string MMC_ICON_PATH = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\mmc.exe");

        private static List<Result> GetDefaultResults(IPublicAPI api, in IEnumerable<WindowsSetting> list)
        {
            return [.. list.Select(entry =>
            {
                var result = NewSettingResult(api, 100, entry);
                AddOptionalToolTip(entry, result);
                return result;
            })];
        }

        /// <summary>
        /// Returns a list with <see cref="Result"/>(s), based on the given list.
        /// </summary>
        /// <param name="list">The original result list to convert.</param>
        /// <param name="query">Query for specific result List</param>
        /// <param name="windowsSettingIconPath">The path to the icon of each entry.</param>
        internal static List<Result> GetResultList(IPublicAPI api, in IEnumerable<WindowsSetting> list, Query query)
        {
            if (string.IsNullOrWhiteSpace(query.Search))
            {
                return GetDefaultResults(api, list);
            }

            var resultList = new List<Result>();
            foreach (var entry in list)
            {
                Result? result;

                var nameMatch = api.FuzzySearch(query.Search, entry.Name);
                if (nameMatch.IsSearchPrecisionScoreMet())
                {
                    var settingResult = NewSettingResult(api, nameMatch.Score, entry);
                    result = settingResult;
                }
                else
                {
                    var areaMatch = api.FuzzySearch(query.Search, entry.JoinedAreaPath);
                    if (areaMatch.IsSearchPrecisionScoreMet())
                    {
                        result = NewSettingResult(api, areaMatch.Score, entry);
                    }
                    else
                    {
                        result = entry.AltNames?
                            .Select(altName => api.FuzzySearch(query.Search, altName))
                            .Where(match => match.IsSearchPrecisionScoreMet())
                            .Select(altNameMatch => NewSettingResult(api, altNameMatch.Score, entry))
                            .FirstOrDefault();
                    }

                    result?.Score += NON_NAME_MATCH_PENALTY;
                }

                if (result is null)
                    continue;

                AddOptionalToolTip(entry, result);
                resultList.Add(result);
            }

            return resultList;
        }

        private static Result NewSettingResult(IPublicAPI api, int score, WindowsSetting entry)
        {
            Result res = new()
            {
                Action = _ => DoOpenSettingsAction(api, entry),
                IcoPath = entry.Type switch
                {
                    WindowsSettingType.AppSettingsApp => WINDOWS_SETTINGS_ICON_PATH,
                    WindowsSettingType.AppMMC => MMC_ICON_PATH,
                    _ => CONTROL_PANEL_ICON_PATH
                },
                Glyph = entry.IconGlyph,
                SubTitle = entry.JoinedFullSettingsPath,
                Title = entry.Name,
                ContextData = entry,
                Score = entry.Type switch
                {
                    WindowsSettingType.AppControlPanel => score + CONTROL_PANEL_PENALTY,
                    WindowsSettingType.AppMMC => score + MMC_PENALTY,
                    _ => score,
                },
            };

            if (entry.IconGlyph is null)
                res.Score += NO_GLYPH_PENALTY;

            return res;
        }

        private static void AddOptionalToolTip(WindowsSetting entry, Result result)
        {
            var toolTipText = new StringBuilder();

            toolTipText.AppendLine($"{Resources.Application}: {entry.DisplayType}");
            toolTipText.AppendLine($"{Resources.Area}: {entry.JoinedAreaPath}");

            if (entry.AltNames != null && entry.AltNames.Any())
            {
                var altList = entry.AltNames.Aggregate((current, next) => $"{current}, {next}");

                toolTipText.AppendLine($"{Resources.AlternativeName}: {altList}");
            }

            toolTipText.Append($"{Resources.Command}: {entry.Command}");

            if (!string.IsNullOrEmpty(entry.Note))
            {
                toolTipText.AppendLine(string.Empty);
                toolTipText.AppendLine(string.Empty);
                toolTipText.Append($"{Resources.Note}: {entry.Note}");
            }

            result.TitleToolTip = toolTipText.ToString();
            result.SubTitleToolTip = result.TitleToolTip;
        }

        /// <summary>
        /// Open the settings page of the given <see cref="WindowsSetting"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the page could be opened, otherwise <see langword="false"/>.</returns>
        private static bool DoOpenSettingsAction(IPublicAPI api, WindowsSetting entry)
        {
            ProcessStartInfo processStartInfo;

            var command = entry.Command;
            command = Environment.ExpandEnvironmentVariables(command);
            if (command.Contains(' '))
            {
                var commandSplit = command.Split(' ');
                var file = commandSplit.First();
                var arguments = command[file.Length..].TrimStart();

                processStartInfo = new ProcessStartInfo(file, arguments)
                {
                    UseShellExecute = false,
                };
            }
            else
            {
                processStartInfo = new ProcessStartInfo(command)
                {
                    UseShellExecute = true,
                };
            }

            try
            {
                Process.Start(processStartInfo);
                return true;
            }
            catch (Win32Exception)
            {
                try
                {
                    processStartInfo.UseShellExecute = true;
                    processStartInfo.Verb = "runas";
                    Process.Start(processStartInfo);
                    return true;
                }
                catch (Exception exception)
                {
                    api.ShowMsgError("Failed to open settings app", exception.ToString());
                    return false;
                }
            }
            catch (Exception exception)
            {
                api.ShowMsgError("Failed to open settings app", exception.ToString());
                return false;
            }
        }
    }
}
