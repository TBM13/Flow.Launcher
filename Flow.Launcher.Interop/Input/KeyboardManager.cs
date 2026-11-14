using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Flow.Launcher.PluginSDK.Hotkeys;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Interop.Input;

/// <summary>
/// Manages global keyboard hooks to detect hotkeys, pressed keys, etc.
/// </summary>
/// <remarks>Only one instance of this class can be active at a time.</remarks>
public sealed class KeyboardManager : IDisposable
{
    /// <summary>
    /// Invoked when a hotkey is triggered.
    /// <para/>
    /// If the subscriber returns true, the KeyUp event from the hotkey's last pressed key
    /// will be blocked from reaching the OS and other apps.
    /// </summary>
    /// <remarks>
    /// Invoked from the hook callback directly, so subscribers must execute as quick as possible.
    /// </remarks>
    public event Func<Hotkey, bool>? OnHotkeyTriggered;

    private static KeyboardManager? _activeInstance;
    private uint _hookThreadId;
    private readonly ManualResetEventSlim _threadInitSignal = new();
    private Exception? _threadInitException;
    private const nuint SIMULATED_KEY_SIGNATURE = 0x4067AA5;

    private PressedKeys _pressedKeys, _lastHotkeyKeys;
    private bool _modifierPressedAfterKey;
    private readonly Lock _pressedKeysLock = new();
    private ManagerState _state;

    private enum ManagerState
    {
        Uninitialized,
        Initializing,
        Initialized,
        Disposed
    }

    public KeyboardManager()
    {
        if (Interlocked.CompareExchange(ref _activeInstance, this, null) is not null)
            throw new InvalidOperationException(
                $"Only one instance of {nameof(KeyboardManager)} can be active at a time");
    }

    /// <summary>
    /// Starts the keyboard hook on a dedicated thread.
    /// </summary>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="InvalidOperationException"/>
    /// <exception cref="Win32Exception"/>
    public void Initialize()
    {
        ManagerState prevState = Interlocked.CompareExchange(
            ref _state, ManagerState.Initializing, ManagerState.Uninitialized);

        if (prevState == ManagerState.Disposed)
        {
            Interlocked.CompareExchange(ref _activeInstance, null, this);
            throw new ObjectDisposedException(nameof(KeyboardManager));
        }
        if (prevState != ManagerState.Uninitialized)
            throw new InvalidOperationException("Already initialized");

        // Run the hook on a dedicated thread so we don't
        // miss any key events when the main thread is busy
        Thread hookThread = new(HookThreadProc)
        {
            Name = "GlobalHotkeyHookThread",
            IsBackground = true
        };
        hookThread.Start();

        // Wait for the thread to initialize the hook so we can throw an error if it failed
        // and to prevent a race condition if Dispose() is called before the thread finishes starting
        _threadInitSignal.Wait();

        if (_threadInitException is not null)
        {
            Dispose();
            throw _threadInitException;
        }

        // Transition to Initialized unless Dispose was called while we were waiting
        if (Interlocked.CompareExchange(
            ref _state, ManagerState.Initialized, ManagerState.Initializing) == ManagerState.Disposed)
            StopHookThread();
    }

