using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Helpers;
using Flow.Launcher.Infrastructure.Plugins;
using Flow.Launcher.Infrastructure.Plugins.Interfaces;
using Flow.Launcher.Infrastructure.UI;
using Flow.Launcher.Plugin.Explorer.Search;

namespace Flow.Launcher.Plugin.Explorer
{
    internal class ContextMenu(PluginInitContext context, Settings settings) : IContextMenu
    {
        private static readonly string ClassName = nameof(ContextMenu);

        private readonly PluginInitContext _context = context;
        private readonly Settings _settings = settings;

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var contextMenus = new List<Result>();

            if (selectedResult.ContextData is SearchResult record)
            {
                contextMenus.Add(new Result
                {
                    Title = Localize.GeneralResult_CopyPath,
                    SubTitle = Localize.GeneralResult_CopyPath_Subtitle,
                    Action = _ =>
                    {
                        _context.API.CopyToClipboard(record.FullPath, showDefaultNotification: false);
                        return true;
                    },
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue8c8")
                });

                if (record.Type == ResultType.Folder)
                    contextMenus.Add(CreateOpenWithShellResult(record));
                else if (record.Type == ResultType.File)
                    contextMenus.Add(CreateOpenWithMenu(record));

                if (record.Type is not ResultType.Volume)
                {
                    // Show windows context menu
                    contextMenus.Add(new Result()
                    {
                        Title = Localize.GeneralResult_ShowWindowsMenu,
                        SubTitle = Localize.GeneralResult_ShowWindowsMenu_Subtitle,
                        Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue700"),
                        Action = _ =>
                        {
                            if (record.Type is ResultType.Volume)
                                return false;

                            ResultManager.ShowNativeContextMenu(record.FullPath, record.Type);
                            return false;
                        },
                    });
                }

                if (record.Type == ResultType.File && CanRunAsDifferentUser(record.FullPath))
                    // Run as different user
                    contextMenus.Add(new Result
                    {
                        Title = Localize.FileResult_RunAsDifferentUser,
                        SubTitle = Localize.FileResult_RunAsDifferentUser_Subtitle,
                        Action = (context) =>
                        {
                            try
                            {
                                _ = Task.Run(() => ShellCommand.RunAsDifferentUser(record.FullPath.SetProcessStartInfo()));
                            }
                            catch (FileNotFoundException e)
                            {
                                _context.API.ShowMsgError(
                                    Localize.PluginName,
                                    Localize.Error_FileNotFound(e.Message));
                                return false;
                            }

                            return true;
                        },
                        Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue748"),
                    });
            }

            return contextMenus;
        }

        private Result CreateOpenWithShellResult(SearchResult record)
        {
            string shellPath = _settings.ShellPath;

            var name = $"{Localize.FolderResult_OpenWithShell} {Path.GetFileNameWithoutExtension(shellPath)}";

            return new Result
            {
                Title = name,
                Action = _ =>
                {
                    try
                    {
                        Main.Context.API.StartProcess(shellPath, workingDirectory: record.FullPath, arguments: string.Empty);
                        return true;
                    }
                    catch (Exception e)
                    {
                        var message = Localize.Error_OpenWithShell(record.FullPath, Path.GetFileNameWithoutExtension(shellPath), shellPath);
                        _context.API.LogException(ClassName, message, e);
                        _context.API.ShowMsgError(message);
                        return false;
                    }
                },
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue756")
            };
        }

        private static Result CreateOpenWithMenu(SearchResult record)
        {
            return new Result
            {
                Title = Localize.FileResult_OpenWith,
                SubTitle = Localize.FileResult_OpenWith_Subtitle,
                Action = _ =>
                {
                    // No need to de-elevate since we are opening a windows menu which cannot bring security risks
                    Process.Start("rundll32.exe", $"{Path.Combine(Environment.SystemDirectory, "shell32.dll")},OpenAs_RunDLL {record.FullPath}");
                    return true;
                },
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue7ac"),
            };
        }

        private static bool CanRunAsDifferentUser(string path)
        {
            return Path.GetExtension(path) switch
            {
                ".exe" or ".bat" or ".msi" => true,
                _ => false,
            };
        }
    }
}
