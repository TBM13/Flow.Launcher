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

        public List<Result>? Query(Query query)
        {
            if (query.IsHomeQuery)
                return null;

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
                var match = Context.API.StringMatcher.FuzzySearchBest(query.Search, c.Title, c.SubTitle);
                if (match.IsThresholdMet)
                {
                    c.Score = match.Score;
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
                    Title = "Shutdown",
                    SubTitle = "Shutdown Computer",
                    IconOrGlyph = "\xe7e8",
                    Action = c =>
                    {
                        var result = MessageBox.Show(
                            "Are you sure you want to shutdown the computer?", "Shutdown Computer",
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
                    Title = "Restart",
                    SubTitle = "Restart Computer",
                    IconOrGlyph = "\xe777",
                    Action = c =>
                    {
                        var result = MessageBox.Show(
                            "Are you sure you want to restart the computer?", "Restart Computer",
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
                    Title = "Restart With Advanced Boot Options",
                    SubTitle = "Restart the computer with Advanced Boot Options for Safe and Debugging modes, as well as other options",
                    IconOrGlyph = "\xecc5",
                    Action = c =>
                    {
                        var result = MessageBox.Show(
                            "Are you sure you want to restart the computer with Advanced Boot Options?", "Restart the computer with Advanced Boot Options for Safe and Debugging modes, as well as other options",
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
                    Title = "Log Off/Sign Out",
                    SubTitle = "Log off",
                    IconOrGlyph = "\xe77b",
                    Action = c =>
                    {
                        var result = MessageBox.Show(
                            "Are you sure you want to log off?", "Log off",
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                            OSHelper.LogOff();

                        return true;
                    }
                },
                new Result
                {
                    Title = "Lock",
                    SubTitle = "Lock this computer",
                    IconOrGlyph = "\xe72e",
                    Action = c =>
                    {
                        OSHelper.Lock();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Sleep",
                    SubTitle = "Put computer to sleep",
                    IconOrGlyph = "\xec46",
                    Action = c =>
                    {
                        OSHelper.Suspend();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Hibernate",
                    SubTitle = "Hibernate computer",
                    IconOrGlyph = "\xe8be",
                    Action= c =>
                    {
                        OSHelper.Suspend(hibernate: true);
                        return true;
                    }
                },
                new Result
                {
                    Title = "Open Recycle Bin",
                    IconOrGlyph = "\xe74d",
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
                    Title = "Exit",
                    SubTitle = "Exit Flow Launcher",
                    IconOrGlyph = "\xe89f",
                    Action = c =>
                    {
                        Context.API.HideMainWindow();
                        Application.Current.MainWindow.Close();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Settings",
                    SubTitle = "Open Flow Launcher Settings",
                    IconOrGlyph = "\xf210",
                    Action = c =>
                    {
                        Context.API.OpenSettingDialog();
                        return true;
                    }
                },
                /*new Result
                {
                    Title = Localize.Cmd_ToggleDarkMode,
                    IconOrGlyph = "\xe7a1",
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
                    IconOrGlyph = "\xE74D",
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

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
