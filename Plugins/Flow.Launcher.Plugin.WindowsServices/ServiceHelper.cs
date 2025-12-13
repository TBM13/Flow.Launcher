using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Windows.Controls;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.Plugin.WindowsServices.Preview;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;


namespace Flow.Launcher.Plugin.WindowsServices;

/* Based on https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.WindowsServices
The MIT License

Copyright (c) Microsoft Corporation. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

public enum Action
{
    Start,
    Stop,
    Restart,

    Disable,
    EnableManual,
    EnableAutomatic,
    EnableAutomaticDelayed
}

public class ServiceResult
{
    private static readonly string ClassName = typeof(ServiceResult).FullName ?? nameof(ServiceResult);

    public string ServiceName { get; }
    public string DisplayName { get; }
    public ServiceStartMode StartType { get; }
    public ServiceControllerStatus Status { get; }
    public bool IsRunning { get; }

    private string? _description = null;
    private string? _imagePath = null;

    private ServiceResult(ServiceController serviceController)
    {
        ServiceName = serviceController.ServiceName;
        DisplayName = serviceController.DisplayName;
        StartType = serviceController.StartType;
        Status = serviceController.Status;
        IsRunning = serviceController.Status != ServiceControllerStatus.Stopped && serviceController.Status != ServiceControllerStatus.StopPending;
    }

    public static ServiceResult? CreateServiceResult(ServiceController serviceController)
    {
        try
        {
            var result = new ServiceResult(serviceController);
            return result;
        }
        catch (Exception ex)
        {
            // Retrieving properties from ServiceController may throw exceptions like PlatformNotSupportedException
            Main.Context.API.LogException(ClassName, $"Failed to create {nameof(ServiceResult)}", ex);
        }

        return null;
    }

    public bool IsDelayedAutoStart()
    {
        RegistryKey? key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{ServiceName}", false);
        return (int?)key?.GetValue("DelayedAutostart", 0) == 1;
    }

    public string? GetImagePath()
    {
        if (_imagePath is not null)
            return _imagePath;

        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{ServiceName}");
        _imagePath = key?.GetValue("ImagePath") as string;
        return _imagePath;
    }

    public string? GetDescription()
    {
        if (_description is not null)
            return _description;

        // Try registry first since it's way faster than WMI
        _description = GetDescriptionFromRegistry();
        // Seems like all descriptions can be read from the registry, WMI is not needed
        // _description ??= GetDescriptionFromWMI();

        return _description;
    }

    private string? GetDescriptionFromRegistry()
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{ServiceName}");
        if (key?.GetValue("Description") is not string value)
            return null;

        // Check if the description is an indirect string and try to resolve it
        if (value.StartsWith('@'))
        {
            char[] buffer = new char[2048];
            while (true)
            {
                int hr = PInvoke.SHLoadIndirectString(value, buffer);
                if (hr != HRESULT.S_OK)
                {
                    if (hr == unchecked((int)0x8007007A)) // ERROR_INSUFFICIENT_BUFFER
                    {
                        if (buffer.Length >= 65536) return null;
                        buffer = new char[buffer.Length * 2];
                        continue;
                    }

                    return null;
                }

                int len = Array.IndexOf(buffer, '\0');
                return new string(buffer, 0, len);
            }
        }

        return value;
    }

    /*private string? GetDescriptionFromWMI()
    {
        using var searcher = new ManagementObjectSearcher(
            $"SELECT Description FROM Win32_Service WHERE Name = '{ServiceName.Replace("'", "''")}'");

        foreach (ManagementBaseObject? obj in searcher.Get())
        {
            string? desc = obj["description"]?.ToString();
            if (!string.IsNullOrEmpty(desc))
                return desc;
        }

        return null;
    }*/
}

public static class ServiceHelper
{
    private static readonly string ClassName = typeof(ServiceHelper).FullName ?? nameof(ServiceHelper);

    public static IEnumerable<Result> Search(string search)
    {
        ServiceController[] services = ServiceController.GetServices();

        int failed = 0;
        IEnumerable<Result?> results = services.Select(svc =>
        {
            ServiceResult? svcResult = ServiceResult.CreateServiceResult(svc);
            if (svcResult is null)
            {
                failed++;
                return null;
            }

            int score = 0;
            if (!string.IsNullOrWhiteSpace(search))
            {
                (MatchResult match, bool isHighPriority)[] matches = [
                    (Main.Context.API.FuzzySearch(search, svcResult.DisplayName), true),
                    (Main.Context.API.FuzzySearch(search, svcResult.ServiceName), true),
                    (Main.Context.API.FuzzySearch(search, GetLocalizedStartType(svcResult)), false),
                    (Main.Context.API.FuzzySearch(search, GetLocalizedStatus(svcResult.Status)), false)
                ];
                (MatchResult bestMatch, bool isHighPriority) = matches.OrderByDescending(r => r.match.Score).First();

                if (!bestMatch.IsSearchPrecisionScoreMet())
                    return null;

                score = isHighPriority ? bestMatch.Score + 1000 : bestMatch.Score;
            }

            return new Result()
            {
                Title = svcResult.DisplayName,
                SubTitle = GetResultSubTitle(svcResult),
                Glyph = GetResultGlyph(svcResult),
                IcoPath = Main.PLUGIN_ICON,
                ContextData = svcResult,
                CopyText = svcResult.ServiceName,
                Score = score,
                Action = c =>
                {
                    Main.Context.API.CopyToClipboard(svcResult.ServiceName);
                    return true;
                },
                PreviewPanel = new Lazy<UserControl>(() => new PreviewPanel(svcResult)),
            };
        });

        // Warn if we failed to create a ServiceResult for one or more services
        if (failed != 0)
        {
            Main.Context.API.LogError(ClassName, $"Failed to create {failed} ServiceResult(s)");
            Main.Context.API.ShowMsgError(Localize.plugin_windowsservices_error_getInformationFail(failed));
        }

        return results.Where(r => r is not null)!;
    }

