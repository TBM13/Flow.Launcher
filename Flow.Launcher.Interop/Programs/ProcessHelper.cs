using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;

namespace Flow.Launcher.Interop.Programs;

/// <summary>
/// Helper class for interacting with processes.
/// </summary>
public static class ProcessHelper
{
    private const char Quote = '\"';
    private const char Backslash = '\\';

    private static bool ContainsNoWhitespaceOrQuotes(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c) || c == Quote)
                return false;
        }

        return true;
    }

    // From https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/PasteArguments.cs
    private static void AppendArgument(StringBuilder stringBuilder, string argument)
    {
        if (stringBuilder.Length != 0)
            stringBuilder.Append(' ');

        // Parsing rules for non-argv[0] arguments:
        //   - Backslash is a normal character except followed by a quote.
        //   - 2N backslashes followed by a quote ==> N literal backslashes followed by unescaped quote
        //   - 2N+1 backslashes followed by a quote ==> N literal backslashes followed by a literal quote
        //   - Parsing stops at first whitespace outside of quoted region.
        //   - (post 2008 rule): A closing quote followed by another quote ==> literal quote, and parsing remains in quoting mode.
        if (argument.Length != 0 && ContainsNoWhitespaceOrQuotes(argument))
        {
            // Simple case - no quoting or changes needed.
            stringBuilder.Append(argument);
        }
        else
        {
            stringBuilder.Append(Quote);
            int idx = 0;
            while (idx < argument.Length)
            {
                char c = argument[idx++];
                if (c == Backslash)
                {
                    int numBackSlash = 1;
                    while (idx < argument.Length && argument[idx] == Backslash)
                    {
                        idx++;
                        numBackSlash++;
                    }

                    if (idx == argument.Length)
                    {
                        // We'll emit an end quote after this so must double the number of backslashes.
                        stringBuilder.Append(Backslash, numBackSlash * 2);
                    }
                    else if (argument[idx] == Quote)
                    {
                        // Backslashes will be followed by a quote. Must double the number of backslashes.
                        stringBuilder.Append(Backslash, numBackSlash * 2 + 1);
                        stringBuilder.Append(Quote);
                        idx++;
                    }
                    else
                    {
                        // Backslash will not be followed by a quote, so emit as normal characters.
                        stringBuilder.Append(Backslash, numBackSlash);
                    }

                    continue;
                }

                if (c == Quote)
                {
                    // Escape the quote so it appears as a literal. This also guarantees that we won't end up generating a closing quote followed
                    // by another quote (which parses differently pre-2008 vs. post-2008.)
                    stringBuilder.Append(Backslash);
                    stringBuilder.Append(Quote);
                    continue;
                }

                stringBuilder.Append(c);
            }

            stringBuilder.Append(Quote);
        }
    }

    private static string JoinArgumentList(IEnumerable<string>? args)
    {
        if (args is null || !args.Any())
            return string.Empty;

        StringBuilder sb = new();
        foreach (string arg in args)
            AppendArgument(sb, arg);

        return sb.ToString();
    }

    /// <summary>
    /// Starts a process.
    /// <para/>
    /// It's highly recommended to use this instead of <see cref="Process"/> methods,
    /// since we automatically handle de-escalating the process when Flow is running with admin privileges.
    /// </summary>
    /// <param name="fileName">The name of the application to start, or the name of a document that has an associated application.</param>
    /// <param name="workingDirectory">The working directory. If not specified, the current directory will be used.</param>
    /// <param name="arguments">Optional arguments to pass to the process.</param>
    /// <param name="useShellExecute">Whether to use the shell to start the process.</param>
    /// <param name="verb">Verb to use when starting the process, e.g. "runas" for elevated permissions. If not specified, no verb will be used.</param>
    /// <param name="createNoWindow">If true the process will be started without creating a new window to contain it.</param>
    /// <inheritdoc cref="RunAsDesktopUser(string, string, string, bool, bool)" path="/exception"/>
    public static void StartProcess(
        string fileName,
        string workingDirectory = "",
        IEnumerable<string>? arguments = null,
        bool useShellExecute = false,
        string verb = "",
        bool createNoWindow = false)
    {
        workingDirectory = string.IsNullOrEmpty(workingDirectory) ? Environment.CurrentDirectory : workingDirectory;

        if (Environment.IsPrivilegedProcess)
        {
            // We need to use native methods to launch the process with lower privileges.
            // However, these native methods only support launching Win32 executables.
            // That's why we launch a de-escalated Flow.Launcher.Command process and
            // let it handle launching the target process (Win32/UWP/URL/etc.)

            arguments ??= [];
            string commandArgs = JoinArgumentList([
                "StartProcess",
                "-FileName", fileName,
                "-WorkingDir", workingDirectory,
                "-UseShellExecute", useShellExecute.ToString(),
                "-Verb", verb,
                "-CreateNoWindow", createNoWindow.ToString(),
                // Process args need to be added last
                "-BeginArgs", ..arguments
                ]);

            RunAsDesktopUser(
                Constant.CommandExecutablePath, Environment.CurrentDirectory, commandArgs,
                loadProfile: false, createNoWindow: true);
        }
        else
        {
            string args = JoinArgumentList(arguments);
            var info = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                Arguments = args,
                UseShellExecute = useShellExecute,
                Verb = verb,
                CreateNoWindow = createNoWindow
            };

            Process.Start(info)?.Dispose();
        }
    }

    /// <summary>
    /// Starts a process as the desktop user.
    /// </summary>
    /// <remarks>
    /// Requires the SE_IMPERSONATE_NAME privilege, so only use this when the app is running as admin.
    /// </remarks>
    /// <exception cref="Win32Exception"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    private static unsafe void RunAsDesktopUser(string app, string currentDir, string cmdLine, bool loadProfile, bool createNoWindow)
    {
        // argv[0] should be the executable path
        app = app.Trim('"', ' ', '\t');
        string fullCmdLine = $"\"{app}\"";
        if (!string.IsNullOrEmpty(cmdLine))
            fullCmdLine += $" {cmdLine}";

        // CreateProcessWithToken requires cmdline to be mutable
        char[] mutableCmdline = (fullCmdLine + '\0').ToCharArray();

        STARTUPINFOW si = new()
        {
            cb = (uint)sizeof(STARTUPINFOW),
        };
        // Lets ensure the new process is created on the same desktop/workstation as the shell
        string lpDesktop = "winsta0\\default";

        PROCESS_INFORMATION pi = new();
        HANDLE hShellProcess = HANDLE.Null, hShellProcessToken = HANDLE.Null, hPrimaryToken = HANDLE.Null;
        void* lpEnvironment = null;

        try
        {
            // Get the handle of the shell process (explorer.exe)
            HWND hwnd = PInvoke.GetShellWindow();
            if (hwnd == HWND.Null)
                throw new InvalidOperationException("Shell is not running");

            _ = PInvoke.GetWindowThreadProcessId(hwnd, out uint dwPID);
            if (dwPID == 0)
                throw new InvalidOperationException("Unable to get the shell's PID");

            hShellProcess = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, dwPID);
            if (hShellProcess.IsNull)
                throw new Win32Exception(Marshal.GetLastPInvokeError());

            // Get the access token of the shell process
            if (!PInvoke.OpenProcessToken(hShellProcess, TOKEN_ACCESS_MASK.TOKEN_DUPLICATE, &hShellProcessToken))
                throw new Win32Exception(Marshal.GetLastPInvokeError());

            // Make a primary token with it, with the minimum rights needed to launch a process
            TOKEN_ACCESS_MASK tokenRights = TOKEN_ACCESS_MASK.TOKEN_QUERY
                | TOKEN_ACCESS_MASK.TOKEN_ASSIGN_PRIMARY | TOKEN_ACCESS_MASK.TOKEN_DUPLICATE
                // seclogon needs to modify the token's DACL
                | TOKEN_ACCESS_MASK.TOKEN_ADJUST_DEFAULT | TOKEN_ACCESS_MASK.TOKEN_ADJUST_SESSIONID;

            if (!PInvoke.DuplicateTokenEx(
                hShellProcessToken,
                tokenRights,
                null,
                SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                TOKEN_TYPE.TokenPrimary,
                &hPrimaryToken))
                throw new Win32Exception(Marshal.GetLastPInvokeError());

            // Create environment block for the target user
            // (otherwise the new process will inherit the admin's environment variables)
            if (!PInvoke.CreateEnvironmentBlock(&lpEnvironment, hPrimaryToken, false))
                throw new Win32Exception(Marshal.GetLastPInvokeError());

            // Start the new process with that primary token
            PROCESS_CREATION_FLAGS flags = PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT;
            if (createNoWindow)
                flags |= PROCESS_CREATION_FLAGS.CREATE_NO_WINDOW;

            CREATE_PROCESS_LOGON_FLAGS logonFlags = loadProfile ? CREATE_PROCESS_LOGON_FLAGS.LOGON_WITH_PROFILE : 0;

            fixed (char* cmdLinePtr = mutableCmdline)
            fixed (char* currentDirPtr = currentDir)
            fixed (char* desktopPtr = lpDesktop)
            {
                si.lpDesktop = desktopPtr;

                if (!PInvoke.CreateProcessWithToken(
                    hPrimaryToken,
                    logonFlags,
                    null,
                    cmdLinePtr,
                    flags,
                    lpEnvironment,
                    currentDirPtr,
                    &si,
                    &pi))
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }
        finally
        {
            if (!pi.hProcess.IsNull) PInvoke.CloseHandle(pi.hProcess);
            if (!pi.hThread.IsNull) PInvoke.CloseHandle(pi.hThread);

            if (lpEnvironment is not null) PInvoke.DestroyEnvironmentBlock(lpEnvironment);

            if (!hPrimaryToken.IsNull) PInvoke.CloseHandle(hPrimaryToken);
            if (!hShellProcessToken.IsNull) PInvoke.CloseHandle(hShellProcessToken);
            if (!hShellProcess.IsNull) PInvoke.CloseHandle(hShellProcess);
        }
    }

    /// <summary>
    /// Gets the file name of the specified process.
    /// </summary>
    /// <exception cref="Win32Exception"></exception>
    public static unsafe string GetProcessFileName(uint processId)
    {
        HANDLE hProcess = PInvoke.OpenProcess(
            PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
            false,
            processId
        );

        if (hProcess.IsNull)
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        try
        {
            uint capacity = PInvoke.MAX_PATH;
            char* buffer = stackalloc char[(int)capacity];

            if (!PInvoke.QueryFullProcessImageName(hProcess, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, &capacity))
                throw new Win32Exception(Marshal.GetLastPInvokeError());

            return new string(buffer, 0, (int)capacity);
        }
        finally
        {
            PInvoke.CloseHandle(hProcess);
        }
    }
}
