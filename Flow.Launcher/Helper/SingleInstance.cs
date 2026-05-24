using System;
using System.Threading;

namespace Flow.Launcher.Helper;

/// <summary>
/// Helper to ensure we are the only running instance of Flow Launcher.
/// </summary>
public static class SingleInstance
{
    private const string MutexName = @"Local\Flow.Launcher_Unique_Application_Mutex_";
    private static Mutex? _mutex;
    private static bool _hasOwnership;

    /// <summary>
    /// Ensures we are the only running instance of Flow Launcher.
    /// </summary>
    /// <param name="waitIfOccupied">Whether to wait if there is another instance running.</param>
    /// <returns>True if we are able to secure the instance lock (and thus we are the only running instance).</returns>
    public static bool Initialize(bool waitIfOccupied)
    {
        string identifier = MutexName + Environment.UserName;
        _mutex = new Mutex(initiallyOwned: true, identifier, out bool isFirstInstance);

        if (isFirstInstance)
        {
            _hasOwnership = true;
            return true;
        }

        // Block until the old instace shuts down and releases the mutex
        if (waitIfOccupied)
        {
            try
            {
                // Wait up to 5 seconds for the old instance to cleanly exit
                if (_mutex.WaitOne(TimeSpan.FromSeconds(5)))
                {
                    _hasOwnership = true;
                    return true;
                }
            }
            catch (AbandonedMutexException)
            {
                // The previous instance crashed/terminated abruptly but we now own the mutex
                _hasOwnership = true;
                return true;
            }
        }

        // Failed to secure the instance lock
        Cleanup();
        return false;
    }

    /// <summary>
    /// Releases the instance lock so that another instance of Flow Launcher can be started.
    /// </summary>
    public static void Cleanup()
    {
        if (_mutex is not null)
        {
            if (_hasOwnership)
                _mutex.ReleaseMutex();

            _mutex.Dispose();
            _mutex = null;
            _hasOwnership = false;
        }
    }
}
