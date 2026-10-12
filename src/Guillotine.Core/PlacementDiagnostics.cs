namespace Guillotine.Core;

/// <summary>Plain-language explanations for placements that cannot work or did not work.</summary>
public static class PlacementDiagnostics
{
    /// <summary>
    /// Returns a message when <paramref name="size"/> cannot fit in the chosen area, or null when it fits.
    /// </summary>
    public static string? CheckFits(PixelRect monitor, PixelRect workArea, PixelSize size, bool useWorkArea)
    {
        var area = useWorkArea ? workArea : monitor;
        if (ZoneCalculator.Fits(area, size))
            return null;

        if (useWorkArea && ZoneCalculator.Fits(monitor, size))
            return $"{size} does not fit in the space the taskbar leaves free ({area.Size}). " +
                   "Turn off \"Keep clear of the taskbar\" and set the taskbar to auto-hide, or pick a smaller size.";

        return $"{size} is larger than the monitor ({monitor.Size}). Pick a smaller size.";
    }

    /// <summary>Explains why the client area ended up at <paramref name="actual"/> instead of <paramref name="target"/>.</summary>
    public static string ExplainMismatch(PixelRect monitor, PixelRect workArea, PixelRect target, PixelRect actual)
    {
        var detail = $"Wanted {target}, got {actual}.";

        bool workAreaIsSmaller = workArea.Height < monitor.Height || workArea.Width < monitor.Width;
        bool shrankToWorkArea = workAreaIsSmaller
            && (actual.Height < target.Height || actual.Width < target.Width)
            && actual.Height <= workArea.Height && actual.Width <= workArea.Width
            && (target.Height > workArea.Height || target.Width > workArea.Width);

        if (shrankToWorkArea)
            return $"{detail} The window was kept to the space the taskbar leaves free ({workArea.Size}). " +
                   "Set the taskbar to auto-hide (Settings > Personalization > Taskbar > Taskbar behaviors), then apply again.";

        if (actual.Size == target.Size)
            return $"{detail} The size is right but the game moved itself afterwards. Try applying again.";

        if (actual.Width > target.Width || actual.Height > target.Height)
            return $"{detail} The game kept a larger minimum size than requested.";

        return $"{detail} The game limits its own window size, or DPI scaling is interfering.";
    }
}
