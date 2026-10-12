using System.Runtime.Versioning;
using Guillotine.Core;

namespace Guillotine.Windows;

/// <summary>What <see cref="WindowKeeper.Check"/> found.</summary>
/// <param name="Changed">How the window differed from the placement before it was put back.</param>
/// <param name="Result">The outcome of putting it back.</param>
public sealed record KeeperEvent(string Changed, ApplyResult Result);

/// <summary>
/// Puts a window back when it changes its own size, position, frame or z-order after being placed,
/// for games that reset their window. Call <see cref="Check"/> on a timer. After
/// <see cref="MaxReapplies"/> corrections it stops, so it cannot fight a game forever.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowKeeper(IntPtr hwnd, Placement placement, PixelRect target, int maxReapplies = 10)
{
    public IntPtr Handle { get; } = hwnd;
    public Placement Placement { get; } = placement;
    public int MaxReapplies { get; } = maxReapplies;
    public int Reapplies { get; private set; }
    public bool GaveUp => Reapplies >= MaxReapplies;

    private PixelRect _target = target;

    /// <summary>
    /// Returns null when the window is still where it should be (or the keeper has given up).
    /// Throws <see cref="WindowOperationException"/> if the window has closed.
    /// </summary>
    public KeeperEvent? Check()
    {
        if (GaveUp)
            return null;

        var window = WindowController.Describe(Handle);
        var changes = new List<string>();
        if (window.Client != _target)
            changes.Add($"client is {window.Client}");
        if (Placement.Borderless && !window.IsBorderless)
            changes.Add("frame came back");
        if (window.IsTopmost != Placement.Topmost)
            changes.Add(Placement.Topmost ? "no longer on top" : "made itself always on top");

        if (changes.Count == 0)
            return null;

        Reapplies++;
        var result = WindowController.Apply(Handle, Placement);
        _target = result.Target;
        return new KeeperEvent(string.Join(", ", changes), result);
    }
}
