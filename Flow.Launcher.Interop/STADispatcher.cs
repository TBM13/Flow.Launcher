using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Interop;

internal static class STADispatcher
{
    private const uint WorkAvailableMessage = 0x8000;
    private static readonly ConcurrentQueue<Action> Queue = [];
    private static readonly ManualResetEventSlim WorkerReady = new();
    private static readonly Thread WorkerThread = CreateWorkerThread();
    private static uint _workerThreadId;

    public static Task<T> InvokeAsync<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Environment.CurrentManagedThreadId == WorkerThread.ManagedThreadId)
        {
            try
            {
                return Task.FromResult(action());
            }
            catch (Exception exception)
            {
                return Task.FromException<T>(exception);
            }
        }

        WorkerReady.Wait();

        TaskCompletionSource<T> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Queue.Enqueue(() =>
        {
            try
            {
                completionSource.TrySetResult(action());
            }
            catch (Exception exception)
            {
                completionSource.TrySetException(exception);
            }
        });

        if (PInvoke.PostThreadMessage(_workerThreadId, WorkAvailableMessage, default, default).Value == 0)
            completionSource.TrySetException(new Win32Exception(Marshal.GetLastPInvokeError()));

        return completionSource.Task;
    }

    private static Thread CreateWorkerThread()
    {
        Thread thread = new(Run)
        {
            IsBackground = true,
            Name = nameof(STADispatcher),
            Priority = ThreadPriority.Normal
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return thread;
    }

    private static void Run()
    {
        bool oleInitialized = false;

        try
        {
            PInvoke.OleInitialize().ThrowOnFailure();
            oleInitialized = true;

            _workerThreadId = PInvoke.GetCurrentThreadId();
            PInvoke.PeekMessage(out _, HWND.Null, 0, 0, 0);
            WorkerReady.Set();

            while (PInvoke.GetMessage(out MSG message, HWND.Null, 0, 0).Value > 0)
            {
                if (message.message == WorkAvailableMessage)
                {
                    while (Queue.TryDequeue(out Action? action))
                        action();
                }
                else
                {
                    PInvoke.TranslateMessage(in message);
                    PInvoke.DispatchMessage(in message);
                }
            }
        }
        finally
        {
            WorkerReady.Set();
            if (oleInitialized)
                PInvoke.OleUninitialize();
        }
    }
}
