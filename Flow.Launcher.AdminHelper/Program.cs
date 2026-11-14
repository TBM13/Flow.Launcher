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

    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 4 || args[0] != "--client-sid" || args[2] != "--session-id")
            return ExitInvalidArgs;

        SecurityIdentifier clientSid;
        try
        {
            clientSid = new SecurityIdentifier(args[1]);
        }
        catch (ArgumentException)
        {
            return ExitInvalidArgs;
        }

        if (!int.TryParse(args[3], out int clientSessionId) || clientSessionId < 0)
            return ExitInvalidArgs;

        using Process currentProcess = Process.GetCurrentProcess();
        if (currentProcess.SessionId != clientSessionId)
            return ExitSessionMismatch;

        string pipeName = KeyboardIpcProtocol.GetPipeName(clientSessionId, clientSid.Value);
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
            if (!ValidateClient(pipe))
                return ExitUnauthorizedClient;

            using KeyboardIpcServer server = new(pipe);
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
    private static bool ValidateClient(NamedPipeServerStream pipe)
    {
        string expectedClientPath = Path.Combine(AppContext.BaseDirectory, "..", "Flow.Launcher.exe");
        if (!ProcessHelper.GetProcessIdFromPipeClient(pipe, out uint clientProcessId))
            return false;

        string processFileName = ProcessHelper.GetProcessFileName(clientProcessId);
        if (string.IsNullOrEmpty(processFileName))
            return false;

        return string.Equals(
            Path.GetFullPath(processFileName),
            Path.GetFullPath(expectedClientPath),
            StringComparison.OrdinalIgnoreCase);
    }
}
