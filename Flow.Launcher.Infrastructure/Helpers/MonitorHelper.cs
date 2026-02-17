using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Infrastructure.Helpers;

public class MonitorInfo
{
    internal unsafe MonitorInfo(HMONITOR monitor, HMONITOR primaryMonitor, RECT* rect)
        : this(monitor, primaryMonitor, new Rect(new Point(rect->left, rect->top), new Point(rect->right, rect->bottom)))
    { }

    internal MonitorInfo(HMONITOR monitor, HMONITOR primaryMonitor)
        : this(monitor, primaryMonitor, bounds: null)
    { }

    private unsafe MonitorInfo(HMONITOR monitor, HMONITOR primaryMonitor, Rect? bounds)
    {
        IsPrimary = monitor == primaryMonitor;
        var info = new MONITORINFOEXW() { monitorInfo = new MONITORINFO() { cbSize = (uint)sizeof(MONITORINFOEXW) } };
        var res = PInvoke.GetMonitorInfo(monitor, ref info.monitorInfo);
        if (!res)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        Bounds = bounds ??
            new Rect(new Point(info.monitorInfo.rcMonitor.left, info.monitorInfo.rcMonitor.top),
            new Point(info.monitorInfo.rcMonitor.right, info.monitorInfo.rcMonitor.bottom));
        WorkingArea =
            new Rect(new Point(info.monitorInfo.rcWork.left, info.monitorInfo.rcWork.top),
            new Point(info.monitorInfo.rcWork.right, info.monitorInfo.rcWork.bottom));
        Name = new string(info.szDevice.AsSpan()).TrimEnd('\0').Trim();
    }

    /// <summary>
    /// Gets the name of the display.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the display monitor rectangle, expressed in virtual-screen coordinates.
    /// </summary>
    /// <remarks>
    /// <note>If the monitor is not the primary display monitor, some of the rectangle's coordinates may be negative values.</note>
    /// </remarks>
    public Rect Bounds { get; }

    /// <summary>
    /// Gets the work area rectangle of the display monitor, expressed in virtual-screen coordinates.
    /// </summary>
    /// <remarks>
    /// <note>If the monitor is not the primary display monitor, some of the rectangle's coordinates may be negative values.</note>
    /// </remarks>
    public Rect WorkingArea { get; }

    /// <summary>
    /// Gets if the monitor is the primary display monitor.
    /// </summary>
    public bool IsPrimary { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Name} {Bounds.Width}x{Bounds.Height}";
}

/// <summary>
/// Contains full information about a display monitor.
/// Inspired from: https://github.com/Jack251970/DesktopWidgets3.
/// </summary>
/// <remarks>
/// Use this class to replace the System.Windows.Forms.Screen class which can cause possible System.PlatformNotSupportedException.
/// </remarks>
public static class MonitorHelper
{
    /// <summary>
    /// Gets the display monitors (including invisible pseudo-monitors associated with the mirroring drivers).
    /// </summary>
    /// <returns>A list of display monitors</returns>
    public static unsafe List<MonitorInfo> GetDisplayMonitors()
    {
        var monitorCount = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CMONITORS);
        var primaryMonitor = GetPrimaryMonitorHandle();
        var list = new List<MonitorInfo>(monitorCount);
        var callback = new MONITORENUMPROC((monitor, deviceContext, rect, data) =>
        {
            list.Add(new MonitorInfo(monitor, primaryMonitor, rect));
            return true;
        });

        bool ok = PInvoke.EnumDisplayMonitors(default, null, callback, default);
        if (!ok)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        return list;
    }

    /// <summary>
    /// Gets the display monitor that is nearest to a given window.
    /// </summary>
    /// <param name="hwnd">Window handle</param>
    public static MonitorInfo? GetNearestDisplayMonitor(nint hwnd)
    {
        var targetMonitor = PInvoke.MonitorFromWindow(new(hwnd), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL);
        if (targetMonitor.IsNull)
            return null;

        return new MonitorInfo(targetMonitor, GetPrimaryMonitorHandle());
    }

    /// <summary>
    /// Gets the display monitor that contains the cursor.
    /// </summary>
    public static MonitorInfo? GetCursorDisplayMonitor()
    {
        if (!PInvoke.GetCursorPos(out var pt))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        var targetMonitor = PInvoke.MonitorFromPoint(pt, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL);
        if (targetMonitor.IsNull)
            return null;

        return new MonitorInfo(targetMonitor, GetPrimaryMonitorHandle());
    }

    /// <summary>
    /// Gets the primary display monitor (the one that contains the taskbar).
    /// </summary>
    public static MonitorInfo GetPrimaryDisplayMonitor()
    {
        var targetMonitor = GetPrimaryMonitorHandle();
        return new MonitorInfo(targetMonitor, targetMonitor);
    }

    private static HMONITOR GetPrimaryMonitorHandle() =>
        PInvoke.MonitorFromWindow(new HWND(nint.Zero), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
}
