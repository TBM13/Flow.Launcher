using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.Plugin.Explorer.Views;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Plugin.SharedModels;
using Peter;
using Path = System.IO.Path;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public static class ResultManager
    {
        private static readonly string ClassName = nameof(ResultManager);

        private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB" };
        private static PluginInitContext Context;
        private static Settings Settings { get; set; }

        public static void Init(PluginInitContext context, Settings settings)
        {
            Context = context;
            Settings = settings;
        }

        public static string GetAutoCompleteText(Query query, string path, ResultType resultType)
        {
            if (resultType == ResultType.File)
                return $"{query.ActionKeyword} {path}";

            return $"{query.ActionKeyword} {path}" + Constants.DirectorySeparator;
        }

        public static Result CreateResult(Query query, SearchResult result)
        {
            return result.Type switch
            {
                ResultType.Folder or ResultType.Volume =>
                    CreateFolderResult(Path.GetFileName(result.FullPath), result.FullPath, result.FullPath, query, result.Score),
                ResultType.File =>
                    CreateFileResult(result.FullPath, query, result.Score),
                _ => throw new ArgumentOutOfRangeException(null)
            };
        }

        internal static void ShowNativeContextMenu(string path, ResultType type)
        {
            var screenWithMouseCursor = MonitorInfo.GetCursorDisplayMonitor();
            var xOfScreenCenter = screenWithMouseCursor.WorkingArea.Left + screenWithMouseCursor.WorkingArea.Width / 2;
            var yOfScreenCenter = screenWithMouseCursor.WorkingArea.Top + screenWithMouseCursor.WorkingArea.Height / 2;
            var showPosition = new System.Drawing.Point((int)xOfScreenCenter, (int)yOfScreenCenter);

            switch (type)
            {
                case ResultType.File:
                    var fileInfo = new FileInfo[] { new(path) };
                    new ShellContextMenu().ShowContextMenu(fileInfo, showPosition);
                    break;

                case ResultType.Folder:
                    var folderInfo = new System.IO.DirectoryInfo[] { new(path) };
                    new ShellContextMenu().ShowContextMenu(folderInfo, showPosition);
                    break;
            }
        }

        internal static Result CreateFolderResult(string title, string subtitle, string path, Query query, int score = 0)
        {
            return new Result
            {
                Title = title,
                IcoPath = path,
                SubTitle = subtitle,
                AutoCompleteText = GetAutoCompleteText(query, path, ResultType.Folder),
                TitleHighlightData = Context.API.FuzzySearch(query.Search, title).MatchData,
                CopyText = path,
                Preview = new Result.PreviewInfo
                {
                    FilePath = path,
                },
                PreviewPanel = new Lazy<UserControl>(() => new PreviewPanel(Settings, path, ResultType.Folder)),
                Action = c =>
                {
                    if (c.SpecialKeyState.ToModifierKeys() == ModifierKeys.Alt)
                    {
                        ShowNativeContextMenu(path, ResultType.Folder);
                        return false;
                    }
                    // open folder
                    if (c.SpecialKeyState.ToModifierKeys() == (ModifierKeys.Control | ModifierKeys.Shift))
                    {
                        try
                        {
                            OpenFolder(path);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Context.API.ShowMsgBox(ex.Message, Localize.plugin_explorer_opendir_error());
                            return false;
                        }
                    }
                    // Open containing folder
                    if (c.SpecialKeyState.ToModifierKeys() == ModifierKeys.Control)
                    {
                        try
                        {
                            Context.API.OpenDirectory(Path.GetDirectoryName(path), path);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Context.API.ShowMsgBox(ex.Message, Localize.plugin_explorer_opendir_error());
                            return false;
                        }
                    }

                    try
                    {
                        OpenFolder(path);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Context.API.ShowMsgBox(ex.Message, Localize.plugin_explorer_opendir_error());
                        return false;
                    }
                },
                Score = score,
                TitleToolTip = Localize.plugin_explorer_plugin_ToolTipOpenDirectory(),
                SubTitleToolTip = Settings.DisplayMoreInformationInToolTip ? GetFolderMoreInfoTooltip(path) : path,
                ContextData = new SearchResult { Type = ResultType.Folder, FullPath = path }
            };
        }

        internal static Result CreateDriveSpaceDisplayResult(string path, int score = 500)
        {
            var driveLetter = path[..1].ToUpper();
            DriveInfo drv = new DriveInfo(driveLetter);
            var freespace = ToReadableSize(drv.AvailableFreeSpace, 2);
            var totalspace = ToReadableSize(drv.TotalSize, 2);
            var subtitle = Localize.plugin_explorer_diskfreespace(freespace, totalspace);
            double usingSize = (Convert.ToDouble(drv.TotalSize) - Convert.ToDouble(drv.AvailableFreeSpace)) / Convert.ToDouble(drv.TotalSize) * 100;

            int? progressValue = Convert.ToInt32(usingSize);

            var tooltip = Settings.DisplayMoreInformationInToolTip
                ? GetVolumeMoreInfoTooltip(path, freespace, totalspace)
                : path;

            return new Result
            {
                Title = path,
                SubTitle = subtitle,
                AutoCompleteText = path,
                IcoPath = path,
                Score = score,
                Preview = new Result.PreviewInfo
                {
                    FilePath = path,
                },
                Action = _ =>
                {
                    OpenFolder(path);
                    return true;
                },
                TitleToolTip = tooltip,
                SubTitleToolTip = tooltip,
                ContextData = new SearchResult { Type = ResultType.Volume, FullPath = path }
            };
        }

        internal static string ToReadableSize(long sizeOnDrive, int pi)
        {
            var unitIndex = 0;
            double readableSize = sizeOnDrive;

            while (readableSize > 1024.0 && unitIndex < SizeUnits.Length - 1)
            {
                readableSize /= 1024.0;
                unitIndex++;
            }

            var unit = SizeUnits[unitIndex] ?? "";

            var returnStr = $"{Convert.ToInt32(readableSize)} {unit}";
            if (unitIndex != 0)
            {
                returnStr = pi switch
                {
                    1 => $"{readableSize:F1} {unit}",
                    2 => $"{readableSize:F2} {unit}",
                    3 => $"{readableSize:F3} {unit}",
                    _ => $"{Convert.ToInt32(readableSize)} {unit}"
                };
            }

            return returnStr;
        }

        internal static Result CreateOpenCurrentFolderResult(string path)
        {
            // Path passed from PathSearchAsync ends with Constants.DirectorySeparator ('\'), need to remove the separator
            // so it's consistent with folder results returned by index search which does not end with one
            var folderPath = path.TrimEnd(Constants.DirectorySeparator);

            return new Result
            {
                Title = Localize.plugin_explorer_openresultfolder(),
                SubTitle = Localize.plugin_explorer_openresultfolder_subtitle(),
                AutoCompleteText = folderPath,
                IcoPath = folderPath,
                Score = 500,
                CopyText = folderPath,
                Action = c =>
                {
                    if (c.SpecialKeyState.ToModifierKeys() == ModifierKeys.Alt)
                    {
                        ShowNativeContextMenu(folderPath, ResultType.Folder);
                        return false;
                    }
                    OpenFolder(folderPath);
                    return true;
                },
                ContextData = new SearchResult { Type = ResultType.Folder, FullPath = folderPath }
            };
        }

        internal static Result CreateFileResult(string filePath, Query query, int score = 0)
        {
            var isMedia = IsMedia(Path.GetExtension(filePath));
            var title = Path.GetFileName(filePath) ?? string.Empty;
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

            /* Preview Detail */

            var result = new Result
            {
                Title = title,
                SubTitle = directory,
                IcoPath = filePath,
                Preview = new Result.PreviewInfo
                {
                    IsMedia = isMedia,
                    PreviewImagePath = isMedia ? filePath : null,
                    FilePath = filePath,
                },
                AutoCompleteText = GetAutoCompleteText(query, filePath, ResultType.File),
                TitleHighlightData = Context.API.FuzzySearch(query.Search, title).MatchData,
                Score = score,
                CopyText = filePath,
                PreviewPanel = new Lazy<UserControl>(() => new PreviewPanel(Settings, filePath, ResultType.File)),
                Action = c =>
                {
                    if (c.SpecialKeyState.ToModifierKeys() == ModifierKeys.Alt)
                    {
                        ShowNativeContextMenu(filePath, ResultType.File);
                        return false;
                    }
                    try
                    {
                        if (c.SpecialKeyState.ToModifierKeys() == (ModifierKeys.Control | ModifierKeys.Shift))
                        {
                            OpenFile(filePath, Settings.UseLocationAsWorkingDir ? directory : string.Empty, true);
                        }
                        else if (c.SpecialKeyState.ToModifierKeys() == ModifierKeys.Control)
                        {
                            OpenFolder(filePath, filePath);
                        }
                        else
                        {
                            OpenFile(filePath, Settings.UseLocationAsWorkingDir ? directory : string.Empty);
                        }
                    }
                    catch (Exception ex)
                    {
                        Context.API.ShowMsgBox(ex.Message, Localize.plugin_explorer_openfile_error());
                    }

                    return true;
                },
                TitleToolTip = Localize.plugin_explorer_plugin_ToolTipOpenContainingFolder(),
                SubTitleToolTip = Settings.DisplayMoreInformationInToolTip ? GetFileMoreInfoTooltip(filePath) : filePath,
                ContextData = new SearchResult { Type = ResultType.File, FullPath = filePath }
            };
            return result;
        }

        private static bool IsMedia(string extension)
        {
            if (string.IsNullOrEmpty(extension)) { return false; }

            return MediaExtensions.Contains(extension.ToLowerInvariant());
        }

        private static void OpenFile(string filePath, string workingDir = "", bool asAdmin = false)
        {
            FilesFolders.OpenFile(filePath, workingDir, asAdmin, (string str) => Context.API.ShowMsgBox(str));
        }

        private static void OpenFolder(string folderPath, string fileNameOrFilePath = null)
        {
            Context.API.OpenDirectory(folderPath, fileNameOrFilePath);
        }

        private static string GetFileMoreInfoTooltip(string filePath)
        {
            try
            {
                var fileSize = PreviewPanel.GetFileSize(filePath);
                var fileCreatedAt = PreviewPanel.GetFileCreatedAt(filePath, Settings.PreviewPanelDateFormat, Settings.PreviewPanelTimeFormat, Settings.ShowFileAgeInPreviewPanel);
                var fileModifiedAt = PreviewPanel.GetFileLastModifiedAt(filePath, Settings.PreviewPanelDateFormat, Settings.PreviewPanelTimeFormat, Settings.ShowFileAgeInPreviewPanel);
                return Localize.plugin_explorer_plugin_tooltip_more_info(filePath, fileSize, fileCreatedAt, fileModifiedAt, Environment.NewLine);
            }
            catch (Exception e)
            {
                Context.API.LogException(ClassName, $"Failed to load tooltip for {filePath}", e);
                return filePath;
            }
        }

        private static string GetFolderMoreInfoTooltip(string folderPath)
        {
            try
            {
                var folderSize = PreviewPanel.GetFolderSize(folderPath);
                var folderCreatedAt = PreviewPanel.GetFolderCreatedAt(folderPath, Settings.PreviewPanelDateFormat, Settings.PreviewPanelTimeFormat, Settings.ShowFileAgeInPreviewPanel);
                var folderModifiedAt = PreviewPanel.GetFolderLastModifiedAt(folderPath, Settings.PreviewPanelDateFormat, Settings.PreviewPanelTimeFormat, Settings.ShowFileAgeInPreviewPanel);
                return Localize.plugin_explorer_plugin_tooltip_more_info(folderPath, folderSize, folderCreatedAt, folderModifiedAt, Environment.NewLine);
            }
            catch (Exception e)
            {
                Context.API.LogException(ClassName, $"Failed to load tooltip for {folderPath}", e);
                return folderPath;
            }
        }

        private static string GetVolumeMoreInfoTooltip(string volumePath, string freespace, string totalspace)
        {
            return Localize.plugin_explorer_plugin_tooltip_more_info_volume(volumePath, freespace, totalspace, Environment.NewLine);
        }

        private static readonly string[] MediaExtensions =
        {
            ".jpg", ".png", ".avi", ".mkv", ".bmp", ".gif", ".wmv", ".mp3", ".flac", ".mp4",
            ".m4a", ".m4v", ".heic", ".mov", ".flv", ".webm"
        };
    }

    public enum ResultType
    {
        Volume,
        Folder,
        File
    }
}
