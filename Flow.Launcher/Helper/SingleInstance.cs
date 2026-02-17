using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Windows;

// http://blogs.microsoft.co.il/arik/2010/05/28/wpf-single-instance-application/
// modified to allow single instace restart
namespace Flow.Launcher.Helper
{
    public static class SingleInstance<TApplication> where TApplication : Application
    {
        private const string InstanceMutexName = "Flow.Launcher_Unique_Application_Mutex";
        private static string ApplicationIdentifier => InstanceMutexName + Environment.UserName;

        internal static Mutex? SingleInstanceMutex { get; set; }
        private static bool IsFirstInstance = false;

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

        /// <returns>True if this is the first instance of the application.</returns>
        public static bool InitializeAsFirstInstance()
        {
            CreateSingleInstanceMutex(out bool isFirstInstance);
            return isFirstInstance;
        }

        /// <summary>
        /// Cleans up single-instance code, clearing shared resources, mutexes, etc.
        /// </summary>
        public static void Cleanup()
        {
            SingleInstanceMutex?.ReleaseMutex();
        }
    }
}
