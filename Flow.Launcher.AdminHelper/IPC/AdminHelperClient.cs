using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows.Input;
using Flow.Launcher.Interop.Input;
using Flow.Launcher.PluginSDK.Hotkeys;

namespace Flow.Launcher.AdminHelper.IPC;

public sealed class AdminHelperClient : IDisposable
{
    private readonly ConcurrentDictionary<int, TaskCompletionSource<PressedKeys>> _pendingPressedKeys = new();
    private readonly Lock _writeLock = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private int _nextRequestId;
    private bool _initialized;
    private volatile bool _isDisposed;

    /// <summary>
    /// Invoked when a hotkey is triggered.
    /// </summary>
    /// <remarks>Subscribers should catch and log exceptions.</remarks>
    public event Action<Hotkey>? OnHotkeyTriggered;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_initialized)
            throw new InvalidOperationException("Already initialized");

        NamedPipeClientStream pipe = new(
            ".", AdminHelperProtocol.GetCurrentUserPipeName(), PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(TimeSpan.FromSeconds(7), cancellationToken);
        }
        catch
        {
            pipe.Dispose();
            throw;
        }

        _pipe = pipe;
        _reader = new StreamReader(pipe, leaveOpen: true);
        _writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        _initialized = true;
        _ = Task.Run(ReadMessagesAsync);
    }

    private async Task ReadMessagesAsync()
    {
        try
        {
            while (await _reader!.ReadLineAsync(_cancellationTokenSource.Token).ConfigureAwait(false) is { } line)
            {
                AdminHelperMessage? message;
                try
                {
                    message = AdminHelperProtocol.Deserialize(line);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (message is null)
                    continue;

                switch (message.Type)
                {
                    case AdminHelperMessageType.HotkeyTriggered:
                        Hotkey hotkey = new((Key)message.MainKey, (ModifierKeys)message.Modifiers);
                        OnHotkeyTriggered?.Invoke(hotkey);
                        break;

                    case AdminHelperMessageType.PressedKeysResult:
                        if (_pendingPressedKeys.TryRemove(message.RequestId, out TaskCompletionSource<PressedKeys>? result))
                        {
                            PressedKeys pressedKeys;
                            try
                            {
                                pressedKeys = AdminHelperProtocol.GetPressedKeys(message);
                            }
                            catch (InvalidDataException ex)
                            {
                                result.TrySetException(ex);
                                break;
                            }

                            result.TrySetResult(pressedKeys);
                        }

                        break;
                }
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) { }
        finally
        {
            IOException exception = new("AdminHelper connection was closed");
            foreach (TaskCompletionSource<PressedKeys> pending in _pendingPressedKeys.Values)
                pending.TrySetException(exception);

            _pendingPressedKeys.Clear();
        }
    }

    /// <inheritdoc cref="KeyboardManager.GetPressedKeys"/>
    public async Task<PressedKeys> GetPressedKeysAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (!_initialized)
            throw new InvalidOperationException("Not initialized");

        int requestId = Interlocked.Increment(ref _nextRequestId);
        TaskCompletionSource<PressedKeys> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingPressedKeys[requestId] = result;

        try
        {
            SendMessage(new AdminHelperMessage
            {
                Type = AdminHelperMessageType.GetPressedKeys,
                RequestId = requestId
            });

            return await result.Task
                .WaitAsync(TimeSpan.FromSeconds(3), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _pendingPressedKeys.TryRemove(requestId, out _);
        }
    }

    /// <summary>
    /// Sets whether the Windows key should be blocked to prevent the start menu from opening.
    /// </summary>
    public void SetBlockWindowsKey(bool block)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (!_initialized)
            throw new InvalidOperationException("Not initialized");

        SendMessage(new AdminHelperMessage
        {
            Type = block ? AdminHelperMessageType.BlockWindowsKey : AdminHelperMessageType.DontBlockWindowsKey
        });
    }

    /// <summary>
    /// Registers or unregisters the AdminHelper scheduled task.
    /// </summary>
    public void SetScheduledTask(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (!_initialized)
            throw new InvalidOperationException("Not initialized");

        SendMessage(new AdminHelperMessage
        {
            Type = enabled ? AdminHelperMessageType.RegisterScheduledTask : AdminHelperMessageType.UnregisterScheduledTask
        });
    }

    private void SendMessage(AdminHelperMessage message)
    {
        string json = AdminHelperProtocol.Serialize(message);
        lock (_writeLock)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _writer!.WriteLine(json);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, true))
            return;

        // No need to dispose reader & writer since they were created
        // with leaveOpen: true and disposing them could cause issues

        _cancellationTokenSource.Cancel();
        _pipe?.Dispose();
        _cancellationTokenSource.Dispose();
    }
}
