using Guillotine.Core;
using static Guillotine.Core.WindowStyles;

namespace Guillotine.Core.Tests;

public class WindowStylesTests
{
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_CLIPSIBLINGS = 0x04000000;
    private const uint TypicalGameStyle = WS_OVERLAPPEDWINDOW | WS_VISIBLE | WS_CLIPSIBLINGS;
    private const uint TypicalGameExStyle = WS_EX_WINDOWEDGE | 0x00000100;

    [Fact]
    public void Borderless_removes_caption_and_frame_and_adds_popup()
    {
        var (style, exStyle) = MakeBorderless(TypicalGameStyle, TypicalGameExStyle | WS_EX_CLIENTEDGE);

        Assert.True(IsBorderless(style));
        Assert.Equal(0u, style & (WS_CAPTION | WS_THICKFRAME | WS_MAXIMIZEBOX));
        Assert.Equal(WS_POPUP, style & WS_POPUP);
        Assert.Equal(0u, exStyle & (WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE));
    }

    [Fact]
    public void Borderless_keeps_unrelated_flags()
    {
        var (style, exStyle) = MakeBorderless(TypicalGameStyle, TypicalGameExStyle | WS_EX_TOPMOST);

        Assert.Equal(WS_VISIBLE | WS_CLIPSIBLINGS, style & (WS_VISIBLE | WS_CLIPSIBLINGS));
        Assert.Equal(WS_SYSMENU | WS_MINIMIZEBOX, style & (WS_SYSMENU | WS_MINIMIZEBOX));
        Assert.Equal(WS_EX_TOPMOST, exStyle & WS_EX_TOPMOST);
    }

    [Fact]
    public void Borderless_clears_maximized()
    {
        var (style, _) = MakeBorderless(TypicalGameStyle | WS_MAXIMIZE, 0);

        Assert.Equal(0u, style & WS_MAXIMIZE);
    }

    [Fact]
    public void Framed_fallback_restores_a_normal_window()
    {
        var (borderless, borderlessEx) = MakeBorderless(TypicalGameStyle, TypicalGameExStyle);

        var (style, _) = MakeFramed(borderless, borderlessEx);

        Assert.False(IsBorderless(style));
        Assert.Equal(WS_OVERLAPPEDWINDOW, style & WS_OVERLAPPEDWINDOW);
        Assert.Equal(0u, style & WS_POPUP);
    }

    [Theory]
    [InlineData(WS_OVERLAPPEDWINDOW, false)]
    [InlineData(WS_CAPTION, false)]
    [InlineData(WS_THICKFRAME, false)]
    [InlineData(WS_POPUP, true)]
    [InlineData(WS_POPUP | WS_SYSMENU | WS_MINIMIZEBOX, true)]
    public void Detects_borderless(uint style, bool expected)
    {
        Assert.Equal(expected, IsBorderless(style));
    }
}
