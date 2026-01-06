using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Flow.Launcher.Plugin.Explorer.Helper;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Plugin.Explorer
{
    internal class ContextMenu : IContextMenu
    {
        private static readonly string ClassName = nameof(ContextMenu);

        private PluginInitContext Context { get; set; }

        private Settings Settings { get; set; }

        public ContextMenu(PluginInitContext context, Settings settings)
        {
            Context = context;
            Settings = settings;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var contextMenus = new List<Result>();
            if (selectedResult.ContextData is SearchResult record)
            {
                contextMenus.Add(new Result
                {
                    Title = Localize.plugin_explorer_copypath(),
                    SubTitle = Localize.plugin_explorer_copypath_subtitle(),
                    Action = _ =>
                    {
                        try
                        {
                            Context.API.CopyToClipboard(record.FullPath, showDefaultNotification: false);
                            return true;
                        }
                        catch (Exception e)
                        {
                            LogException("Fail to set text in clipboard", e);
                            Context.API.ShowMsgError(Localize.plugin_explorer_fail_to_set_text());
                            return false;
                        }
                    },
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue8c8")
                });

                if (record.Type == ResultType.File && !string.IsNullOrEmpty(Settings.EditorPath))
                    contextMenus.Add(CreateOpenWithEditorResult(record, Settings.EditorPath));

                if ((record.Type == ResultType.Folder || record.Type == ResultType.Volume) && !string.IsNullOrEmpty(Settings.FolderEditorPath))
                    contextMenus.Add(CreateOpenWithEditorResult(record, Settings.FolderEditorPath));

                if (record.Type == ResultType.Folder)
                    contextMenus.Add(CreateOpenWithShellResult(record));
                else if (record.Type == ResultType.File)
                    contextMenus.Add(CreateOpenWithMenu(record));

                if (record.Type is not ResultType.Volume)
                {
                    contextMenus.Add(new Result()
                    {
                        Title = Localize.plugin_explorer_show_contextmenu_title(),
                        SubTitle = Localize.plugin_explorer_show_contextmenu_subtitle(),
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
                    contextMenus.Add(new Result
                    {
                        Title = Localize.plugin_explorer_runasdifferentuser(),
                        SubTitle = Localize.plugin_explorer_runasdifferentuser_subtitle(),
                        Action = (context) =>
                        {
                            try
                            {
                                _ = Task.Run(() => ShellCommand.RunAsDifferentUser(record.FullPath.SetProcessStartInfo()));
                            }
                            catch (FileNotFoundException e)
                            {
                                Context.API.ShowMsgError(
                                    Localize.plugin_explorer_plugin_name(),
                                    Localize.plugin_explorer_file_not_found(e.Message));
                                return false;
                            }

                            return true;
                        },
                        Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue748"),
                    });
            }

            return contextMenus;
        }

        private Result CreateOpenWithEditorResult(SearchResult record, string editorPath)
        {
            var name = $"{Localize.plugin_explorer_openwitheditor()} {Path.GetFileNameWithoutExtension(editorPath)}";

            return new Result
            {
                Title = name,
                Action = _ =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo()
                        {
                            FileName = editorPath,
                            ArgumentList = { record.FullPath }
                        });
                        return true;
                    }
                    catch (Exception e)
                    {
                        var message = Localize.plugin_explorer_openwitheditor_error(record.FullPath, Path.GetFileNameWithoutExtension(editorPath), editorPath);
                        LogException(message, e);
                        Context.API.ShowMsgError(message);
                        return false;
                    }
                },
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue70f"),
            };
        }

        private Result CreateOpenWithShellResult(SearchResult record)
        {
            string shellPath = Settings.ShellPath;

            var name = $"{Localize.plugin_explorer_openwithshell()} {Path.GetFileNameWithoutExtension(shellPath)}";

            return new Result
            {
                Title = name,
                Action = _ =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo()
                        {
                            FileName = shellPath,
                            WorkingDirectory = record.FullPath
                        });
                        return true;
                    }
                    catch (Exception e)
                    {
                        var message = Localize.plugin_explorer_openwithshell_error(record.FullPath, Path.GetFileNameWithoutExtension(shellPath), shellPath);
                        LogException(message, e);
                        Context.API.ShowMsgError(message);
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
                Title = Localize.plugin_explorer_openwith(),
                SubTitle = Localize.plugin_explorer_openwith_subtitle(),
                Action = _ =>
                {
                    Process.Start("rundll32.exe", $"{Path.Combine(Environment.SystemDirectory, "shell32.dll")},OpenAs_RunDLL {record.FullPath}");
                    return true;
                },
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue7ac"),
            };
        }

        private void LogException(string message, Exception e)
        {
            Context.API.LogException(ClassName, message, e);
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
