using Guillotine.Core;

namespace Guillotine.Core.Tests;

public class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Alt+G", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "G")]
    [InlineData("ctrl + alt + g", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "G")]
    [InlineData("Control+Shift+F5", HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "F5")]
    [InlineData("Win+Shift+space", HotkeyModifiers.Win | HotkeyModifiers.Shift, "Space")]
    [InlineData("Alt+1", HotkeyModifiers.Alt, "1")]
    public void Parses(string text, HotkeyModifiers modifiers, string key)
    {
        Assert.Equal(new Hotkey(modifiers, key), Hotkey.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("G")]               // no modifier
    [InlineData("Shift+G")]         // Shift alone would block typing capitals
    [InlineData("Ctrl+Alt")]        // no key
    [InlineData("Ctrl+G+H")]        // two keys
    [InlineData("Ctrl+F-5")]        // not a key name
    public void Rejects(string text)
    {
        Assert.False(Hotkey.TryParse(text, out _));
    }

    [Fact]
    public void Formats_in_a_fixed_order_and_round_trips()
    {
        var hotkey = Hotkey.Parse("win+shift+alt+ctrl+k");

        Assert.Equal("Ctrl+Alt+Shift+Win+K", hotkey.ToString());
        Assert.Equal(hotkey, Hotkey.Parse(hotkey.ToString()));
    }

    [Fact]
    public void Default_is_ctrl_alt_g_and_settings_validate_it()
    {
        Assert.Equal("Ctrl+Alt+G", Hotkey.Default.ToString());
        Assert.Equal("Ctrl+Alt+G", new GuillotineSettings().Hotkey);
        Assert.Throws<InvalidDataException>(() => GuillotineSettings.FromJson("""{ "hotkey": "G" }"""));
        Assert.Equal("", GuillotineSettings.FromJson("""{ "hotkey": "" }""").Hotkey); // empty turns the hotkey off
    }
}

public class FlyoutPlacementTests
{
    private static readonly PixelRect Monitor = new(0, 0, 5120, 1440);
    private static readonly PixelSize Panel = new(380, 600);

    [Fact]
    public void Bottom_taskbar_puts_the_panel_above_it_in_the_right_corner()
    {
        var rect = FlyoutPlacement.Compute(Monitor, TaskbarEdge.Bottom, 60, Panel, 12);

        Assert.Equal(new PixelRect(5120 - 12 - 380, 1440 - 60 - 12 - 600, 380, 600), rect);
    }

    [Fact]
    public void Right_taskbar_puts_the_panel_beside_it_at_the_bottom()
    {
        var rect = FlyoutPlacement.Compute(Monitor, TaskbarEdge.Right, 80, Panel, 12);

        Assert.Equal(new PixelRect(5120 - 80 - 12 - 380, 1440 - 12 - 600, 380, 600), rect);
    }

    [Fact]
    public void Top_taskbar_puts_the_panel_below_it()
    {
        var rect = FlyoutPlacement.Compute(Monitor, TaskbarEdge.Top, 60, Panel, 12);

        Assert.Equal(new PixelRect(5120 - 12 - 380, 60 + 12, 380, 600), rect);
    }

    [Fact]
    public void Panel_stays_on_screen_when_too_tall()
    {
        var rect = FlyoutPlacement.Compute(new PixelRect(-1920, 0, 1920, 500), TaskbarEdge.Bottom, 60, Panel, 12);

        Assert.Equal(0, rect.Top);
        Assert.Equal(-12 - 380, rect.Left);
    }
}
