namespace Guillotine.Core;

/// <summary>Which edge of the monitor the taskbar is docked to.</summary>
public enum TaskbarEdge
{
    Left,
    Top,
    Right,
    Bottom,
}

/// <summary>Where to show a flyout panel next to the notification area, like Windows' own flyouts.</summary>
public static class FlyoutPlacement
{
    /// <summary>
    /// Places a panel of <paramref name="size"/> in the corner of <paramref name="monitor"/> next to the
    /// notification area, leaving room for the taskbar (its full thickness, so the panel does not move
    /// when an auto-hide taskbar slides in or out). Everything is in physical pixels.
    /// </summary>
    public static PixelRect Compute(PixelRect monitor, TaskbarEdge edge, int taskbarThickness, PixelSize size, int margin)
    {
        int right = monitor.Right - margin - (edge == TaskbarEdge.Right ? taskbarThickness : 0);
        int bottom = monitor.Bottom - margin - (edge == TaskbarEdge.Bottom ? taskbarThickness : 0);
        int x = right - size.Width;
        int y = edge == TaskbarEdge.Top
            ? monitor.Top + taskbarThickness + margin
            : bottom - size.Height;

        // Keep it on screen even if the panel is taller or wider than the space available.
        x = Math.Max(monitor.Left, x);
        y = Math.Max(monitor.Top, y);
        return new PixelRect(x, y, size.Width, size.Height);
    }
}
