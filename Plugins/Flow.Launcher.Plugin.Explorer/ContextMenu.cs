using System.Diagnostics;
using System.IO;
using Flow.Launcher.Interop.Programs;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;

namespace Flow.Launcher.Plugin.Explorer;

internal class ContextMenu(PluginInitContext context, Settings settings) : IContextMenu
{
    private readonly PluginInitContext _context = context;
    private readonly Settings _settings = settings;

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        List<Result> contextMenus = [];

        if (selectedResult.ContextData is SearchResult record)
        {
            contextMenus.Add(new Result
            {
                Title = "Copy path",
                SubTitle = "You can open the parent dir. with CTRL + Click on the result",
                Action = _ =>
                {
                    _context.API.CopyToClipboard(record.FullPath, showDefaultNotification: false);
                    return true;
                },
                IconOrGlyph = "\ue8c8"
            });

            if (record.Type == ResultType.Folder)
                contextMenus.Add(CreateOpenWithShellResult(record));
            else if (record.Type == ResultType.File)
                contextMenus.Add(CreateOpenWithMenu(record));

            // Show windows context menu
            contextMenus.Add(new Result()
            {
                Title = "Show Windows Context Menu",
                SubTitle = "You can also open it with Alt + Click on the result",
                IconOrGlyph = "\ue700",
                Action = c =>
                {
                    ResultManager.ShowNativeContextMenu(record.FullPath, record.Type, c.ResultPosition);
                    return false;
                },
            });

            if (record.Type == ResultType.File && CanRunAsDifferentUser(record.FullPath))
                // Run as different user
                contextMenus.Add(new Result
                {
                    Title = "Run as different user",
                    SubTitle = "Run the selected file using a different user account",
                    Action = (context) =>
                    {
                        try
                        {
                            _ = Task.Run(() => ProcessHelper.StartProcess(record.FullPath, useShellExecute: true, verb: "RunAsUser"));
                        }
                        catch (FileNotFoundException e)
                        {
                            _context.API.ShowMsgError(
                                "Explorer",
                                $"File not found: {e.Message}");
                            return false;
                        }

                        return true;
                    },
                    IconOrGlyph = "\ue748",
                });
        }

        return contextMenus;
    }

    private Result CreateOpenWithShellResult(SearchResult record)
    {
        string shellPath = _settings.ShellPath;
        string name = $"Open With Shell: {Path.GetFileNameWithoutExtension(shellPath)}";

        return new Result
        {
            Title = name,
            Action = _ =>
            {
                try
                {
                    ProcessHelper.StartProcess(shellPath, workingDirectory: record.FullPath);
                    return true;
                }
                catch (Exception e)
                {
                    var message = $"Failed to open folder {record.FullPath} with Shell {Path.GetFileNameWithoutExtension(shellPath)} at {shellPath}";
                    // TODO: Make ShowMsgError log the exception
                    _context.API.ShowMsgError(message);
                    return false;
                }
            },
            IconOrGlyph = "\ue756"
        };
    }

    private static Result CreateOpenWithMenu(SearchResult record)
    {
        return new Result
        {
            Title = "Open With",
            SubTitle = "Select a program to open with",
            Action = _ =>
            {
                // No need to de-elevate since we are opening a windows menu which cannot bring security risks
                Process.Start("rundll32.exe", $"{Path.Combine(Environment.SystemDirectory, "shell32.dll")},OpenAs_RunDLL {record.FullPath}");
                return true;
            },
            IconOrGlyph = "\ue7ac",
        };
    }

    private static bool CanRunAsDifferentUser(ReadOnlySpan<char> path)
    {
        return Path.GetExtension(path) switch
        {
            ".exe" or ".bat" or ".msi" => true,
            _ => false,
        };
    }
}