    private unsafe void HookThreadProc()
    {
        UnhookWindowsHookExSafeHandle? hookId = null;
        try
        {
            // Force creation of the thread's message queue before signaling completion
            // so Dispose() cannot race and fail
            _ = PInvoke.PeekMessage(out _, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
            Volatile.Write(ref _hookThreadId, PInvoke.GetCurrentThreadId());

            // Low-level hooks such as WH_KEYBOARD_LL are guaranteed
            // to execute sequentially on the current thread
            hookId = PInvoke.SetWindowsHookEx(
                WINDOWS_HOOK_ID.WH_KEYBOARD_LL, &StaticHookCallback, PInvoke.GetModuleHandle(null), 0);

            if (hookId.IsInvalid)
                throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _hookThreadId, 0);
            hookId?.Dispose();
            _threadInitException = ex;
            return;
        }
        finally
        {
            _threadInitSignal.Set();
        }

        // The hook requires a message loop
        try
        {
            while (PInvoke.GetMessage(out MSG msg, HWND.Null, 0, 0).Value > 0)
            {
                PInvoke.TranslateMessage(in msg);
                PInvoke.DispatchMessage(in msg);
            }
        }
        finally
        {
            // Unhook
            hookId.Dispose();
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static LRESULT StaticHookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0 && Volatile.Read(ref _activeInstance) is { } instance)
        {
            try
            {
                return instance.HookCallback(nCode, wParam, lParam);
            }
            catch { }
        }

        // Let the next hook in the chain receive the key event (OS, some other app, etc.)
        return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);
    }

    private LRESULT HookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        uint vkCode;
        bool isSimulatedKeyEvent;
        unsafe
        {
            KBDLLHOOKSTRUCT* kbdData = (KBDLLHOOKSTRUCT*)lParam.Value;
            vkCode = kbdData->vkCode;

            // If the event has our signature, we sent it ourselves so skip processing it
            isSimulatedKeyEvent = kbdData->dwExtraInfo == SIMULATED_KEY_SIGNATURE;
        }

        Key key = KeyInterop.KeyFromVirtualKey((int)vkCode);
        if (key == Key.None || isSimulatedKeyEvent)
            return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);

        uint msg = (uint)wParam.Value;
        if (msg is PInvoke.WM_KEYDOWN or PInvoke.WM_SYSKEYDOWN)
        {
            lock (_pressedKeysLock)
            {
                // Since it is normal for us to miss KEYUP events of modifiers
                // (e.g. when pressing CTRL+ALT+SUPR), lets ensure that our state
                // matches their physical press state
                ReadOnlySpan<int> modifierVKs = [
                    (int)VIRTUAL_KEY.VK_LSHIFT,
                    (int)VIRTUAL_KEY.VK_RSHIFT,
                    (int)VIRTUAL_KEY.VK_LCONTROL,
                    (int)VIRTUAL_KEY.VK_RCONTROL,
                    (int)VIRTUAL_KEY.VK_LMENU,
                    (int)VIRTUAL_KEY.VK_RMENU,
                    (int)VIRTUAL_KEY.VK_LWIN,
                    (int)VIRTUAL_KEY.VK_RWIN
                ];
                bool invalidState = false;
                foreach (int vk in modifierVKs)
                {
                    // If modifier is pressed but physically up, our state is invalid
                    if (_pressedKeys.KeyPressed((uint)vk) && (PInvoke.GetAsyncKeyState(vk) & 0x8000) == 0)
                    {
                        _pressedKeys = _pressedKeys.ReleaseKey((uint)vk);
                        invalidState = true;
                    }
                }

                // If a modifier's state was desynced and there are still keys pressed, lets reset
                if (invalidState && _pressedKeys.PressedCount > 0)
                {
                    _pressedKeys = default;
                    _lastHotkeyKeys = default;
                    _modifierPressedAfterKey = false;
                    return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);
                }

                // Windows sends continuous KEYDOWN events when a non-modifier key is held down
                if (_pressedKeys.KeyPressed(vkCode))
                    return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);

                _pressedKeys = _pressedKeys.PressKey(vkCode);
                if (key.IsModifierKey() && _pressedKeys.PressedModifiersCount < _pressedKeys.PressedCount)
                    _modifierPressedAfterKey = true;

                // Whatever key from the previous hotkey is still being pressed
                // will now be part of a new hotkey
                _lastHotkeyKeys = default;
            }
        }
        else if (msg is PInvoke.WM_KEYUP or PInvoke.WM_SYSKEYUP)
        {
            Hotkey hotkey;
            lock (_pressedKeysLock)
            {
                // Check if our state is valid
                if (!_pressedKeys.KeyPressed(vkCode))
                {
                    // We missed the key down event for this key. Lets reset our state and ignore
                    _pressedKeys = default;
                    _lastHotkeyKeys = default;
                    _modifierPressedAfterKey = false;

                    return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);
                }

                hotkey = _pressedKeys.ToHotkey();
                bool wasModifierPressedAfterKey = _modifierPressedAfterKey;

                // Update our state
                _pressedKeys = _pressedKeys.ReleaseKey(vkCode);
                if (_pressedKeys.PressedModifiersCount == _pressedKeys.PressedCount)
                    // Released all non-modifier keys
                    _modifierPressedAfterKey = false;

                // If we are releasing a key from the last hotkey, ignore this event
                if (_lastHotkeyKeys.KeyPressed(vkCode))
                {
                    _lastHotkeyKeys = _lastHotkeyKeys.ReleaseKey(vkCode);
                    return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);
                }

                // The key must be pressed after all the modifiers. E.g: Shift+K+Alt is not a hotkey
                // The hotkey must also be valid and contain at least one modifier
                if (wasModifierPressedAfterKey || !hotkey.IsValid || hotkey.Modifiers == ModifierKeys.None)
                    return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);

                _lastHotkeyKeys = _pressedKeys;
            }

            bool block = OnHotkeyTriggered?.Invoke(hotkey) ?? false;
            if (block)
                // Block the key event from reaching the OS and other apps
                return new LRESULT(1);
        }

        // Let the next hook in the chain receive the key event (OS, some other app, etc.)
        return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);
    }

    /// <summary>
    /// Sends the key event for the specified key to the OS, without processing it ourselves.
    /// </summary>
    public static void SimulateKeyEvent(Key key, bool isKeyUp)
    {
        int vKey = KeyInterop.VirtualKeyFromKey(key);
        INPUT input = new()
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = new INPUT._Anonymous_e__Union
            {
                ki = new()
                {
                    wVk = (VIRTUAL_KEY)vKey,
                    wScan = 0,
                    dwFlags = isKeyUp ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : default,
                    dwExtraInfo = SIMULATED_KEY_SIGNATURE
                }
            }
        };

        unsafe
        {
            _ = PInvoke.SendInput(1, &input, sizeof(INPUT));
        }
    }

    /// <summary>
    /// Gets the keys that are currently pressed (down).
    /// </summary>
    public PressedKeys GetPressedKeys()
    {
        lock (_pressedKeysLock)
            return _pressedKeys;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _state, ManagerState.Disposed) == ManagerState.Disposed)
            return;

        Interlocked.CompareExchange(ref _activeInstance, null, this);
        StopHookThread();
    }

    private void StopHookThread()
    {
        // Tell the hook thread's message loop to wake up and exit
        uint threadId = Interlocked.Exchange(ref _hookThreadId, 0);
        if (threadId != 0)
            PInvoke.PostThreadMessage(threadId, PInvoke.WM_QUIT, default, default);
    }
}
