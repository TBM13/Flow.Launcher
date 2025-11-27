using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Windows.Controls;
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
    public string ServiceName { get; }
    public string DisplayName { get; }
    public ServiceStartMode StartType { get; }
    public ServiceControllerStatus Status { get; }
    public bool IsRunning { get; }

    private string? _description = null;

    private ServiceResult(ServiceController serviceController)
    {
        ServiceName = serviceController.ServiceName;
        DisplayName = serviceController.DisplayName;
        StartType = serviceController.StartType;
        Status = serviceController.Status;
        IsRunning = serviceController.Status != ServiceControllerStatus.Stopped && serviceController.Status != ServiceControllerStatus.StopPending;
    }

    public static ServiceResult? CreateServiceController(ServiceController serviceController)
    {
        try
        {
            var result = new ServiceResult(serviceController);

            return result;
        }
        catch (Exception ex)
        {
            // retrieve properties from serviceController will throw exception. Such as PlatformNotSupportedException.
            Debug.WriteLine($"Failed to create ServiceController: {ex.GetType().Name} - {ex.Message}");
        }

        return null;
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
        string? value = key?.GetValue("Description") as string;
        if (string.IsNullOrEmpty(value))
            return null;

        // Indirect string
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

                int len = buffer.IndexOf('\0');
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
    public static IEnumerable<Result?> Search(string search)
    {
        var services = ServiceController.GetServices().OrderBy(s => s.DisplayName);
        IEnumerable<ServiceResult?> serviceList = [];

        if (search.StartsWith("Status:", StringComparison.CurrentCultureIgnoreCase))
        {
            // allows queries like 'status:running'
            serviceList = services
                .Where(s => GetLocalizedStatus(s.Status).Contains(search.Split(':')[1], StringComparison.CurrentCultureIgnoreCase))
                .Select(ServiceResult.CreateServiceController);
        }
        else if (search.StartsWith("Startup:", StringComparison.CurrentCultureIgnoreCase))
        {
            // allows queries like 'startup:automatic'
            serviceList = services
                .Where(s => GetLocalizedStartType(s.StartType, s.ServiceName).Contains(search.Split(':')[1], StringComparison.CurrentCultureIgnoreCase))
                .Select(ServiceResult.CreateServiceController);
        }
        else
        {
            // To show 'starts with' results first, we split the search into two steps and then concatenating the lists.
            var servicesStartsWith = services
                .Where(s => s.DisplayName.StartsWith(search, StringComparison.OrdinalIgnoreCase) || s.ServiceName.StartsWith(search, StringComparison.OrdinalIgnoreCase));
            var servicesContains = services.Except(servicesStartsWith)
                .Where(s => s.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) || s.ServiceName.Contains(search, StringComparison.OrdinalIgnoreCase));
            serviceList = servicesStartsWith.Concat(servicesContains).Select(ServiceResult.CreateServiceController);
        }

        int failed = 0;
        var result = serviceList.Select(svcResult =>
        {
            if (svcResult is null)
            {
                failed++;
                return null;
            }

            GlyphInfo glyph =
                svcResult.StartType == ServiceStartMode.Disabled && svcResult.Status == ServiceControllerStatus.Stopped
                    ? new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xeb90")
                    : new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: svcResult.Status switch
                    {
                        ServiceControllerStatus.Stopped => "\xea39",
                        ServiceControllerStatus.Running => "\xe930",
                        ServiceControllerStatus.Paused => "\xe769",
                        _ => "\xe9ce" // Unknown
                    });

            return new Result()
            {
                Title = svcResult.DisplayName,
                SubTitle = GetResultSubTitle(svcResult),
                ContextData = svcResult,
                Glyph = glyph,
                CopyText = svcResult.ServiceName,
                Action = c =>
                {
                    try
                    {
                        Main.Context.API.CopyToClipboard(svcResult.ServiceName, showDefaultNotification: false);
                        return true;
                    }
                    catch (ExternalException)
                    {
                        Main.Context.API.ShowMsgBox("Failed to copy service name to clipboard.");
                        return false;
                    }
                },
                PreviewPanel = new Lazy<UserControl>(() => new PreviewPanel(svcResult)),
            };
        }).Where(s => s is not null);

        if (failed != 0)
            Main.Context.API.ShowMsgError($"Failed to get information from {failed} service(s)");

        return result;
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

    private static string GetResultSubTitle(ServiceResult serviceController)
    {
        return $"Status: {GetLocalizedStatus(serviceController.Status)} - Startup: {GetLocalizedStartType(serviceController.StartType, serviceController.ServiceName)} - Name: {serviceController.ServiceName}";
    }

    private static string GetLocalizedStatus(ServiceControllerStatus status)
    {
        return status switch
        {
            ServiceControllerStatus.StartPending => "Starting",
            ServiceControllerStatus.Running => "Running",
            ServiceControllerStatus.StopPending => "Stopping",
            ServiceControllerStatus.Stopped => "Stopped",

            ServiceControllerStatus.PausePending => "Pausing",
            ServiceControllerStatus.Paused => "Paused",
            ServiceControllerStatus.ContinuePending => "Continuing",
            _ => status.ToString()
        };
    }

    private static string GetLocalizedStartType(ServiceStartMode startMode, string serviceName)
    {
        if (startMode == ServiceStartMode.Boot)
            return "Boot";
        else if (startMode == ServiceStartMode.System)
            return "System";
        else
        {
            return startMode == ServiceStartMode.Automatic
                ? !IsDelayedStart(serviceName) ? "Automatic" : "Automatic (Delayed Start)"
                : startMode == ServiceStartMode.Manual
                            ? "Manual"
                            : startMode == ServiceStartMode.Disabled ? "Disabled" : startMode.ToString();
        }
    }

    private static bool IsDelayedStart(string serviceName)
        => (int?)Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Services\" + serviceName, false)?.GetValue("DelayedAutostart", 0, RegistryValueOptions.None) == 1;
}
