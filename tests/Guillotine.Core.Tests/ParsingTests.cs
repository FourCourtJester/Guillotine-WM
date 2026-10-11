using Guillotine.Core;

namespace Guillotine.Core.Tests;

public class ParsingTests
{
    [Theory]
    [InlineData("center", HorizontalZone.Center, VerticalZone.Center)]
    [InlineData("Left", HorizontalZone.Left, VerticalZone.Center)]
    [InlineData("right", HorizontalZone.Right, VerticalZone.Center)]
    [InlineData("top", HorizontalZone.Center, VerticalZone.Top)]
    [InlineData("bottom", HorizontalZone.Center, VerticalZone.Bottom)]
    [InlineData("top-left", HorizontalZone.Left, VerticalZone.Top)]
    [InlineData("bottom_right", HorizontalZone.Right, VerticalZone.Bottom)]
    [InlineData("center center", HorizontalZone.Center, VerticalZone.Center)]
    public void Zone_parses(string text, HorizontalZone horizontal, VerticalZone vertical)
    {
        Assert.Equal(new Zone(horizontal, vertical), Zone.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("middle")]
    [InlineData("left-top")] // vertical comes first
    [InlineData("top-left-ish")]
    [InlineData("1")]
    public void Zone_rejects(string text)
    {
        Assert.False(Zone.TryParse(text, out _));
    }

    [Fact]
    public void Zone_names_round_trip()
    {
        foreach (var zone in Zone.All)
            Assert.Equal(zone, Zone.Parse(zone.ToString()));
    }

    [Fact]
    public void Zone_all_has_nine_distinct_zones()
    {
        Assert.Equal(9, Zone.All.Distinct().Count());
    }

    [Theory]
    [InlineData("2560x1440", 2560, 1440)]
    [InlineData(" 1920X1080 ", 1920, 1080)]
    [InlineData("800*600", 800, 600)]
    public void Size_parses(string text, int width, int height)
    {
        Assert.Equal(new PixelSize(width, height), PixelSize.Parse(text));
    }

    [Theory]
    [InlineData("2560")]
    [InlineData("0x1440")]
    [InlineData("-2560x1440")]
    [InlineData("2560x1440x3")]
    [InlineData("wide")]
    public void Size_rejects(string text)
    {
        Assert.False(PixelSize.TryParse(text, out _));
    }
}
