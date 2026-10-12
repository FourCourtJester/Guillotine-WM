using System.Runtime.Versioning;
using Guillotine.Core;
using static Guillotine.Windows.NativeMethods;

namespace Guillotine.Windows;

/// <param name="Bounds">Full monitor bounds (rcMonitor) in physical pixels.</param>
/// <param name="WorkArea">Bounds excluding the taskbar and docked toolbars (rcWork).</param>
/// <param name="Dpi">Effective DPI; 96 means 100% scaling.</param>
public sealed record MonitorInfo(IntPtr Handle, string DeviceName, PixelRect Bounds, PixelRect WorkArea, bool IsPrimary, uint Dpi)
{
    public int ScalePercent => (int)Math.Round(Dpi * 100 / 96.0);
}

[SupportedOSPlatform("windows")]
public static class Monitors
{
    /// <summary>
    /// Makes this process per-monitor DPI aware so every coordinate the Win32 APIs return
    /// is in physical pixels. Call once at startup, before any window or monitor query.
    /// Returns false if awareness was already set (for example, by an app manifest).
    /// </summary>
    public static bool EnablePerMonitorDpiAwareness() =>
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    public static IReadOnlyList<MonitorInfo> GetAll()
    {
        var monitors = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr handle, IntPtr _, ref RECT _, IntPtr _) =>
        {
            monitors.Add(Get(handle));
            return true;
        }, IntPtr.Zero);
        return monitors;
    }

    /// <summary>The monitor a window is on (the nearest one if it is off screen).</summary>
    public static MonitorInfo ForWindow(IntPtr hwnd) => Get(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST));

    public static MonitorInfo Get(IntPtr monitorHandle)
    {
        var info = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
        if (!GetMonitorInfo(monitorHandle, ref info))
            throw new InvalidOperationException($"GetMonitorInfo failed for monitor 0x{monitorHandle:X}.");

        uint dpi = GetDpiForMonitor(monitorHandle, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 ? dpiX : 96;

        return new MonitorInfo(
            monitorHandle,
            info.szDevice,
            ToPixelRect(info.rcMonitor),
            ToPixelRect(info.rcWork),
            (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
            dpi);
    }

    private static PixelRect ToPixelRect(RECT r) => PixelRect.FromEdges(r.Left, r.Top, r.Right, r.Bottom);
}
