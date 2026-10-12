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
public sealed record ApplyResult(WindowInfo Window, MonitorInfo Monitor, PixelRect Target, PixelRect Actual, bool Forced)
{
    public bool IsExact => Target == Actual;

    /// <summary>A plain-language explanation when the result is not exact; null when it is.</summary>
    public string? Problem => IsExact
        ? null
        : PlacementDiagnostics.ExplainMismatch(Monitor.Bounds, Monitor.WorkArea, Target, Actual);
}

/// <summary>A Win32 call on a window failed.</summary>
public sealed class WindowOperationException(string message) : Exception(message);
