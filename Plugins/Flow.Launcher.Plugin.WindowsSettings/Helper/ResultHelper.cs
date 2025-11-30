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
        private static List<Result> GetDefaultResults(
            IPublicAPI api,
            in IEnumerable<WindowsSetting> list,
            string windowsSettingIconPath,
            string controlPanelIconPath)
        {
            return [.. list.Select(entry =>
            {
                var result = NewSettingResult(api, 100, entry.Type, windowsSettingIconPath, controlPanelIconPath, entry);
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
        internal static List<Result> GetResultList(
            IPublicAPI api,
            in IEnumerable<WindowsSetting> list,
            Query query,
            string windowsSettingIconPath,
            string controlPanelIconPath)
        {
            if (string.IsNullOrWhiteSpace(query.Search))
            {
                return GetDefaultResults(api, list, windowsSettingIconPath, controlPanelIconPath);
            }

            var resultList = new List<Result>();
            foreach (var entry in list)
            {
                // Adjust the score to lower the order of many irrelevant matches from area strings
                // that may only be for description.
                const int nonNameMatchScoreAdj = 10;
                Result? result;

                var nameMatch = api.FuzzySearch(query.Search, entry.Name);
                if (nameMatch.IsSearchPrecisionScoreMet())
                {
                    var settingResult = NewSettingResult(api, nameMatch.Score, entry.Type, windowsSettingIconPath, controlPanelIconPath, entry);
                    settingResult.TitleHighlightData = nameMatch.MatchData;
                    result = settingResult;
                }
                else
                {
                    var areaMatch = api.FuzzySearch(query.Search, entry.Area);
                    if (areaMatch.IsSearchPrecisionScoreMet())
                    {
                        var settingResult = NewSettingResult(api, areaMatch.Score - nonNameMatchScoreAdj, entry.Type, windowsSettingIconPath, controlPanelIconPath, entry);
                        result = settingResult;
                    }
                    else
                    {
                        result = entry.AltNames?
                            .Select(altName => api.FuzzySearch(query.Search, altName))
                            .Where(match => match.IsSearchPrecisionScoreMet())
                            .Select(altNameMatch => NewSettingResult(api, altNameMatch.Score - nonNameMatchScoreAdj, entry.Type, windowsSettingIconPath, controlPanelIconPath, entry))
                            .FirstOrDefault();
                    }

                    if (result is null && entry.Keywords is not null)
                    {
                        string[] searchKeywords = query.SearchTerms;

                        if (searchKeywords
                            .All(x => entry
                                .Keywords
                                .SelectMany(x => x)
                                .Contains(x, StringComparer.CurrentCultureIgnoreCase))
                        )
                            result = NewSettingResult(api, nonNameMatchScoreAdj, entry.Type, windowsSettingIconPath, controlPanelIconPath, entry);
                    }
                }

                if (result is null)
                    continue;

                AddOptionalToolTip(entry, result);
                resultList.Add(result);
            }

            return resultList;
        }

        private const int TaskLinkScorePenalty = 50;

        private static Result NewSettingResult(
            IPublicAPI api,
            int score, string type,
            string windowsSettingIconPath, string controlPanelIconPath,
            WindowsSetting entry) => new()
            {
                Action = _ => DoOpenSettingsAction(api, entry),
                IcoPath = type == "AppSettingsApp" ? windowsSettingIconPath : controlPanelIconPath,
                Glyph = entry.IconGlyph,
                SubTitle = GetSubtitle(entry.Area, type),
                Title = entry.Name,
                ContextData = entry,
                Score = score - (type == "TaskLink" ? TaskLinkScorePenalty : 0),
            };

        private static string GetSubtitle(string section, string entryType)
        {
            var settingType = entryType == "AppSettingsApp" ? Resources.AppSettingsApp : Resources.AppControlPanel;
            return $"{settingType} > {section}";
        }

        /// <summary>
        /// Adds a tooltip to the given <see cref="Result"/>, based on the given <see cref="WindowsSetting"/>.
        /// </summary>
        private static void AddOptionalToolTip(WindowsSetting entry, Result result)
        {
            var toolTipText = new StringBuilder();

            var settingType = entry.Type == "AppSettingsApp" ? Resources.AppSettingsApp : Resources.AppControlPanel;

            toolTipText.AppendLine($"{Resources.Application}: {settingType}");
            toolTipText.AppendLine($"{Resources.Area}: {entry.Area}");

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
