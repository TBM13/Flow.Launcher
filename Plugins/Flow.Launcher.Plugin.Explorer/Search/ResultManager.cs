using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.Interop.Files;
using Flow.Launcher.Interop.Programs;
using Flow.Launcher.Interop.Shell;
using Flow.Launcher.Plugin.Explorer.Views;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Hotkeys;

namespace Flow.Launcher.Plugin.Explorer.Search;

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
                path = ShortcutHelper.RetrieveTargetPath(path);
                if (!path.EndsWith(Path.DirectorySeparatorChar) && Directory.Exists(path))
                    path += Path.DirectorySeparatorChar;
            }
        }
        else if (!path.EndsWith(Path.DirectorySeparatorChar))
            path += Path.DirectorySeparatorChar;

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
            PreviewPanel = new Lazy<UserControl>(() => new PreviewPanel(Main.Settings, path, ResultType.Folder)),
            Action = c =>
            {
                IPressedKeys keys = c.PressedKeys;
                if (keys.AltPressed)
                {
                    ShowNativeContextMenu(path, ResultType.Folder, c.ResultPosition);
                    return false;
                }
                // Open containing folder
                else if (keys.IsModifierPressed(ModifierKeys.Control))
                {
                    OpenFolderAndSelectFile(path);
                    return true;
                }

                try
                {
                    OpenFolder(path);
                    return true;
                }
                catch (Exception ex)
                {
                    Main.Context.Logger.LogError(ex, $"Failed to open dir {path}");
                    MessageBox.Show(ex.Message, "Could not open folder");
                    return false;
                }
            },
            Score = score,
            TitleToolTip = "Ctrl + Enter to open the directory",
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
        var subtitle = $"{freespace} free of {totalspace}";
        double usingSize = (Convert.ToDouble(drv.TotalSize) - Convert.ToDouble(drv.AvailableFreeSpace)) / Convert.ToDouble(drv.TotalSize) * 100;

        return new Result
        {
            Title = path.ToUpper(),
            SubTitle = subtitle,
            AutoCompleteText = GetAutoCompleteText(query, path, ResultType.Volume),
            IcoPath = path,
            Score = score,
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
        var folderPath = path.TrimEnd(Path.DirectorySeparatorChar);

        return new Result
        {
            Title = "Open in Default File Manager",
            SubTitle = "Use '*' as a search wildcard, '>' to include subdirectories.",
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
                // Open containing folder
                else if (c.PressedKeys.IsModifierPressed(ModifierKeys.Control))
                {
                    OpenFolderAndSelectFile(folderPath);
                    return true;
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
        var title = Path.GetFileName(filePath) ?? string.Empty;
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

        var result = new Result
        {
            Title = title,
            SubTitle =
                isRecursiveSearch ? filePath :
                isShellLink ? ShortcutHelper.RetrieveTargetPath(filePath) :
                string.Empty,
            IcoPath = filePath,
            AutoCompleteText = GetAutoCompleteText(query, filePath, ResultType.File),
            Score = score,
            CopyText = filePath,
            PreviewPanel = new Lazy<UserControl>(() => new PreviewPanel(Main.Settings, filePath, ResultType.File)),
            Action = c =>
            {
                IPressedKeys keys = c.PressedKeys;
                if (keys.AltPressed)
                {
                    ShowNativeContextMenu(filePath, ResultType.File, c.ResultPosition);
                    return false;
                }
                if (keys.OnlyModifiersPressed(ModifierKeys.Shift))
                {
                    OpenFile(filePath, Main.Settings.UseLocationAsWorkingDir ? directory : string.Empty, true);
                }
                else if (keys.IsModifierPressed(ModifierKeys.Control))
                {
                    OpenFolderAndSelectFile(filePath);
                }
                else
                {
                    OpenFile(filePath, Main.Settings.UseLocationAsWorkingDir ? directory : string.Empty);
                }

                return true;
            },
            TitleToolTip = "Ctrl + Enter to open the containing folder",
            SubTitleToolTip = filePath,
            ContextData = new SearchResult { Type = ResultType.File, FullPath = filePath }
        };

        return result;
    }

    private static void OpenFile(string filePath, string workingDir = "", bool asAdmin = false)
    {
        string verb = asAdmin ? "runas" : string.Empty;
        try
        {
            // useShellExecute is needed to open all types of files (e.g. .sln)
            ProcessHelper.StartProcess(
                filePath, workingDirectory: workingDir, useShellExecute: true, verb: verb);
        }
        catch (Exception e)
        {
            Main.Context.Logger.LogError(e, $"Failed to open file {filePath}");
            Main.Context.API.ShowMsgError("Could not open file");
        }
    }

    private static void OpenFolder(string folderPath)
    {
        try
        {
            FileExplorerHelper.OpenFolder(folderPath);

        }
        catch (Exception e)
        {
            Main.Context.Logger.LogError(e, $"Failed to open folder {folderPath}");
            Main.Context.API.ShowMsgError("Could not open folder");
        }
    }

    private static void OpenFolderAndSelectFile(string filePath)
    {
        try
        {
            FileExplorerHelper.OpenFolderAndSelectFile(filePath);
        }
        catch (Exception e)
        {
            Main.Context.Logger.LogError(e, $"Failed to open folder and select file {filePath}");
            Main.Context.API.ShowMsgError("Could not open folder");
        }
    }
}
