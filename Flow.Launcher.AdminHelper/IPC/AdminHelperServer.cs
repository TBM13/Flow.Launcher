using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows.Input;
using Flow.Launcher.Interop.Input;
using Flow.Launcher.Interop.Programs;
using Flow.Launcher.PluginSDK.Hotkeys;

namespace Flow.Launcher.AdminHelper.IPC;

public sealed class AdminHelperServer : IDisposable
{
    private readonly uint _clientProcessId;
    private readonly SecurityIdentifier _clientSid;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly KeyboardManager _keyboardManager = new();
    private readonly Channel<string> _outboundChannel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });

    private int _nextRequestId;
    private volatile bool _blockWindowsKey;

    public AdminHelperServer(NamedPipeServerStream pipe, uint clientProcessId, SecurityIdentifier clientSid)
    {
        _clientProcessId = clientProcessId;
        _clientSid = clientSid;
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
                    case AdminHelperMessageType.GetPressedKeys:
                        EnqueueMessage(AdminHelperProtocol.CreatePressedKeysResult(
                            message.RequestId, _keyboardManager.GetPressedKeys()));
                        break;

                    case AdminHelperMessageType.BlockWindowsKey:
                        _blockWindowsKey = true;
                        break;
                    case AdminHelperMessageType.DontBlockWindowsKey:
                        _blockWindowsKey = false;
                        break;

                    case AdminHelperMessageType.RegisterScheduledTask:
                        using (WindowsIdentity currentIdentity = WindowsIdentity.GetCurrent())
                        {
                            if (currentIdentity.User == _clientSid)
                                AdminHelperTask.TryRegister(_clientSid);
                        }
                        break;
                    case AdminHelperMessageType.UnregisterScheduledTask:
                        AdminHelperTask.TryDelete(_clientSid);
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
        bool isWindowsKey = _blockWindowsKey
            && hotkey.MainKey == Key.None
            && hotkey.Modifiers == ModifierKeys.Windows;

        if (isWindowsKey)
        {
            // Since we will block the KEYUP event, the OS will think it's still being
            // held down and will trigger respective shortcuts when another key is pressed.
            // To prevent this, simulate Alt Key press to trigger Windows + Alt shortcut (which does nothing).
            KeyboardManager.SimulateKeyEvent(Key.LeftAlt, isKeyUp: false);

            // Simulate release of both the Windows and Alt keys
            // TODO: Support right windows key
            KeyboardManager.SimulateKeyEvent(Key.LWin, isKeyUp: true);
            KeyboardManager.SimulateKeyEvent(Key.LeftAlt, isKeyUp: true);
        }

        // Grant the client the ability to set the foreground window in case
        // it wants to bring its own window to the front in response to the hotkey.
        ProcessHelper.AllowSetForegroundWindow(_clientProcessId);

        int requestId = Interlocked.Increment(ref _nextRequestId);
        EnqueueMessage(new AdminHelperMessage
        {
            Type = AdminHelperMessageType.HotkeyTriggered,
            RequestId = requestId,
            MainKey = (int)hotkey.MainKey,
            Modifiers = (int)hotkey.Modifiers
        });

        // Block the Windows key's KEYUP event so the start menu doesn't open
        return isWindowsKey;
    }

    private void EnqueueMessage(AdminHelperMessage message)
    {
        string json = AdminHelperProtocol.Serialize(message);
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
