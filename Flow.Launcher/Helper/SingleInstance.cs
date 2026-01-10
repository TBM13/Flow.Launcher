using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

// http://blogs.microsoft.co.il/arik/2010/05/28/wpf-single-instance-application/
// modified to allow single instace restart
namespace Flow.Launcher.Helper
{
    public interface ISingleInstanceApp
    {
        void OnSecondAppStarted();
    }

    /// <summary>
    /// This class checks to make sure that only one instance of 
    /// this application is running at a time.
    /// </summary>
    /// <remarks>
    /// Note: this class should be used with some caution, because it does no
    /// security checking. For example, if one instance of an app that uses this class
    /// is running as Administrator, any other instance, even if it is not
    /// running as Administrator, can activate it with command line arguments.
    /// For most apps, this will not be much of an issue.
    /// </remarks>
    public static class SingleInstance<TApplication> where TApplication : Application, ISingleInstanceApp
    {
        #region Private Fields

        /// <summary>
        /// String delimiter used in channel names.
        /// </summary>
        private const string Delimiter = ":";

        /// <summary>
        /// Suffix to the channel name.
        /// </summary>
        private const string ChannelNameSuffix = "SingeInstanceIPCChannel";
        private const string InstanceMutexName = "Flow.Launcher_Unique_Application_Mutex";
        private static string ApplicationIdentifier => InstanceMutexName + Environment.UserName;

        /// <summary>
        /// Application mutex.
        /// </summary>
        internal static Mutex? SingleInstanceMutex { get; set; }
        private static bool IsFirstInstance = false;

        #endregion

        #region Public Methods
        [MemberNotNull(nameof(SingleInstanceMutex))]
        private static void CreateSingleInstanceMutex(out bool isFirstInstance)
        {
            if (SingleInstanceMutex is not null)
            {
                isFirstInstance = IsFirstInstance;
                return;
            }

            // Create mutex based on unique application Id to check if this is the first instance of the application. 
            SingleInstanceMutex = new Mutex(true, ApplicationIdentifier, out var firstInstance);
            isFirstInstance = firstInstance;
            IsFirstInstance = firstInstance;
        }

        public static void WaitUntilWeAreFirstInstance()
        {
            CreateSingleInstanceMutex(out bool isFirstInstance);
            if (isFirstInstance)
                return;

            // Wait until we can acquire the mutex
            try
            {
                SingleInstanceMutex.WaitOne();
            }
            catch (AbandonedMutexException)
            {
                // Existing instance crashed
            }

            // We should own the mutex now
            SingleInstanceMutex.ReleaseMutex();
            SingleInstanceMutex.Dispose();
            SingleInstanceMutex = null;
            CreateSingleInstanceMutex(out isFirstInstance);
            if (!isFirstInstance)
            {
                // We still don't own the mutex, so lets keep waiting
                WaitUntilWeAreFirstInstance();
                return;
            }

            IsFirstInstance = true;
        }

        /// <summary>
        /// Checks if the instance of the application attempting to start is the first instance. 
        /// If not, activates the first instance.
        /// </summary>
        /// <returns>True if this is the first instance of the application.</returns>
        public static bool InitializeAsFirstInstance()
        {
            CreateSingleInstanceMutex(out bool isFirstInstance);
            string channelName = string.Concat(ApplicationIdentifier, Delimiter, ChannelNameSuffix);

            if (isFirstInstance)
            {
                _ = CreateRemoteServiceAsync(channelName);
                return true;
            }
            else
            {
                _ = SignalFirstInstanceAsync(channelName);
                return false;
            }
        }

        /// <summary>
        /// Cleans up single-instance code, clearing shared resources, mutexes, etc.
        /// </summary>
        public static void Cleanup()
        {
            SingleInstanceMutex?.ReleaseMutex();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Creates a remote server pipe for communication. 
        /// Once receives signal from client, will activate first instance.
        /// </summary>
        /// <param name="channelName">Application's IPC channel name.</param>
        private static async Task CreateRemoteServiceAsync(string channelName)
        {
            using NamedPipeServerStream pipeServer = new NamedPipeServerStream(channelName, PipeDirection.In);
            while (true)
            {
                // Wait for connection to the pipe
                await pipeServer.WaitForConnectionAsync();

                // Do an asynchronous call to ActivateFirstInstance function
                Application.Current?.Dispatcher.Invoke(ActivateFirstInstance);

                // Disconect client
                pipeServer.Disconnect();
            }
        }

        /// <summary>
        /// Creates a client pipe and sends a signal to server to launch first instance
        /// </summary>
        /// <param name="channelName">Application's IPC channel name.</param>
        /// <param name="args">
        /// Command line arguments for the second instance, passed to the first instance to take appropriate action.
        /// </param>
        private static async Task SignalFirstInstanceAsync(string channelName)
        {
            // Create a client pipe connected to server
            using NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", channelName, PipeDirection.Out);

            // Connect to the available pipe
            await pipeClient.ConnectAsync(0);
        }

        /// <summary>
        /// Activates the first instance of the application with arguments from a second instance.
        /// </summary>
        /// <param name="args">List of arguments to supply the first instance of the application.</param>
        private static void ActivateFirstInstance()
        {
            // Set main window state and process command line args
            if (Application.Current == null)
            {
                return;
            }

            ((TApplication)Application.Current).OnSecondAppStarted();
        }

        #endregion
    }
}
