using System.Diagnostics;
using System.Windows;
using Flow.Launcher.Interop;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;

namespace Flow.Launcher.Plugin.Sys
{
    public static class PluginMetadataDefinition
    {
        public static readonly PluginMetadata Metadata = new()
        {
            ID = "CEA08895D2544B019B2E9C5009600DF4",
            ActionKeywords = [">"],
            Name = "System Commands",
            Description = "Provide System related commands. e.g. shutdown,lock, setting etc.",
            Author = "qianlifeng",
            Version = "1.0.0",
            IcoPath = "Images/Plugin.Sys.png",

            Plugin = new Main()
        };
    }

    public class Main : IPlugin
    {
        internal static PluginInitContext Context { get; private set; } = null!;

        public List<Result> Query(Query query)
        {
            var commands = Commands(query);
            var results = new List<Result>();
            var isEmptyQuery = string.IsNullOrWhiteSpace(query.Search);
            foreach (var c in commands)
            {
                if (isEmptyQuery)
                {
                    results.Add(c);
                    continue;
                }

                // Match from localized title & localized subtitle & keyword
                var titleMatch = Context.API.FuzzySearch(query.Search, c.Title);
                var subTitleMatch = Context.API.FuzzySearch(query.Search, c.SubTitle);

                // Get the largest score from them
                var score = Math.Max(titleMatch.Score, subTitleMatch.Score);
                if (score > 0)
                {
                    c.Score = score;
                    results.Add(c);
                }
            }

            return results;
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
        }

        private static List<Result> Commands(Query query)
        {
            var results = new List<Result>();
            var recycleBinFolder = "shell:RecycleBinFolder";
            results.AddRange(
            [
                new Result
                {
                    Title = Localize.Cmd_Shutdown,
                    SubTitle = Localize.Cmd_Shutdown_Description,
                    Glyph = new GlyphInfo (Glyph:"\xe7e8"),
                    Action = c =>
                    {
                        var result = MessageBox.Show(
                            Localize.Dialog_ConfirmShutdown, Localize.Cmd_Shutdown_Description,
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            // Save settings before shutdown to avoid data loss
                            // TODO: Shouldn't flow be able to detect shutdowns and automatically save settings?
                            Context.API.SaveAppAllSettings();

                            OSHelper.Shutdown();
                        }

                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.Cmd_Restart,
                    SubTitle = Localize.Cmd_Restart_Description,
                    Glyph = new GlyphInfo (Glyph:"\xe777"),
                    Action = c =>
                    {
                        var result = MessageBox.Show(
                            Localize.Dialog_ConfirmRestart, Localize.Cmd_Restart_Description,
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            // Save settings before restart to avoid data loss
                            // TODO: Shouldn't flow be able to detect shutdowns and automatically save settings?
                            Context.API.SaveAppAllSettings();

                            OSHelper.Restart();
                        }

                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.Cmd_RestartAdvanced,
                    SubTitle = Localize.Cmd_RestartAdvanced_Description,
                    Glyph = new GlyphInfo (Glyph:"\xecc5"),
                    Action = c =>
                    {
                        var result = MessageBox.Show(
                            Localize.Dialog_ConfirmRestartAdvanced, Localize.Cmd_RestartAdvanced_Description,
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            // Save settings before advanced restart to avoid data loss
                            // TODO: Shouldn't flow be able to detect shutdowns and automatically save settings?
                            Context.API.SaveAppAllSettings();

                            OSHelper.Restart(advancedBootOptions: true);
                        }

                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.Cmd_LogOff,
                    SubTitle = Localize.Cmd_LogOff_Description,
                    Glyph = new GlyphInfo (Glyph:"\xe77b"),
                    Action = c =>
                    {
                        var result = MessageBox.Show(
                            Localize.Dialog_ConfirmLogOff, Localize.Cmd_LogOff_Description,
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                            OSHelper.LogOff();

                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.Cmd_Lock,
                    SubTitle = Localize.Cmd_Lock_Description,
                    Glyph = new GlyphInfo (Glyph:"\xe72e"),
                    Action = c =>
                    {
                        OSHelper.Lock();
                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.Cmd_Sleep,
                    SubTitle = Localize.Cmd_Sleep_Description,
                    Glyph = new GlyphInfo (Glyph:"\xec46"),
                    Action = c =>
                    {
                        OSHelper.Suspend();
                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.Cmd_Hibernate,
                    SubTitle = Localize.Cmd_Hibernate_Description,
                    Glyph = new GlyphInfo (Glyph:"\xe8be"),
                    Action= c =>
                    {
                        OSHelper.Suspend(hibernate: true);
                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.Cmd_OpenRecycleBin,
                    Glyph = new GlyphInfo (Glyph:"\xe74d"),
                    CopyText = recycleBinFolder,
                    Action = c =>
                    {
                        // No need to de-elevate since we are file explorer which cannot bring security risks
                        Process.Start("explorer", recycleBinFolder);
                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.Cmd_Exit,
                    SubTitle = Localize.Cmd_Exit_Description,
                    IcoPath = "Images\\app.png",
                    Glyph = new GlyphInfo (Glyph:"\xe89f"),
                    Action = c =>
                    {
                        Context.API.HideMainWindow();
                        Application.Current.MainWindow.Close();
                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.Cmd_Settings,
                    SubTitle = Localize.Cmd_Settings_Description,
                    Glyph = new GlyphInfo (Glyph:"\xf210"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        Context.API.OpenSettingDialog();
                        return true;
                    }
                },
                /*new Result
                {
                    Title = Localize.Cmd_ToggleDarkMode,
                    Glyph = new GlyphInfo (Glyph:"\xe7a1"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        using var key = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", true);
                        if (key is null)
                        {
                            Context.API.ShowMsgError("Failed to open registry key");
                            return false;
                        }

                        bool lightMode = (int)key.GetValue("SystemUsesLightTheme", 0) == 1;
                        key.SetValue("SystemUsesLightTheme", lightMode ? 0 : 1, RegistryValueKind.DWord);
                        key.SetValue("AppsUseLightTheme", lightMode ? 0 : 1, RegistryValueKind.DWord);

                        unsafe
                        {
                            fixed (char* pMessage = "ImmersiveColorSet")
                            {
                                // Without this, the taskbar's color doesn't update
                                PInvoke.SendMessageTimeout(
                                    HWND.HWND_BROADCAST,
                                    PInvoke.WM_SETTINGCHANGE,
                                    (WPARAM)0,
                                    (LPARAM)(nint)pMessage,
                                    SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG,
                                    2000, // Wait up to 2 seconds per window to avoid hanging
                                    null);
                            }
                        }

                        return true;
                    }
                },*/
                new Result {
                    Title = "Garbage Collection",
                    Glyph = new GlyphInfo ("\xE74D"),
                    Action = c => {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                        return true;
                    }
                }
            ]);

            return results;
        }
    }
}
