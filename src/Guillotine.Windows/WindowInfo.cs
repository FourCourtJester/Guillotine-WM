using Guillotine.Core;

namespace Guillotine.Windows;

/// <summary>A snapshot of a top-level window. Rectangles are in physical screen pixels.</summary>
/// <param name="Bounds">The whole window, including any frame (GetWindowRect).</param>
/// <param name="Client">The client area in screen coordinates. This is what a capture tool records.</param>
public sealed record WindowInfo(
    IntPtr Handle,
    string Title,
    int ProcessId,
    string ProcessName,
    PixelRect Bounds,
    PixelRect Client,
    bool IsBorderless,
    bool IsTopmost)
{
    public string HandleText => $"0x{Handle.ToInt64():X}";
}

/// <summary>The outcome of <see cref="WindowController.Apply"/>.</summary>
/// <param name="Target">Where the client area should be.</param>
/// <param name="Actual">Where the client area ended up.</param>
/// <param name="Forced">
/// True when the window's own size limits had to be bypassed (the window refused the size when asked normally).
/// </param>
/// <param name="Fallback">
/// Set when the game insisted on a different size: where that size was placed in the requested zone.
/// </param>
public sealed record ApplyResult(
    WindowInfo Window, MonitorInfo Monitor, PixelRect Target, PixelRect Actual, bool Forced, PixelRect? Fallback = null)
{
    public bool IsExact => Target == Actual;

    /// <summary>Where the window should stay: the target, or the fallback when the game chose its own size.</summary>
    public PixelRect Settled => Fallback ?? Target;

    /// <summary>A plain-language explanation when the result is not exact; null when it is.</summary>
    public string? Problem => IsExact
        ? null
        : Fallback is { } fallback && Actual == fallback
            ? $"The game will not go to {Target.Size}; it keeps resizing itself to {Actual.Size}. " +
              $"Placed it in the same zone at that size instead: {Actual}."
            : PlacementDiagnostics.ExplainMismatch(Monitor.Bounds, Monitor.WorkArea, Target, Actual);
}

/// <summary>A Win32 call on a window failed.</summary>
public sealed class WindowOperationException(string message) : Exception(message);
