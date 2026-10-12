using Guillotine.Core;

namespace Guillotine.Core.Tests;

public class PlacementDiagnosticsTests
{
    private static readonly PixelRect Monitor = new(0, 0, 5120, 1440);
    private static readonly PixelRect WorkArea = new(0, 0, 5120, 1392); // 48 px taskbar at the bottom

    [Fact]
    public void Full_height_fits_the_monitor()
    {
        Assert.Null(PlacementDiagnostics.CheckFits(Monitor, WorkArea, new PixelSize(2560, 1440), useWorkArea: false));
    }

    [Fact]
    public void Full_height_does_not_fit_above_the_taskbar()
    {
        var message = PlacementDiagnostics.CheckFits(Monitor, WorkArea, new PixelSize(2560, 1440), useWorkArea: true);

        Assert.NotNull(message);
        Assert.Contains("taskbar", message);
        Assert.Contains("5120x1392", message);
    }

    [Fact]
    public void Shorter_window_fits_above_the_taskbar()
    {
        Assert.Null(PlacementDiagnostics.CheckFits(Monitor, WorkArea, new PixelSize(1920, 1080), useWorkArea: true));
    }

    [Fact]
    public void Larger_than_monitor_is_reported()
    {
        var message = PlacementDiagnostics.CheckFits(Monitor, WorkArea, new PixelSize(6000, 1440), useWorkArea: false);

        Assert.Contains("larger than the monitor", message);
    }

    [Fact]
    public void Window_shrunk_to_the_work_area_suggests_auto_hide()
    {
        var target = new PixelRect(1280, 0, 2560, 1440);
        var actual = new PixelRect(1280, 0, 2560, 1392);

        var message = PlacementDiagnostics.ExplainMismatch(Monitor, WorkArea, target, actual);

        Assert.Contains("auto-hide", message);
    }

    [Fact]
    public void Right_size_wrong_place_says_the_game_moved()
    {
        var target = new PixelRect(1280, 0, 2560, 1440);
        var actual = new PixelRect(1300, 0, 2560, 1440);

        Assert.Contains("moved itself", PlacementDiagnostics.ExplainMismatch(Monitor, WorkArea, target, actual));
    }

    [Fact]
    public void Other_shrinking_blames_the_game()
    {
        var target = new PixelRect(1280, 0, 2560, 1440);
        var actual = new PixelRect(1280, 0, 2400, 1350);

        var message = PlacementDiagnostics.ExplainMismatch(Monitor, Monitor, target, actual);

        Assert.Contains("limits its own window size", message);
    }
}
