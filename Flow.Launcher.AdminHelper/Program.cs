using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Flow.Launcher.AdminHelper.IPC;
using Flow.Launcher.Interop.Programs;

namespace Flow.Launcher.AdminHelper;

internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitInvalidArgs = 1;
    private const int ExitSessionMismatch = 2;
    private const int ExitAlreadyRunning = 3;
    private const int ExitConnectionTimeout = 4;
    private const int ExitUnauthorizedClient = 5;
    private const int ExitNotPrivileged = 6;

    private static async Task<int> Main(string[] args)
    {
        if (!Environment.IsPrivilegedProcess)
            return ExitNotPrivileged;

        string? clientSidArg = null;
        int? expectedSessionId = null;
        bool registerTask = false;
        bool unregisterTask = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--client-sid":
                    if (clientSidArg is not null || ++i >= args.Length)
                        return ExitInvalidArgs;
                    clientSidArg = args[i];
                    break;

                case "--session-id":
                    if (expectedSessionId is not null || ++i >= args.Length
                        || !int.TryParse(args[i], out int parsedSessionId) || parsedSessionId < 0)
                        return ExitInvalidArgs;
                    expectedSessionId = parsedSessionId;
                    break;

                case "--register-task":
                    if (registerTask || unregisterTask)
                        return ExitInvalidArgs;
                    registerTask = true;
                    break;

                case "--unregister-task":
                    if (unregisterTask || registerTask)
                        return ExitInvalidArgs;
                    unregisterTask = true;
                    break;

                default:
                    return ExitInvalidArgs;
            }
        }

        if (string.IsNullOrEmpty(clientSidArg))
            return ExitInvalidArgs;

        SecurityIdentifier clientSid;
        try
        {
            clientSid = new SecurityIdentifier(clientSidArg);
        }
        catch (ArgumentException)
        {
            return ExitInvalidArgs;
        }

        using Process currentProcess = Process.GetCurrentProcess();
        int clientSessionId = expectedSessionId ?? currentProcess.SessionId;
        if (currentProcess.SessionId <= 0 || currentProcess.SessionId != clientSessionId)
            return ExitSessionMismatch;

        string pipeName = AdminHelperProtocol.GetPipeName(clientSessionId, clientSid.Value);
        if (!SingleInstance.TryAcquire(
            $"Local\\{pipeName}",
            out var instance, TimeSpan.FromMilliseconds(5000)))
            return ExitAlreadyRunning;

        using (instance)
        {
            await using NamedPipeServerStream pipe = CreatePipe(pipeName, clientSid, currentProcess.Handle);

            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
            try
            {
                await pipe.WaitForConnectionAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                return ExitConnectionTimeout;
            }

            // Ensure the connected client is actually Flow Launcher
            if (!ProcessHelper.GetProcessIdFromPipeClient(pipe, out uint clientProcessId))
                return ExitUnauthorizedClient;
            if (!ValidateClient(clientProcessId))
                return ExitUnauthorizedClient;

            // Register or unregister task to start AdminHelper without UAC prompt
            if (registerTask)
            {
                using WindowsIdentity currentIdentity = WindowsIdentity.GetCurrent();
                if (currentIdentity.User == clientSid)
                    AdminHelperTask.TryRegister(clientSid);
            }
            else if (unregisterTask)
            {
                AdminHelperTask.TryDelete(clientSid);
            }

            using AdminHelperServer server = new(pipe, clientProcessId, clientSid);
            await server.RunAsync(CancellationToken.None);
            return ExitSuccess;
        }
    }

    private static NamedPipeServerStream CreatePipe(string pipeName, SecurityIdentifier userSid, nint processHandle)
    {
        // Writing a mandatory integrity label modifies the SACL, which requires SeSecurityPrivilege
        ProcessHelper.EnableSecurityPrivilege(processHandle, "SeSecurityPrivilege");
        try
        {
            // Grant Full Control to Built-in Admins & LocalSystem (handles UAC OTS elevation)
            PipeSecurity security = new();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

            // Grant only relevant pipe rights to the SID of the user where Flow is running
            security.AddAccessRule(new PipeAccessRule(
                userSid,
                PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
                AccessControlType.Allow));

            // Lower Mandatory Label to Medium (ME) so non-elevated Flow Launcher can connect
            security.SetSecurityDescriptorSddlForm("S:(ML;;NW;;;ME)", AccessControlSections.Audit);

            return NamedPipeServerStreamAcl.Create(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance,
                0,
                0,
                security);
        }
        finally
        {
            // Permanently remove the privilege once we no longer need it
            ProcessHelper.RemoveSecurityPrivilege(processHandle, "SeSecurityPrivilege");
        }
    }

    /// <returns>True if the connected client is Flow Launcher</returns>
    private static bool ValidateClient(uint clientProcessId)
    {
        string expectedClientPath = Path.Combine(AppContext.BaseDirectory, "..", "Flow.Launcher.exe");

        string processFileName = ProcessHelper.GetProcessFileName(clientProcessId);
        if (string.IsNullOrEmpty(processFileName))
            return false;

        return string.Equals(
            Path.GetFullPath(processFileName),
            Path.GetFullPath(expectedClientPath),
            StringComparison.OrdinalIgnoreCase);
    }
}
