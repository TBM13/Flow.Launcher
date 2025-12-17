using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Shutdown;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Plugin.Sys
{
    public class Main : IPlugin, IPluginI18n
    {
        // SHTDN_REASON_MAJOR_OTHER indicates a generic shutdown reason that isn't categorized under hardware failure,
        // software updates, or other predefined reasons.
        // SHTDN_REASON_FLAG_PLANNED marks the shutdown as planned rather than an unexpected shutdown or failure
        private const SHUTDOWN_REASON REASON = SHUTDOWN_REASON.SHTDN_REASON_MAJOR_OTHER |
            SHUTDOWN_REASON.SHTDN_REASON_FLAG_PLANNED;

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

        private static unsafe bool EnableShutdownPrivilege()
        {
            try
            {
                if (!PInvoke.OpenProcessToken(Process.GetCurrentProcess().SafeHandle, TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | TOKEN_ACCESS_MASK.TOKEN_QUERY, out var tokenHandle))
                {
                    return false;
                }

                if (!PInvoke.LookupPrivilegeValue(null, PInvoke.SE_SHUTDOWN_NAME, out var luid))
                {
                    return false;
                }

                var privileges = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Privileges = new() { e0 = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = TOKEN_PRIVILEGES_ATTRIBUTES.SE_PRIVILEGE_ENABLED } }
                };

                if (!PInvoke.AdjustTokenPrivileges(tokenHandle, false, &privileges, 0, null, null))
                {
                    return false;
                }

                if (Marshal.GetLastWin32Error() != (int)WIN32_ERROR.NO_ERROR)
                {
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static List<Result> Commands(Query query)
        {
            var results = new List<Result>();
            var recycleBinFolder = "shell:RecycleBinFolder";
            results.AddRange(
            [
                new Result
                {
                    Title = Localize.flowlauncher_plugin_sys_shutdown_computer_cmd(),
                    SubTitle = Localize.flowlauncher_plugin_sys_shutdown_computer(),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe7e8"),
                    Action = c =>
                    {
                        var result = Context.API.ShowMsgBox(
                            Localize.flowlauncher_plugin_sys_dlgtext_shutdown_computer(),
                            Localize.flowlauncher_plugin_sys_shutdown_computer(),
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            // Save settings before shutdown to avoid data loss
                            Context.API.SaveAppAllSettings();

                            if (EnableShutdownPrivilege())
                                PInvoke.ExitWindowsEx(EXIT_WINDOWS_FLAGS.EWX_SHUTDOWN | EXIT_WINDOWS_FLAGS.EWX_POWEROFF, REASON);
                            else
                                Process.Start("shutdown", "/s /t 0");
                        }

                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.flowlauncher_plugin_sys_restart_computer_cmd(),
                    SubTitle = Localize.flowlauncher_plugin_sys_restart_computer(),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe777"),
                    Action = c =>
                    {
                        var result = Context.API.ShowMsgBox(
                            Localize.flowlauncher_plugin_sys_dlgtext_restart_computer(),
                            Localize.flowlauncher_plugin_sys_restart_computer(),
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            // Save settings before restart to avoid data loss
                            Context.API.SaveAppAllSettings();

                            if (EnableShutdownPrivilege())
                                PInvoke.ExitWindowsEx(EXIT_WINDOWS_FLAGS.EWX_REBOOT, REASON);
                            else
                                Process.Start("shutdown", "/r /t 0");
                        }

                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.flowlauncher_plugin_sys_restart_advanced_cmd(),
                    SubTitle = Localize.flowlauncher_plugin_sys_restart_advanced(),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xecc5"),
                    Action = c =>
                    {
                        var result = Context.API.ShowMsgBox(
                            Localize.flowlauncher_plugin_sys_dlgtext_restart_computer_advanced(),
                            Localize.flowlauncher_plugin_sys_restart_computer(),
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            // Save settings before advanced restart to avoid data loss
                            Context.API.SaveAppAllSettings();

                            if (EnableShutdownPrivilege())
                                PInvoke.ExitWindowsEx(EXIT_WINDOWS_FLAGS.EWX_REBOOT | EXIT_WINDOWS_FLAGS.EWX_BOOTOPTIONS, REASON);
                            else
                                Process.Start("shutdown", "/r /o /t 0");
                        }

                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.flowlauncher_plugin_sys_log_off_cmd(),
                    SubTitle = Localize.flowlauncher_plugin_sys_log_off(),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe77b"),
                    Action = c =>
                    {
                        var result = Context.API.ShowMsgBox(
                            Localize.flowlauncher_plugin_sys_dlgtext_logoff_computer(),
                            Localize.flowlauncher_plugin_sys_log_off(),
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.Yes)
                            PInvoke.ExitWindowsEx(EXIT_WINDOWS_FLAGS.EWX_LOGOFF, REASON);
                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.flowlauncher_plugin_sys_lock_cmd(),
                    SubTitle = Localize.flowlauncher_plugin_sys_lock(),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe72e"),
                    Action = c =>
                    {
                        PInvoke.LockWorkStation();
                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.flowlauncher_plugin_sys_sleep_cmd(),
                    SubTitle = Localize.flowlauncher_plugin_sys_sleep(),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xec46"),
                    Action = c =>
                    {
                        PInvoke.SetSuspendState(false, false, false);
                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.flowlauncher_plugin_sys_hibernate_cmd(),
                    SubTitle = Localize.flowlauncher_plugin_sys_hibernate(),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe8be"),
                    Action= c =>
                    {
                        PInvoke.SetSuspendState(true, false, false);
                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.flowlauncher_plugin_sys_openrecyclebin_cmd(),
                    SubTitle = Localize.flowlauncher_plugin_sys_openrecyclebin(),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe74d"),
                    CopyText = recycleBinFolder,
                    Action = c =>
                    {
                        Process.Start("explorer", recycleBinFolder);
                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.flowlauncher_plugin_sys_exit_cmd(),
                    SubTitle = Localize.flowlauncher_plugin_sys_exit(),
                    IcoPath = "Images\\app.png",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe89f"),
                    Action = c =>
                    {
                        Context.API.HideMainWindow();
                        Application.Current.MainWindow.Close();
                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.flowlauncher_plugin_sys_setting_cmd(),
                    SubTitle = Localize.flowlauncher_plugin_sys_setting(),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xf210"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        Context.API.OpenSettingDialog();
                        return true;
                    }
                },
                new Result
                {
                    Title = Localize.flowlauncher_plugin_sys_toggleDarkMode_cmd(),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe7a1"),
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
                }
            ]);

            return results;
        }

        public string GetTranslatedPluginTitle()
        {
            return Localize.flowlauncher_plugin_sys_plugin_name();
        }

        public string GetTranslatedPluginDescription()
        {
            return Localize.flowlauncher_plugin_sys_plugin_description();
        }

        public void OnCultureInfoChanged(CultureInfo _)
        {

        }
    }
}
