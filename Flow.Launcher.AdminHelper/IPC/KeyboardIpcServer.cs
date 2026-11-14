using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows.Input;
using Flow.Launcher.Interop.Input;
using Flow.Launcher.PluginSDK.Hotkeys;

namespace Flow.Launcher.AdminHelper.IPC;

public sealed class KeyboardIpcServer : IDisposable
{
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly KeyboardManager _keyboardManager = new();
    private readonly Channel<string> _outboundChannel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });

    private int _nextRequestId;
    private volatile bool _blockWindowsKey;

    public KeyboardIpcServer(NamedPipeServerStream pipe)
    {
        _reader = new StreamReader(pipe, leaveOpen: true);
        _writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        _keyboardManager.OnHotkeyTriggered += OnHotkeyTriggered;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _keyboardManager.Initialize();

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task writeLoopTask = WriteOutboundLoopAsync(linkedCts.Token);

        try
        {
            while (await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                KeyboardIpcMessage? message;
                try
                {
                    message = KeyboardIpcProtocol.Deserialize(line);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (message is null)
                    continue;

                switch (message.Type)
                {
                    case KeyboardIpcMessageType.GetPressedKeys:
                        EnqueueMessage(KeyboardIpcProtocol.CreatePressedKeysResult(
                            message.RequestId, _keyboardManager.GetPressedKeys()));
                        break;

                    case KeyboardIpcMessageType.BlockWindowsKey:
                        _blockWindowsKey = true;
                        break;
                    case KeyboardIpcMessageType.DontBlockWindowsKey:
                        _blockWindowsKey = false;
                        break;
                }
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) { }
        finally
        {
            _outboundChannel.Writer.TryComplete();
            await linkedCts.CancelAsync().ConfigureAwait(false);
            try
            {
                await writeLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }
    }

    private async Task WriteOutboundLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (string json in _outboundChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await _writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // Client disconnected
        }
    }

    private bool OnHotkeyTriggered(Hotkey hotkey)
    {
        int requestId = Interlocked.Increment(ref _nextRequestId);
        EnqueueMessage(new KeyboardIpcMessage
        {
            Type = KeyboardIpcMessageType.HotkeyTriggered,
            RequestId = requestId,
            MainKey = (int)hotkey.MainKey,
            Modifiers = (int)hotkey.Modifiers
        });

        // Block the start menu from opening
        if (_blockWindowsKey && hotkey.MainKey == Key.None && hotkey.Modifiers == ModifierKeys.Windows)
        {
            // Since we blocked the Windows key's KEYUP event, the OS will think it's still being
            // held down and will trigger respective shortcuts when another key is pressed.
            // To prevent this, simulate Alt Key press to trigger Windows + Alt shortcut (which does nothing).
            KeyboardManager.SimulateKeyEvent(Key.LeftAlt, isKeyUp: false);

            // Simulate release of both the Windows and Alt keys
            // TODO: Support right windows key
            KeyboardManager.SimulateKeyEvent(Key.LWin, isKeyUp: true);
            KeyboardManager.SimulateKeyEvent(Key.LeftAlt, isKeyUp: true);
            return true;
        }

        return false;
    }

    private void EnqueueMessage(KeyboardIpcMessage message)
    {
        string json = KeyboardIpcProtocol.Serialize(message);
        _outboundChannel.Writer.TryWrite(json);
    }

    public void Dispose()
    {
        _keyboardManager.OnHotkeyTriggered -= OnHotkeyTriggered;
        _keyboardManager.Dispose();
        _reader.Dispose();
        _writer.Dispose();
    }
}