    public static void ChangeStatus(ServiceResult serviceResult, Action action)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = "sc",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            switch (action)
            {
                case Action.Start:
                    info.Arguments = $"start \"{serviceResult.ServiceName}\"";
                    break;
                case Action.Stop:
                    info.Arguments = $"stop \"{serviceResult.ServiceName}\"";
                    break;
                case Action.Restart:
                    info.FileName = "cmd";
                    info.Arguments = $"/c sc stop \"{serviceResult.ServiceName}\" && timeout 1 && sc start \"{serviceResult.ServiceName}\"";
                    break;

                case Action.Disable:
                    info.Arguments = $"config \"{serviceResult.ServiceName}\" start= disabled";
                    break;
                case Action.EnableManual:
                    info.Arguments = $"config \"{serviceResult.ServiceName}\" start= demand";
                    break;
                case Action.EnableAutomatic:
                    info.Arguments = $"config \"{serviceResult.ServiceName}\" start= auto";
                    break;
                case Action.EnableAutomaticDelayed:
                    info.Arguments = $"config \"{serviceResult.ServiceName}\" start= delayed-auto";
                    break;

                default:
                    throw new Exception("Unknown action");
            }

            Process? process = Process.Start(info) ?? throw new Exception("Failed to start process");
            process.WaitForExit();

            int exitCode = process.ExitCode;
            if (exitCode != 0)
                throw new Exception($"The command returned {exitCode}");
        }
        catch (Win32Exception ex)
        {
            throw new Exception($"Failed to change service '{serviceResult.DisplayName}' status to {action}: {ex.Message}");
        }
    }

    private static GlyphInfo GetResultGlyph(ServiceResult svc)
    {
        if (svc.StartType == ServiceStartMode.Disabled && svc.Status == ServiceControllerStatus.Stopped)
            return new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xeb90");

        return new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: svc.Status switch
        {
            ServiceControllerStatus.Stopped => "\xea39",
            ServiceControllerStatus.Running => "\xe930",
            ServiceControllerStatus.Paused => "\xe769",
            _ => "\xe9ce" // Unknown
        });
    }

    private static string GetResultSubTitle(ServiceResult svc)
    {
        return Localize.plugin_windowsservices_info_status() + ": " + GetLocalizedStatus(svc.Status)
            + " - " + Localize.plugin_windowsservices_info_startupType() + ": " + GetLocalizedStartType(svc)
            + " - " + Localize.plugin_windowsservices_info_name() + ": " + svc.ServiceName;
    }

    private static string GetLocalizedStatus(ServiceControllerStatus status)
    {
        return status switch
        {
            ServiceControllerStatus.StartPending => Localize.plugin_windowsservices_info_status_startPending(),
            ServiceControllerStatus.Running => Localize.plugin_windowsservices_info_status_running(),
            ServiceControllerStatus.StopPending => Localize.plugin_windowsservices_info_status_stopPending(),
            ServiceControllerStatus.Stopped => Localize.plugin_windowsservices_info_status_stopped(),
            ServiceControllerStatus.PausePending => Localize.plugin_windowsservices_info_status_pausePending(),
            ServiceControllerStatus.Paused => Localize.plugin_windowsservices_info_status_paused(),
            ServiceControllerStatus.ContinuePending => Localize.plugin_windowsservices_info_status_continuePending(),
            _ => status.ToString()
        };
    }

    private static string GetLocalizedStartType(ServiceResult svc)
    {
        ServiceStartMode startMode = svc.StartType;
        return startMode switch
        {
            ServiceStartMode.Boot => Localize.plugin_windowsservices_info_startupType_boot(),
            ServiceStartMode.System => Localize.plugin_windowsservices_info_startupType_system(),
            ServiceStartMode.Automatic when svc.IsDelayedAutoStart() => Localize.plugin_windowsservices_info_startupType_automaticDelayed(),
            ServiceStartMode.Automatic => Localize.plugin_windowsservices_info_startupType_automatic(),
            ServiceStartMode.Manual => Localize.plugin_windowsservices_info_startupType_manual(),
            ServiceStartMode.Disabled => Localize.plugin_windowsservices_info_startupType_disabled(),
            _ => startMode.ToString()
        };
    }
}
