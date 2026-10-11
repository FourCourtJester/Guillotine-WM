using Guillotine.Core;

namespace Guillotine.Core.Tests;

public class ZoneCalculatorTests
{
    private static readonly PixelRect SuperUltrawide = new(0, 0, 5120, 1440);

    // Worked example from docs/handoff.md.
    [Theory]
    [InlineData("left", 0, 0)]
    [InlineData("center", 1280, 0)]
    [InlineData("right", 2560, 0)]
    public void Ultrawide_1440p_matches_handoff(string zone, int x, int y)
    {
        var rect = ZoneCalculator.Compute(SuperUltrawide, new PixelSize(2560, 1440), Zone.Parse(zone));

        Assert.Equal(new PixelRect(x, y, 2560, 1440), rect);
    }

    [Fact]
    public void Full_height_window_collapses_vertical_zones_to_top()
    {
        var desired = new PixelSize(2560, 1440);

        foreach (var vertical in Enum.GetValues<VerticalZone>())
            Assert.Equal(0, ZoneCalculator.Compute(SuperUltrawide, desired, new Zone(HorizontalZone.Center, vertical)).Top);
    }

    [Theory]
    [InlineData("top-left", 0, 0)]
    [InlineData("top", 1600, 0)]
    [InlineData("top-right", 3200, 0)]
    [InlineData("left", 0, 180)]
    [InlineData("center", 1600, 180)]
    [InlineData("right", 3200, 180)]
    [InlineData("bottom-left", 0, 360)]
    [InlineData("bottom", 1600, 360)]
    [InlineData("bottom-right", 3200, 360)]
    public void Shorter_window_gets_distinct_vertical_zones(string zone, int x, int y)
    {
        var rect = ZoneCalculator.Compute(SuperUltrawide, new PixelSize(1920, 1080), Zone.Parse(zone));

        Assert.Equal(new PixelRect(x, y, 1920, 1080), rect);
    }

    [Fact]
    public void Zones_are_relative_to_monitor_origin()
    {
        // A secondary monitor to the left of the primary has negative coordinates.
        var secondary = new PixelRect(-2560, -100, 2560, 1440);

        var rect = ZoneCalculator.Compute(secondary, new PixelSize(1920, 1080), Zone.Parse("bottom-right"));

        Assert.Equal(new PixelRect(-1920, 260, 1920, 1080), rect);
    }

    [Fact]
    public void Work_area_shrinks_the_usable_height()
    {
        var workArea = new PixelRect(0, 0, 5120, 1392); // 48 px taskbar at the bottom

        var rect = ZoneCalculator.Compute(workArea, new PixelSize(2560, 1440), Zone.Center);

        Assert.Equal(new PixelRect(1280, -24, 2560, 1440), rect);
        Assert.False(ZoneCalculator.Fits(workArea, new PixelSize(2560, 1440)));
    }

    [Fact]
    public void Result_is_always_the_desired_size()
    {
        var desired = new PixelSize(1234, 567);

        foreach (var zone in Zone.All)
            Assert.Equal(desired, ZoneCalculator.Compute(SuperUltrawide, desired, zone).Size);
    }

    [Fact]
    public void Odd_leftover_space_rounds_toward_the_origin()
    {
        var rect = ZoneCalculator.Compute(new PixelRect(0, 0, 101, 101), new PixelSize(50, 50), Zone.Center);

        Assert.Equal(25, rect.Left);
        Assert.Equal(25, rect.Top);
    }
}
