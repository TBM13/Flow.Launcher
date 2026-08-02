using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Flow.Launcher.Interop.Hardware;

/// <summary>
/// Represents the information of a display monitor.
/// </summary>
public class MonitorInfo
{
    /// <exception cref="InvalidOperationException"></exception>
    internal MonitorInfo(HMONITOR monitor, HMONITOR primaryMonitor, RECT rect)
        : this(monitor, primaryMonitor, new Rect(new Point(rect.left, rect.top), new Point(rect.right, rect.bottom)))
    { }

    /// <exception cref="InvalidOperationException"></exception>
    internal MonitorInfo(HMONITOR monitor, HMONITOR primaryMonitor)
        : this(monitor, primaryMonitor, bounds: null)
    { }

    /// <exception cref="InvalidOperationException"></exception>
    private unsafe MonitorInfo(HMONITOR monitor, HMONITOR primaryMonitor, Rect? bounds)
    {
        IsPrimary = monitor == primaryMonitor;
        var info = new MONITORINFOEXW() { monitorInfo = new MONITORINFO() { cbSize = (uint)sizeof(MONITORINFOEXW) } };
        var res = PInvoke.GetMonitorInfo(monitor, ref info.monitorInfo);
        if (!res)
            throw new InvalidOperationException("Failed to get monitor info");

        Bounds = bounds ??
            new Rect(new Point(info.monitorInfo.rcMonitor.left, info.monitorInfo.rcMonitor.top),
            new Point(info.monitorInfo.rcMonitor.right, info.monitorInfo.rcMonitor.bottom));
        WorkingArea =
            new Rect(new Point(info.monitorInfo.rcWork.left, info.monitorInfo.rcWork.top),
            new Point(info.monitorInfo.rcWork.right, info.monitorInfo.rcWork.bottom));
        Name = new string(info.szDevice.AsSpan().TrimEnd('\0').Trim());
    }

    /// <summary>
    /// The name of the display.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The display monitor rectangle, expressed in virtual-screen coordinates.
    /// </summary>
    /// <remarks>
    /// If this is not the primary display monitor, some of the coordinates may be negative values.
    /// </remarks>
    public Rect Bounds { get; }

    /// <summary>
    /// The work area rectangle of the display monitor, expressed in virtual-screen coordinates.
    /// </summary>
    /// <remarks>
    /// If this is not the primary display monitor, some of the coordinates may be negative values.
    /// </remarks>
    public Rect WorkingArea { get; }

    /// <summary>
    /// Whether this is the primary display monitor.
    /// </summary>
    public bool IsPrimary { get; }

    public override string ToString() => $"{Name} {Bounds.Width}x{Bounds.Height}";
}

/// <summary>
/// Helper to get information of display monitors.
/// </summary>
/// <remarks>
/// Accessing the monitors while the user is in the lockscreen or logging-in may throw an exception.
/// </remarks>
// Based on https://github.com/Jack251970/DesktopWidgets3.
public static class MonitorHelper
{
    /// <summary>
    /// Gets all the display monitors (including invisible pseudo-monitors associated with the mirroring drivers).
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public static unsafe List<MonitorInfo> GetDisplayMonitors()
    {
        HMONITOR primaryMonitor = GetPrimaryMonitorHandle();
        List<MonitorInfo> list = [];

        var context = (list, primaryMonitor);
        GCHandle handle = GCHandle.Alloc(context);
        try
        {
            LPARAM lParam = (LPARAM)GCHandle.ToIntPtr(handle);

            bool ok = PInvoke.EnumDisplayMonitors(default, null, &EnumCallback, lParam);
            if (!ok)
                throw new InvalidOperationException("Failed to enum display monitors");
            return list;
        }
        finally
        {
            handle.Free();
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        static BOOL EnumCallback(HMONITOR monitor, HDC deviceContext, RECT* rect, LPARAM data)
        {
            try
            {
                GCHandle handle = GCHandle.FromIntPtr((nint)data);
                if (handle.Target is (List<MonitorInfo> list, HMONITOR primaryMonitor))
                {
                    list.Add(new MonitorInfo(monitor, primaryMonitor, *rect));
                }
            }
            catch
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Gets the display monitor that is nearest to a given window.
    /// </summary>
    /// <param name="hwnd">The window handle.</param>
    /// <exception cref="InvalidOperationException"></exception>
    public static MonitorInfo? GetNearestDisplayMonitor(nint hwnd)
    {
        HMONITOR targetMonitor = PInvoke.MonitorFromWindow(new(hwnd), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL);
        if (targetMonitor.IsNull)
            return null;

        return new MonitorInfo(targetMonitor, GetPrimaryMonitorHandle());
    }

    /// <summary>
    /// Gets the display monitor where the cursor is.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="Win32Exception"></exception>
    public static MonitorInfo? GetCursorDisplayMonitor()
    {
        if (!PInvoke.GetCursorPos(out var pt))
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        HMONITOR targetMonitor = PInvoke.MonitorFromPoint(pt, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL);
        if (targetMonitor.IsNull)
            return null;

        return new MonitorInfo(targetMonitor, GetPrimaryMonitorHandle());
    }

    /// <summary>
    /// Gets the primary display monitor.
    /// </summary>
    public static MonitorInfo GetPrimaryDisplayMonitor()
    {
        HMONITOR targetMonitor = GetPrimaryMonitorHandle();
        return new MonitorInfo(targetMonitor, targetMonitor);
    }

    private static HMONITOR GetPrimaryMonitorHandle() =>
        PInvoke.MonitorFromWindow(new HWND(nint.Zero), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
}
