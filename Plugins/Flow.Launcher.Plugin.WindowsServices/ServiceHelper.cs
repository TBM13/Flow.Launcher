using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;

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

    public ServiceStartMode StartMode { get; }

    public bool IsRunning { get; }

    private ServiceResult(ServiceController serviceController)
    {
        ArgumentNullException.ThrowIfNull(serviceController);

        ServiceName = serviceController.ServiceName;
        DisplayName = serviceController.DisplayName;
        StartMode = serviceController.StartType;
        IsRunning = serviceController.Status != ServiceControllerStatus.Stopped && serviceController.Status != ServiceControllerStatus.StopPending;
    }

    public static ServiceResult CreateServiceController(ServiceController serviceController)
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
}

public static class ServiceHelper
{
    public static IEnumerable<Result> Search(string search)
    {
        var services = ServiceController.GetServices().OrderBy(s => s.DisplayName);
        IEnumerable<ServiceController> serviceList = [];

        if (search.StartsWith("Status:", StringComparison.CurrentCultureIgnoreCase))
        {
            // allows queries like 'status:running'
            serviceList = services.Where(s => GetLocalizedStatus(s.Status).Contains(search.Split(':')[1], StringComparison.CurrentCultureIgnoreCase));
        }
        else if (search.StartsWith("Startup:", StringComparison.CurrentCultureIgnoreCase))
        {
            // allows queries like 'startup:automatic'
            serviceList = services.Where(s => GetLocalizedStartType(s.StartType, s.ServiceName).Contains(search.Split(':')[1], StringComparison.CurrentCultureIgnoreCase));
        }
        else
        {
            // To show 'starts with' results first, we split the search into two steps and then concatenating the lists.
            var servicesStartsWith = services
                .Where(s => s.DisplayName.StartsWith(search, StringComparison.OrdinalIgnoreCase) || s.ServiceName.StartsWith(search, StringComparison.OrdinalIgnoreCase));
            var servicesContains = services.Except(servicesStartsWith)
                .Where(s => s.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) || s.ServiceName.Contains(search, StringComparison.OrdinalIgnoreCase));
            serviceList = servicesStartsWith.Concat(servicesContains);
        }

        var result = serviceList.Select(s =>
        {
            var serviceResult = ServiceResult.CreateServiceController(s);
            if (serviceResult == null)
                return null;

            GlyphInfo glyph =
                s.StartType == ServiceStartMode.Disabled && s.Status == ServiceControllerStatus.Stopped
                    ? new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xeb90")
                    : new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: s.Status switch
                    {
                        ServiceControllerStatus.Stopped => "\xea39",
                        ServiceControllerStatus.Running => "\xe930",
                        ServiceControllerStatus.Paused => "\xe769",
                        _ => "\xe9ce" // Unknown
                    });

            return new Result()
            {
                Title = s.DisplayName,
                SubTitle = GetResultSubTitle(s),
                ContextData = serviceResult,
                Glyph = glyph,
                Action = c =>
                {
                    try
                    {
                        Main.Context.API.CopyToClipboard(s.ServiceName, showDefaultNotification: false);
                        return true;
                    }
                    catch (ExternalException)
                    {
                        Main.Context.API.ShowMsgBox("Failed to copy service name to clipboard.");
                        return false;
                    }
                }
            };
        }).Where(s => s != null);

        return result;
    }

    public static void ChangeStatus(ServiceResult serviceResult, Action action)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

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

            var process = Process.Start(info);
            process.WaitForExit();
            var exitCode = process.ExitCode;

            if (exitCode != 0)
                throw new Exception($"The command returned {exitCode}");
        }
        catch (Win32Exception ex)
        {
            throw new Exception($"Failed to change service '{serviceResult.DisplayName}' status to {action}: {ex.Message}");
        }
    }

    public static void OpenServices()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "services.msc",
                UseShellExecute = true,
            };

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to open services.msc: {ex.Message}");
        }
    }

    private static string GetResultSubTitle(ServiceController serviceController)
    {
        ArgumentNullException.ThrowIfNull(serviceController);
        return $"Status: {GetLocalizedStatus(serviceController.Status)} - Startup: {GetLocalizedStartType(serviceController.StartType, serviceController.ServiceName)} - Name: {serviceController.ServiceName}";
    }

    private static string GetLocalizedStatus(ServiceControllerStatus status)
    {
        if (status == ServiceControllerStatus.Stopped)
            return "Stopped";
        else if (status == ServiceControllerStatus.StartPending)
            return "Starting";
        else if (status == ServiceControllerStatus.StopPending)
            return "Stopping";
        else if (status == ServiceControllerStatus.Running)
            return "Running";
        else
        {
            return status == ServiceControllerStatus.ContinuePending
                ? "Continue"
                : status == ServiceControllerStatus.PausePending
                            ? "Pausing"
                            : status == ServiceControllerStatus.Paused ? "Paused" : status.ToString();
        }
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

    private static string GetLocalizedMessage(Action action)
    {
        return action == Action.Start
            ? "The service has been started"
            : action == Action.Stop
                ? "The service has been stopped"
                : action == Action.Restart ? "The service has been restarted" : string.Empty;
    }

    private static string GetLocalizedErrorMessage(Action action)
    {
        return action == Action.Start
            ? "An error occurred while starting the service"
            : action == Action.Stop
                ? "An error occurred while stopping the service"
                : action == Action.Restart ? "An error occurred while restarting the service" : string.Empty;
    }

    private static bool IsDelayedStart(string serviceName)
        => (int?)Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Services\" + serviceName, false)?.GetValue("DelayedAutostart", 0, RegistryValueOptions.None) == 1;
}
