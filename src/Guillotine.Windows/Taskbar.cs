using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Guillotine.Core;
using static Guillotine.Windows.NativeMethods;

namespace Guillotine.Windows;

/// <summary>Where the Windows taskbar is.</summary>
/// <param name="Bounds">The taskbar's rectangle in physical pixels (partly off screen while an auto-hide taskbar is hidden).</param>
/// <param name="Thickness">How far the taskbar reaches into the monitor when it is showing.</param>
public sealed record TaskbarInfo(PixelRect Bounds, TaskbarEdge Edge, int Thickness, MonitorInfo Monitor);

[SupportedOSPlatform("windows")]
public static class Taskbar
{
    /// <summary>The primary taskbar, or null if Windows does not report one.</summary>
    public static TaskbarInfo? Get()
    {
        var data = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>() };
        if (SHAppBarMessage(ABM_GETTASKBARPOS, ref data) == UIntPtr.Zero)
            return null;

        var edge = data.uEdge switch
        {
            0 => TaskbarEdge.Left,
            1 => TaskbarEdge.Top,
            2 => TaskbarEdge.Right,
            _ => TaskbarEdge.Bottom,
        };
        var bounds = PixelRect.FromEdges(data.rc.Left, data.rc.Top, data.rc.Right, data.rc.Bottom);
        int thickness = edge is TaskbarEdge.Left or TaskbarEdge.Right ? bounds.Width : bounds.Height;
        var monitor = Monitors.Get(MonitorFromRect(ref data.rc, MONITOR_DEFAULTTONEAREST));
        return new TaskbarInfo(bounds, edge, thickness, monitor);
    }
}
