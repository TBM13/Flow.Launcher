using System.Diagnostics.CodeAnalysis;

namespace Flow.Launcher.AdminHelper.IPC;

/// <summary>
/// Uses a named Mutex to ensure that only one instance of an application runs at a time.
/// </summary>
/// <remarks>
/// Must be disposed on the same thread that acquired it.
/// </remarks>
public sealed class SingleInstance : IDisposable
{
    private Mutex? _mutex;
    private readonly int _owningThreadId;

    private SingleInstance(Mutex mutex)
    {
        _mutex = mutex;
        _owningThreadId = Environment.CurrentManagedThreadId;
    }

    /// <summary>
    /// Attempts to acquire exclusive ownership of a named mutex.
    /// </summary>
    public static bool TryAcquire(
        string mutexName,
        [NotNullWhen(true)] out SingleInstance? instance,
        TimeSpan timeout = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be non-negative or InfiniteTimeSpan");

        Mutex mutex = new(initiallyOwned: false, name: mutexName);
        bool hasOwnership;
        try
        {
            hasOwnership = mutex.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            // Previous instance crashed, but we acquired ownership of the mutex
            hasOwnership = true;
        }
        catch
        {
            mutex.Dispose();
            throw;
        }

        if (!hasOwnership)
        {
            mutex.Dispose();
            instance = null;
            return false;
        }

        instance = new SingleInstance(mutex);
        return true;
    }

    public void Dispose()
    {
        if (Volatile.Read(ref _mutex) is null)
            return;

        if (Environment.CurrentManagedThreadId != _owningThreadId)
        {
            throw new InvalidOperationException(
                $"Cannot dispose {nameof(SingleInstance)} from thread {Environment.CurrentManagedThreadId}; " +
                $"it must be disposed from acquiring thread {_owningThreadId}.");
        }

        Mutex? mutex = Interlocked.Exchange(ref _mutex, null);
        if (mutex is null)
            return;

        try
        {
            mutex.ReleaseMutex();
        }
        finally
        {
            mutex.Dispose();
        }
    }
}
