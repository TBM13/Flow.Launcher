using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace Flow.Launcher.AdminHelper.IPC;

public static class AdminHelperTask
{
    private const int SchtasksTimeoutMs = 5000;

    public static string GetTaskName(SecurityIdentifier userSid)
        => $"Flow.Launcher.AdminHelper.{userSid.Value}";

    private static string GetTaskArguments(SecurityIdentifier userSid)
        => $"--client-sid {userSid.Value}";

    public static bool IsRegisteredAndValid(SecurityIdentifier userSid, string helperExecutablePath)
    {
        try
        {
            string taskFilePath = Path.Combine(Environment.SystemDirectory, "Tasks", GetTaskName(userSid));
            if (!File.Exists(taskFilePath))
                return false;

            string xml = File.ReadAllText(taskFilePath);
            string fullHelperPath = Path.GetFullPath(helperExecutablePath);

            return xml.Contains($"<Command>{SecurityElement.Escape(fullHelperPath)}</Command>", StringComparison.OrdinalIgnoreCase)
                && xml.Contains($"<Arguments>{SecurityElement.Escape(GetTaskArguments(userSid))}</Arguments>", StringComparison.Ordinal)
                && xml.Contains($"<UserId>{SecurityElement.Escape(userSid.Value)}</UserId>", StringComparison.OrdinalIgnoreCase)
                && xml.Contains("<LogonType>InteractiveToken</LogonType>", StringComparison.Ordinal)
                && xml.Contains("<RunLevel>HighestAvailable</RunLevel>", StringComparison.Ordinal)
                && !xml.Contains("<Enabled>false</Enabled>", StringComparison.OrdinalIgnoreCase)
                && !xml.Contains("<AllowStartOnDemand>false</AllowStartOnDemand>", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to start AdminHelper through a scheduled task.
    /// </summary>
    /// <returns>true if successful.</returns>
    public static bool TryStart(SecurityIdentifier userSid, string helperExecutablePath)
    {
        if (!IsRegisteredAndValid(userSid, helperExecutablePath))
            return false;

        return RunSchtasks("/Run", "/TN", GetTaskName(userSid));
    }

    /// <summary>
    /// Attempts to register AdminHelper as a scheduled task.
    /// </summary>
    /// <returns>true if successful.</returns>
    public static bool TryRegister(SecurityIdentifier userSid)
    {
        string? helperPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(helperPath) || !File.Exists(helperPath))
            return false;

        string fullHelperPath = Path.GetFullPath(helperPath);
        string escapedHelperPath = SecurityElement.Escape(fullHelperPath);
        string escapedArgs = SecurityElement.Escape(GetTaskArguments(userSid));
        string escapedSid = SecurityElement.Escape(userSid.Value);

        string taskXml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Runs Flow Launcher AdminHelper with administrator privileges for global hotkeys.</Description>
              </RegistrationInfo>
              <Triggers />
              <Principals>
                <Principal id="Author">
                  <UserId>{escapedSid}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>false</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>4</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{escapedHelperPath}</Command>
                  <Arguments>{escapedArgs}</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;

        string tempXmlPath = Path.Combine(
            Path.GetTempPath(),
            $"Flow.Launcher.AdminHelper.{Guid.NewGuid():N}.xml");

        try
        {
            File.WriteAllText(tempXmlPath, taskXml, Encoding.Unicode);
            return RunSchtasks("/Create", "/TN", GetTaskName(userSid), "/XML", tempXmlPath, "/F");
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempXmlPath))
                    File.Delete(tempXmlPath);
            }
            catch { }
        }
    }

    public static bool TryDelete(SecurityIdentifier userSid)
    {
        try
        {
            string taskName = GetTaskName(userSid);
            string taskFilePath = Path.Combine(Environment.SystemDirectory, "Tasks", taskName);
            if (!File.Exists(taskFilePath))
                return true;

            return RunSchtasks("/Delete", "/TN", taskName, "/F");
        }
        catch
        {
            return false;
        }
    }

    private static bool RunSchtasks(params ReadOnlySpan<string> args)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (string arg in args)
                startInfo.ArgumentList.Add(arg);

            using Process? process = Process.Start(startInfo);
            if (process is null)
                return false;

            if (!process.WaitForExit(SchtasksTimeoutMs))
            {
                try
                {
                    process.Kill();
                }
                catch { }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
