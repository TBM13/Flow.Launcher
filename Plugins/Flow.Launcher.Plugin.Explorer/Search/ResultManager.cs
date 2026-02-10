using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.Infrastructure.Helpers;
using Flow.Launcher.Infrastructure.Results;
using Flow.Launcher.Plugin.Explorer.Views;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public static class ResultManager
    {
        private static readonly string[] SizeUnits = ["B", "KB", "MB", "GB", "TB"];

        public static string GetAutoCompleteText(Query query, string path, ResultType resultType)
        {
            string actionKeyword = string.IsNullOrEmpty(query.ActionKeyword)
                ? string.Empty
                : query.ActionKeyword + ' ';

            if (resultType == ResultType.File)
            {
                if (path.EndsWith(".lnk", StringComparison.InvariantCultureIgnoreCase))
                {
                    path = ShellLinkHelper.RetrieveTargetPath(path);
                    if (!path.EndsWith(Constants.DirectorySeparator) && Directory.Exists(path))
                        path += Constants.DirectorySeparator;
                }
            }
            else if (!path.EndsWith(Constants.DirectorySeparator))
                path += Constants.DirectorySeparator;

            return actionKeyword + path;
        }

        public static Result CreateResult(Query query, SearchResult result, bool isRecursive)
        {
            return result.Type switch
            {
                ResultType.Folder or ResultType.Volume =>
                    CreateFolderResult(Path.GetFileName(result.FullPath), isRecursive ? result.FullPath : string.Empty, result.FullPath, query, result.Score),
                ResultType.File =>
                    CreateFileResult(result.FullPath, query, isRecursive, result.Score),
                _ => throw new ArgumentOutOfRangeException(null)
            };
        }

        internal static void ShowNativeContextMenu(string path, ResultType type, Point showPosition)
        {
            System.Drawing.Point point = new((int)showPosition.X, (int)showPosition.Y);

            switch (type)
            {
                case ResultType.File:
                    var fileInfo = new FileInfo[] { new(path) };
                    new ShellContextMenu().ShowContextMenu(fileInfo, point);
                    break;

                case ResultType.Folder:
                    var folderInfo = new System.IO.DirectoryInfo[] { new(path) };
                    new ShellContextMenu().ShowContextMenu(folderInfo, point);
                    break;

                case ResultType.Volume:
                    var driveInfo = new DriveInfo[] { new(path) };
                    new ShellContextMenu().ShowContextMenu(driveInfo, point);
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
                CopyText = path,
                Preview = new Result.PreviewInfo
                {
                    FilePath = path,
                },
                PreviewPanel = new Lazy<UserControl>(() => new PreviewPanel(Main.Settings, path, ResultType.Folder)),
                Action = c =>
                {
                    var keys = c.PressedKeys;
                    if (keys.AltPressed)
                    {
                        ShowNativeContextMenu(path, ResultType.Folder, c.ResultPosition);
                        return false;
                    }
                    // open folder
                    if (keys == (ModifierKeys.Control | ModifierKeys.Shift))
                    {
                        try
                        {
                            OpenFolder(path);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Main.Context.API.ShowMsgBox(ex.Message, Localize.Error_OpenDir);
                            return false;
                        }
                    }
                    // Open containing folder
                    if (keys == ModifierKeys.Control)
                    {
                        string? dirPath = Path.GetDirectoryName(path);
                        if (!Directory.Exists(dirPath))
                        {
                            string msg = Localize.Error_DirNotFound(dirPath ?? path);
                            Main.Context.API.ShowMsgBox(msg, Localize.Error_OpenDir);
                            return false;
                        }

                        try
                        {
                            Main.Context.API.OpenDirectory(dirPath, path);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Main.Context.API.ShowMsgBox(ex.Message, Localize.Error_OpenDir);
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
                        Main.Context.API.ShowMsgBox(ex.Message, Localize.Error_OpenDir);
                        return false;
                    }
                },
                Score = score,
                TitleToolTip = Localize.FolderResult_OpenDirectoryTooltip,
                SubTitleToolTip = path,
                ContextData = new SearchResult { Type = ResultType.Folder, FullPath = path }
            };
        }

        internal static Result CreateDriveSpaceDisplayResult(Query query, string path, int score = 500)
        {
            var driveLetter = path[..1].ToUpper();
            DriveInfo drv = new DriveInfo(driveLetter);
            var freespace = ToReadableSize(drv.AvailableFreeSpace, 2);
            var totalspace = ToReadableSize(drv.TotalSize, 2);
            var subtitle = Localize.DiskResult_FreeSpace(freespace, totalspace);
            double usingSize = (Convert.ToDouble(drv.TotalSize) - Convert.ToDouble(drv.AvailableFreeSpace)) / Convert.ToDouble(drv.TotalSize) * 100;

            return new Result
            {
                Title = path.ToUpper(),
                SubTitle = subtitle,
                AutoCompleteText = GetAutoCompleteText(query, path, ResultType.Volume),
                IcoPath = path,
                Score = score,
                Preview = new Result.PreviewInfo
                {
                    FilePath = path,
                },
                Action = c =>
                {
                    if (c.PressedKeys.AltPressed)
                    {
                        ShowNativeContextMenu(path, ResultType.Volume, c.ResultPosition);
                        return false;
                    }

                    OpenFolder(path);
                    return true;
                },
                TitleToolTip = path,
                SubTitleToolTip = path,
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

        internal static Result CreateOpenCurrentFolderResult(Query query, string path)
        {
            // Path passed from PathSearchAsync ends with Constants.DirectorySeparator ('\'), need to remove the separator
            // so it's consistent with folder results returned by index search which does not end with one
            var folderPath = path.TrimEnd(Constants.DirectorySeparator);

            return new Result
            {
                Title = Localize.FolderResult_OpenResultFolder,
                SubTitle = Localize.FolderResult_OpenResultFolder_Subtitle,
                AutoCompleteText = GetAutoCompleteText(query, path, ResultType.Folder),
                IcoPath = folderPath,
                Score = 500,
                CopyText = folderPath,
                Action = c =>
                {
                    if (c.PressedKeys.AltPressed)
                    {
                        ShowNativeContextMenu(folderPath, ResultType.Folder, c.ResultPosition);
                        return false;
                    }
                    OpenFolder(folderPath);
                    return true;
                },
                ContextData = new SearchResult { Type = ResultType.Folder, FullPath = folderPath }
            };
        }

        internal static Result CreateFileResult(string filePath, Query query, bool isRecursiveSearch, int score = 0)
        {
            var isShellLink = filePath.EndsWith(".lnk", StringComparison.InvariantCultureIgnoreCase);
            var isMedia = IsMedia(Path.GetExtension(filePath));
            var title = Path.GetFileName(filePath) ?? string.Empty;
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

            var result = new Result
            {
                Title = title,
                SubTitle =
                    isRecursiveSearch ? filePath :
                    isShellLink ? ShellLinkHelper.RetrieveTargetPath(filePath) :
                    string.Empty,
                IcoPath = filePath,
                Preview = new Result.PreviewInfo
                {
                    IsMedia = isMedia,
                    PreviewImagePath = isMedia ? filePath : null,
                    FilePath = filePath,
                },
                AutoCompleteText = GetAutoCompleteText(query, filePath, ResultType.File),
                Score = score,
                CopyText = filePath,
                PreviewPanel = new Lazy<UserControl>(() => new PreviewPanel(Main.Settings, filePath, ResultType.File)),
                Action = c =>
                {
                    var keys = c.PressedKeys;
                    if (keys.AltPressed)
                    {
                        ShowNativeContextMenu(filePath, ResultType.File, c.ResultPosition);
                        return false;
                    }
                    try
                    {
                        if (keys == (ModifierKeys.Control | ModifierKeys.Shift))
                        {
                            OpenFile(filePath, Main.Settings.UseLocationAsWorkingDir ? directory : string.Empty, true);
                        }
                        else if (keys == ModifierKeys.Control)
                        {
                            OpenFolder(filePath, filePath);
                        }
                        else
                        {
                            OpenFile(filePath, Main.Settings.UseLocationAsWorkingDir ? directory : string.Empty);
                        }
                    }
                    catch (Exception ex)
                    {
                        Main.Context.API.ShowMsgBox(ex.Message, Localize.Error_OpenFile);
                    }

                    return true;
                },
                TitleToolTip = Localize.FileResult_OpenContainingFolderTooltip,
                SubTitleToolTip = filePath,
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
            string verb = asAdmin ? "runas" : string.Empty;
            bool res = Main.Context.API.StartProcess(filePath, workingDir, arguments: string.Empty, verb: verb);
            if (!res)
            {
                Main.Context.API.ShowMsgError(Localize.Error_OpenFile);
            }
        }

        private static void OpenFolder(string folderPath, string? fileNameOrFilePath = null)
        {
            Main.Context.API.OpenDirectory(folderPath, fileNameOrFilePath);
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
