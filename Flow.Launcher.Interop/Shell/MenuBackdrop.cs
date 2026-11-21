using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Interop.Shell;

/// <summary>
/// Renders native Win32 popup menus (<c>#32768</c>) with Windows 11's acrylic backdrop.
/// <para/>
/// Standard Win32 menus paint via GDI, which leaves the alpha channel zeroed on every pixel.
/// When DWM acrylic is enabled on such a window, GDI pixels turn black or invisible.
/// To fix this, each <c>#32768</c> window created on the current thread is subclassed, painted
/// off-screen via <c>WM_PRINT</c> into a 32bpp DIB, converted to premultiplied ARGB using
/// color-to-alpha against the menu's background color, and blitted back into the window DC.
/// </summary>
internal static unsafe class MenuBackdrop
{
    private const uint Sentinel = 0x00FF00FFu;

    private sealed class MenuWindowState
    {
        public delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, LRESULT> OriginalWndProc;
        public HMENU Menu;
        public int LastHilite = int.MinValue;
        public uint BackgroundColor;
        public bool HasBackgroundColor;
        public bool BackdropApplied;
        public bool IsRendering;
        public bool Failed;
        public bool Detached;

        public HDC MemDc;
        public HBITMAP Dib;
        public HGDIOBJ OldObj;
        public uint* Bits;
        public int Width;
        public int Height;

        public bool EnsureBuffer(int width, int height)
        {
            if (Width == width && Height == height && !MemDc.IsNull && !Dib.IsNull && Bits is not null)
                return true;

            FreeBuffer();

            HDC memDc = PInvoke.CreateCompatibleDC(default);
            if (memDc.IsNull)
                return false;

            BITMAPINFO bmi = default;
            bmi.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height; // top-down DIB
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = (uint)BI_COMPRESSION.BI_RGB;

            void* bits = null;
            HBITMAP dib = PInvoke.CreateDIBSection(memDc, &bmi, DIB_USAGE.DIB_RGB_COLORS, &bits, default, 0);
            if (dib.IsNull || bits is null)
            {
                _ = PInvoke.DeleteDC(memDc);
                return false;
            }

            HGDIOBJ oldObj = PInvoke.SelectObject(memDc, dib);

            MemDc = memDc;
            Dib = dib;
            OldObj = oldObj;
            Bits = (uint*)bits;
            Width = width;
            Height = height;
            return true;
        }

        public void FreeBuffer()
        {
            if (!MemDc.IsNull)
            {
                if (!OldObj.IsNull)
                {
                    _ = PInvoke.SelectObject(MemDc, OldObj);
                    OldObj = default;
                }

                if (!Dib.IsNull)
                {
                    _ = PInvoke.DeleteObject(Dib);
                    Dib = default;
                }

                _ = PInvoke.DeleteDC(MemDc);
                MemDc = default;
            }
            else if (!Dib.IsNull)
            {
                _ = PInvoke.DeleteObject(Dib);
                Dib = default;
            }

            Bits = null;
            Width = 0;
            Height = 0;
        }
    }

    private static readonly Dictionary<HWND, MenuWindowState> _windows = [];
    private static delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, LRESULT> _classWndProc;
    private static UnhookWindowsHookExSafeHandle? _cbtHook;

    public static bool IsSupported => WindowHelper.SupportsAcrylicPopup;

    private static delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, LRESULT> OurProc => &MenuWindowProc;

    public static void Begin()
    {
        try
        {
            _cbtHook?.Dispose();
            _cbtHook = null;

            foreach (MenuWindowState state in _windows.Values)
                state.FreeBuffer();
            _windows.Clear();

            _cbtHook = PInvoke.SetWindowsHookEx(
                WINDOWS_HOOK_ID.WH_CBT,
                &CbtHook,
                null,
                PInvoke.GetCurrentThreadId());
        }
        catch
        {
        }
    }

