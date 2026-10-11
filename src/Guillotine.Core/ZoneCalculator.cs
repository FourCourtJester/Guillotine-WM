namespace Guillotine.Core;

/// <summary>
/// Derives window placement from the current monitor bounds and the desired size.
/// Nothing here is stored; call it again whenever the monitor or size changes.
/// </summary>
public static class ZoneCalculator
{
    /// <summary>
    /// Returns the window rectangle for <paramref name="zone"/>. The result is always
    /// exactly <paramref name="desired"/> in size. If the desired size is larger than the
    /// bounds, the window overflows them (centered zones overflow evenly on both sides).
    /// </summary>
    public static PixelRect Compute(PixelRect bounds, PixelSize desired, Zone zone)
    {
        int x = zone.Horizontal switch
        {
            HorizontalZone.Left => bounds.Left,
            HorizontalZone.Center => bounds.Left + (bounds.Width - desired.Width) / 2,
            HorizontalZone.Right => bounds.Right - desired.Width,
            _ => throw new ArgumentOutOfRangeException(nameof(zone)),
        };

        int y = zone.Vertical switch
        {
            VerticalZone.Top => bounds.Top,
            VerticalZone.Center => bounds.Top + (bounds.Height - desired.Height) / 2,
            VerticalZone.Bottom => bounds.Bottom - desired.Height,
            _ => throw new ArgumentOutOfRangeException(nameof(zone)),
        };

        return new PixelRect(x, y, desired.Width, desired.Height);
    }

    /// <summary>True when a window of <paramref name="desired"/> size fits inside the bounds.</summary>
    public static bool Fits(PixelRect bounds, PixelSize desired) =>
        desired.Width <= bounds.Width && desired.Height <= bounds.Height;
}
