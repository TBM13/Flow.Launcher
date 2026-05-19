using System.Diagnostics;

namespace Flow.Launcher.Command;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Error: No args provided");
            return -1;
        }

        if (args[0] == "StartProcess")
        {
            string fileName = string.Empty;
            string workingDirectory = Environment.CurrentDirectory;
            bool useShellExecute = true;
            string verb = string.Empty;
            bool createNoWindow = false;
            string[] processArgs = [];

            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-FileName":
                        if (i + 1 == args.Length)
                        {
                            Console.WriteLine("No arg provided after FileName");
                            return -2;
                        }

                        fileName = args[++i];
                        break;

                    case "-WorkingDir":
                        if (i + 1 == args.Length)
                        {
                            Console.WriteLine("No arg provided after WorkingDir");
                            return -2;
                        }

                        workingDirectory = args[++i];
                        break;

                    case "-UseShellExecute":
                        if (i + 1 == args.Length)
                        {
                            Console.WriteLine("No arg provided after UseShellExecute");
                            return -2;
                        }
                        if (!bool.TryParse(args[++i], out bool useShell))
                        {
                            Console.WriteLine("Invalid value for UseShellExecute");
                            return -2;
                        }

                        useShellExecute = useShell;
                        break;

                    case "-Verb":
                        if (i + 1 == args.Length)
                        {
                            Console.WriteLine("No arg provided after Verb");
                            return -2;
                        }

                        verb = args[++i];
                        break;

                    case "-CreateNoWindow":
                        if (i + 1 == args.Length)
                        {
                            Console.WriteLine("No arg provided after CreateNoWindow");
                            return -2;
                        }
                        if (!bool.TryParse(args[++i], out bool createNoWin))
                        {
                            Console.WriteLine("Invalid value for CreateNoWindow");
                            return -2;
                        }

                        createNoWindow = createNoWin;
                        break;

                    case "-BeginArgs":
                        // Everything that follows is an argument
                        processArgs = args[(i + 1)..];
                        i = args.Length; // End the loop
                        break;

                    default:
                        Console.WriteLine($"Unknown arg: {args[i]}");
                        return -2;
                }
            }

            if (string.IsNullOrEmpty(fileName))
            {
                Console.WriteLine("Error: -FileName is required.");
                return -3;
            }

            ProcessStartInfo info = new()
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                UseShellExecute = useShellExecute,
                Verb = verb,
                CreateNoWindow = createNoWindow
            };
            foreach (string arg in processArgs)
                info.ArgumentList.Add(arg);

            try
            {
                Process.Start(info)?.Dispose();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return -4;
            }
        }

        Console.WriteLine($"Unknown command: {args[0]}");
        return -1;
    }
}