    public static void End()
    {
        try
        {
            _cbtHook?.Dispose();
            _cbtHook = null;

            nint ourProc = (nint)OurProc;
            foreach ((HWND hwnd, MenuWindowState state) in _windows.ToArray())
            {
                state.FreeBuffer();

                if (!PInvoke.IsWindow(hwnd))
                    continue;

                if (!state.Detached && PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC) == ourProc)
                    _ = PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, (nint)state.OriginalWndProc);

                if (state.BackdropApplied && IsMenuWindowClass(hwnd))
                    WindowHelper.DisablePopupAcrylic(hwnd);
            }
        }
        catch
        {
        }
        finally
        {
            _windows.Clear();
        }
    }

    public static void RefreshAll()
    {
        try
        {
            foreach ((HWND hwnd, MenuWindowState state) in _windows.ToArray())
                RefreshIfSelectionChanged(hwnd, state);
        }
        catch
        {
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT CbtHook(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode == (int)PInvoke.HCBT_CREATEWND)
        {
            try
            {
                HWND hwnd = new((void*)wParam.Value);
                if (IsMenuWindowClass(hwnd))
                    Attach(hwnd);
            }
            catch
            {
            }
        }

        return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);
    }

    private static bool IsMenuWindowClass(HWND hwnd)
    {
        Span<char> buffer = stackalloc char[32];
        int length = PInvoke.GetClassName(hwnd, buffer);
        return length > 0 && buffer[..length].Equals("#32768", StringComparison.Ordinal);
    }

    private static void Attach(HWND hwnd)
    {
        if (_windows.TryGetValue(hwnd, out MenuWindowState? existing) && !existing.Detached)
            return;

        existing?.FreeBuffer();

        MenuWindowState state = new();
        _windows[hwnd] = state;

        nint ourProc = (nint)OurProc;
        nint orig = PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, ourProc);
        if (orig == 0)
        {
            _windows.Remove(hwnd);
            return;
        }

        if (orig == ourProc)
        {
            if (_classWndProc is not null)
                _ = PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, (nint)_classWndProc);
            _windows.Remove(hwnd);
            return;
        }

        state.OriginalWndProc = (delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, LRESULT>)orig;
        if (_classWndProc is null)
            _classWndProc = state.OriginalWndProc;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT MenuWindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, LRESULT> orig = null;

        try
        {
            if (!_windows.TryGetValue(hwnd, out MenuWindowState? state) || state.OriginalWndProc is null)
                return _classWndProc is not null ? PInvoke.CallWindowProc(_classWndProc, hwnd, msg, wParam, lParam) : default;

            orig = state.OriginalWndProc;

            if (msg == PInvoke.WM_NCDESTROY)
            {
                RestoreOnDestroy(hwnd, state, orig);
                return PInvoke.CallWindowProc(orig, hwnd, msg, wParam, lParam);
            }

            if (msg == PInvoke.WM_WINDOWPOSCHANGED)
            {
                LRESULT result = PInvoke.CallWindowProc(orig, hwnd, msg, wParam, lParam);
                HandleWindowPosChanged(hwnd, state, orig, lParam);
                return result;
            }

            if (state.Failed)
                return PInvoke.CallWindowProc(orig, hwnd, msg, wParam, lParam);

            switch (msg)
            {
                case PInvoke.WM_ERASEBKGND:
                    return new LRESULT(1);

                case PInvoke.WM_NCPAINT:
                    if (TryRender(hwnd, state))
                        return default;
                    return PInvoke.CallWindowProc(orig, hwnd, msg, wParam, lParam);

                case PInvoke.WM_PAINT:
                    HandlePaint(hwnd, state);
                    return default;

                default:
                    LRESULT result = PInvoke.CallWindowProc(orig, hwnd, msg, wParam, lParam);
                    if (!state.IsRendering && !state.Detached)
                        RefreshIfSelectionChanged(hwnd, state);
                    return result;
            }
        }
        catch
        {
            delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, LRESULT> fallback = orig is not null ? orig : _classWndProc;
            return fallback is not null ? PInvoke.CallWindowProc(fallback, hwnd, msg, wParam, lParam) : default;
        }
    }

    private static void RestoreOnDestroy(
        HWND hwnd,
        MenuWindowState state,
        delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, LRESULT> orig)
    {
        try
        {
            state.FreeBuffer();
            _ = PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, (nint)orig);
            _windows.Remove(hwnd);
        }
        catch
        {
        }
    }

    private static void HandleWindowPosChanged(
        HWND hwnd,
        MenuWindowState state,
        delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, LRESULT> orig,
        LPARAM lParam)
    {
        try
        {
            if (lParam.Value == 0)
                return;

            WINDOWPOS* wp = (WINDOWPOS*)lParam.Value;
            if ((wp->flags & SET_WINDOW_POS_FLAGS.SWP_HIDEWINDOW) != 0)
            {
                // Restore immediately on hide so a cached desktop #32768 window
                // can never retain a wndproc pointer into our process.
                state.FreeBuffer();
                _ = PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, (nint)orig);
                state.Detached = true;
            }
        }
        catch
        {
        }
    }

    private static void HandlePaint(HWND hwnd, MenuWindowState state)
    {
        try
        {
            PAINTSTRUCT ps = default;
            HDC hdc = PInvoke.BeginPaint(hwnd, &ps);
            if (!hdc.IsNull)
                _ = PInvoke.EndPaint(hwnd, &ps);

            if (!TryRender(hwnd, state))
            {
                _ = PInvoke.RedrawWindow(
                    hwnd,
                    (RECT*)null,
                    HRGN.Null,
                    REDRAW_WINDOW_FLAGS.RDW_INVALIDATE
                    | REDRAW_WINDOW_FLAGS.RDW_FRAME
                    | REDRAW_WINDOW_FLAGS.RDW_ERASE);
            }
        }
        catch
        {
        }
    }

    private static int GetHiliteIndex(HWND hwnd, MenuWindowState state)
    {
        if (state.Menu.IsNull)
        {
            LRESULT menuResult = PInvoke.CallWindowProc(state.OriginalWndProc, hwnd, PInvoke.MN_GETHMENU, default, default);
            state.Menu = new(menuResult.Value);
        }

        if (state.Menu.IsNull)
            return -1;

        int count = PInvoke.GetMenuItemCount(state.Menu);
        if (count <= 0)
            return -1;

        int last = state.LastHilite;
        if ((uint)last < (uint)count && IsItemHilited(state.Menu, (uint)last))
            return last;

        for (int i = 0; i < count; i++)
        {
            if (i != last && IsItemHilited(state.Menu, (uint)i))
                return i;
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsItemHilited(HMENU menu, uint index)
    {
        uint itemState = PInvoke.GetMenuState(menu, index, MENU_ITEM_FLAGS.MF_BYPOSITION);
        return itemState != uint.MaxValue && (itemState & (uint)MENU_ITEM_FLAGS.MF_HILITE) != 0;
    }

    private static void RefreshIfSelectionChanged(HWND hwnd, MenuWindowState state)
    {
        try
        {
            if (state.Detached || state.Failed || state.IsRendering || !PInvoke.IsWindowVisible(hwnd))
                return;

            if (GetHiliteIndex(hwnd, state) != state.LastHilite)
                _ = TryRender(hwnd, state);
        }
        catch
        {
        }
    }

    private static bool Fail(HWND hwnd, MenuWindowState state)
    {
        state.Failed = true;
        state.FreeBuffer();
        if (state.BackdropApplied)
            WindowHelper.DisablePopupAcrylic(hwnd);
        return false;
    }

    private static bool TryRender(HWND hwnd, MenuWindowState state)
    {
        if (state.IsRendering)
            return true;

        try
        {
            state.IsRendering = true;
            state.LastHilite = GetHiliteIndex(hwnd, state);

            RECT rect;
            if (!PInvoke.GetWindowRect(hwnd, &rect))
                return Fail(hwnd, state);

            int width = rect.right - rect.left;
            int height = rect.bottom - rect.top;
            if (width <= 0 || height <= 0)
                return true;

            if (!state.EnsureBuffer(width, height))
                return Fail(hwnd, state);

            int pixelCount = checked(width * height);
            Span<uint> pixels = new(state.Bits, pixelCount);
            pixels.Fill(Sentinel);

            LPARAM printFlags = new(PInvoke.PRF_NONCLIENT | PInvoke.PRF_CLIENT | PInvoke.PRF_ERASEBKGND | PInvoke.PRF_CHILDREN);
            _ = PInvoke.CallWindowProc(state.OriginalWndProc, hwnd, PInvoke.WM_PRINT, new((nuint)state.MemDc.Value), printFlags);
            _ = PInvoke.GdiFlush();

            if (!state.HasBackgroundColor)
            {
                if (!TryFindBackgroundColor(pixels, out uint detectedBg))
                    return true;

                state.BackgroundColor = detectedBg;
                state.HasBackgroundColor = true;
            }

            uint bg = state.BackgroundColor;

            if (!state.BackdropApplied)
            {
                WindowHelper.EnablePopupAcrylic(hwnd, IsDark(bg));
                state.BackdropApplied = true;
            }

            ConvertToPremultiplied(pixels, bg);

            HDC windowDc = PInvoke.GetWindowDC(hwnd);
            if (windowDc.IsNull)
                return Fail(hwnd, state);

            try
            {
                if (!PInvoke.BitBlt(windowDc, 0, 0, width, height, state.MemDc, 0, 0, ROP_CODE.SRCCOPY))
                    return Fail(hwnd, state);
            }
            finally
            {
                _ = PInvoke.ReleaseDC(hwnd, windowDc);
            }

            return true;
        }
        catch
        {
            return Fail(hwnd, state);
        }
        finally
        {
            state.IsRendering = false;
        }
    }

    private static bool TryFindBackgroundColor(ReadOnlySpan<uint> pixels, out uint backgroundColor)
    {
        Dictionary<uint, int> counts = [];
        int i = 0;
        while (i < pixels.Length)
        {
            uint rgb = pixels[i] & 0x00FFFFFFu;
            int runStart = i;
            i++;
            while (i < pixels.Length && (pixels[i] & 0x00FFFFFFu) == rgb)
                i++;

            if (rgb == Sentinel)
                continue;

            ref int count = ref CollectionsMarshal.GetValueRefOrAddDefault(counts, rgb, out _);
            count += i - runStart;
        }

        uint bestColor = 0;
        int bestCount = 0;
        foreach ((uint color, int count) in counts)
        {
            if (count > bestCount)
            {
                bestCount = count;
                bestColor = color;
            }
        }

        backgroundColor = bestColor;
        return bestCount > 0;
    }

    private static bool IsDark(uint bg)
    {
        int r = (int)((bg >> 16) & 0xFF);
        int g = (int)((bg >> 8) & 0xFF);
        int b = (int)(bg & 0xFF);
        return (299 * r + 587 * g + 114 * b) / 1000 < 128;
    }

    private static void ConvertToPremultiplied(Span<uint> pixels, uint bg)
    {
        int br = (int)((bg >> 16) & 0xFF);
        int bgG = (int)((bg >> 8) & 0xFF);
        int bb = (int)(bg & 0xFF);

        uint lastIn = Sentinel;
        uint lastOut = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            uint c = pixels[i] & 0x00FFFFFFu;
            if (c == bg || c == Sentinel)
            {
                pixels[i] = 0;
                continue;
            }

            if (c == lastIn)
            {
                pixels[i] = lastOut;
                continue;
            }

            int r = (int)((c >> 16) & 0xFF);
            int g = (int)((c >> 8) & 0xFF);
            int b = (int)(c & 0xFF);

            int a = Math.Max(AlphaFor(r, br), Math.Max(AlphaFor(g, bgG), AlphaFor(b, bb)));
            uint result;
            if (a == 0)
            {
                result = 0;
            }
            else
            {
                int inv = 255 - a;
                int pr = Math.Clamp(r - (inv * br + 127) / 255, 0, a);
                int pg = Math.Clamp(g - (inv * bgG + 127) / 255, 0, a);
                int pb = Math.Clamp(b - (inv * bb + 127) / 255, 0, a);

                result = (uint)((a << 24) | (pr << 16) | (pg << 8) | pb);
            }

            lastIn = c;
            lastOut = result;
            pixels[i] = result;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int AlphaFor(int c, int k)
    {
        if (c > k)
            return ((c - k) * 255 + (255 - k) - 1) / (255 - k);
        if (c < k)
            return ((k - c) * 255 + k - 1) / k;
        return 0;
    }
}
