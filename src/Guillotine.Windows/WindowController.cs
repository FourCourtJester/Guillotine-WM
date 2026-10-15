using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Guillotine.Core;
using static Guillotine.Windows.NativeMethods;

namespace Guillotine.Windows;

/// <summary>Finds top-level windows and changes their position, size, frame and z-order.</summary>
[SupportedOSPlatform("windows")]
public static class WindowController
{
    // The original styles are stored on the window itself (SetProp), so any later run of
    // Guillotine, CLI or GUI, can restore them.
    private const string SavedMarkerProp = "Guillotine.Saved";
    private const string SavedStyleProp = "Guillotine.Style";
    private const string SavedExStyleProp = "Guillotine.ExStyle";

    /// <summary>
    /// Lists visible, titled, top-level application windows, the same set that appears in Alt+Tab.
    /// Windows that belong to this process, its console, or the processes that started it (the
    /// terminal, whose title often contains the command line) are left out.
    /// </summary>
    public static IReadOnlyList<WindowInfo> GetWindows()
    {
        var handles = new List<IntPtr>();
        EnumWindows((hwnd, _) =>
        {
            handles.Add(hwnd);
            return true;
        }, IntPtr.Zero);

        var excludedProcesses = ProcessAncestry.GetAncestors();
        excludedProcesses.Add(Environment.ProcessId);
        IntPtr console = GetConsoleWindow();

        var processNames = new Dictionary<int, string>();
        var windows = new List<WindowInfo>();

        foreach (var hwnd in handles)
        {
            if (hwnd == console || !IsCandidate(hwnd))
                continue;

            var info = TryDescribe(hwnd, processNames);
            if (info is not null && !excludedProcesses.Contains(info.ProcessId))
                windows.Add(info);
        }
        return windows;
    }

    /// <summary>
    /// Windows that match any of <paramref name="rules"/>, best match first (an exact title before one
    /// that only contains the text); ties stay in front-to-back order.
    /// </summary>
    public static IReadOnlyList<WindowInfo> Find(IEnumerable<GameRule> rules)
    {
        var ruleList = rules.ToList();
        return GetWindows()
            .Select(w => (Window: w, GuillotineSettings.BestMatch(ruleList, w.Title, w.ProcessName).Quality))
            .Where(m => m.Quality > 0)
            .OrderByDescending(m => m.Quality)
            .Select(m => m.Window)
            .ToList();
    }

    /// <summary>Reads the current state of one window.</summary>
    public static WindowInfo Describe(IntPtr hwnd) =>
        TryDescribe(hwnd, []) ?? throw new WindowOperationException($"Window 0x{hwnd.ToInt64():X} no longer exists.");

    /// <summary>
    /// Sets the frame and z-order, then moves and resizes the window so its client area is exactly
    /// <see cref="Placement.Size"/> at the zone position on the monitor the window is on. When the
    /// window keeps its frame, the frame sits outside the zone so the client area still lines up.
    /// </summary>
    public static ApplyResult Apply(IntPtr hwnd, Placement placement)
    {
        EnsureWindow(hwnd);

        // Maximised and minimised windows ignore or override explicit sizes.
        if (IsIconic(hwnd) || IsZoomed(hwnd))
            ShowWindow(hwnd, SW_RESTORE);

        SetBorderless(hwnd, placement.Borderless);
        SetTopmost(hwnd, placement.Topmost);

        var monitor = Monitors.ForWindow(hwnd);
        var area = placement.UseWorkArea ? monitor.WorkArea : monitor.Bounds;
        var target = ZoneCalculator.Compute(area, placement.Size, placement.Zone);

        // Some windows adjust their frame after the first resize (menus wrapping, DPI changes),
        // so measure the frame again and correct once if the first attempt is off.
        var actual = MoveClientTo(hwnd, target, force: false);
        if (actual != target)
            actual = MoveClientTo(hwnd, target, force: false);

        // Windows and many games cap a window's size while it is being resized (WM_GETMINMAXINFO /
        // WM_WINDOWPOSCHANGING), often to the space above the taskbar. Skipping that message lets a
        // full-height window through.
        bool forced = false;
        if (actual != target)
        {
            forced = true;
            actual = MoveClientTo(hwnd, target, force: true);
        }

        // Some games resize themselves straight back to a size of their own choosing (Heroes of the
        // Storm keeps 16:9 inside the monitor minus its own frame). Accept that size and put it in
        // the requested zone instead, so the window is at least where it was asked to be. Moving
        // without resizing does not set the game's sizing rule off again.
        PixelRect? fallback = null;
        if (actual.Size != target.Size)
        {
            fallback = ZoneCalculator.Compute(area, actual.Size, placement.Zone);
            actual = MoveClientTo(hwnd, fallback.Value, force: false);
        }

        return new ApplyResult(Describe(hwnd), monitor, target, actual, forced, fallback);
    }

    /// <summary>
    /// Removes or restores the title bar and borders. The window keeps its outer position and size;
    /// call <see cref="Apply"/> afterwards to fit the client area to a zone.
    /// </summary>
    public static void SetBorderless(IntPtr hwnd, bool borderless)
    {
        EnsureWindow(hwnd);
        uint style = GetWindowStyle(hwnd, GWL_STYLE);
        uint exStyle = GetWindowStyle(hwnd, GWL_EXSTYLE);

        if (borderless == WindowStyles.IsBorderless(style))
            return;

        var (newStyle, newExStyle) = ComputeFrameStyles(hwnd, borderless);

        if (!SetWindowStyle(hwnd, GWL_STYLE, newStyle) || !SetWindowStyle(hwnd, GWL_EXSTYLE, newExStyle))
            throw Win32Failure(hwnd, "change the window style");

        if (!borderless)
            ClearSavedStyles(hwnd);

        // Style changes only take effect on screen after SWP_FRAMECHANGED.
        if (!SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_FRAMECHANGED))
            throw Win32Failure(hwnd, "redraw the window frame");
    }

    /// <summary>
    /// Removes or restores the title bar and borders while keeping the client area (the game picture)
    /// exactly where it is: the outer window shrinks to the client area or grows around it. The style
    /// and the new outer rectangle are applied in one step, so the game never sees an in-between size.
    /// A restored frame is kept on the monitor where possible.
    /// </summary>
    public static WindowInfo SetBorderlessKeepingClient(IntPtr hwnd, bool borderless)
    {
        EnsureWindow(hwnd);
        uint style = GetWindowStyle(hwnd, GWL_STYLE);
        if (borderless == WindowStyles.IsBorderless(style))
            return Describe(hwnd);

        var client = GetClientScreenRect(hwnd);
        var (newStyle, newExStyle) = ComputeFrameStyles(hwnd, borderless);
        if (!SetWindowStyle(hwnd, GWL_STYLE, newStyle) || !SetWindowStyle(hwnd, GWL_EXSTYLE, newExStyle))
            throw Win32Failure(hwnd, "change the window style");
        if (!borderless)
            ClearSavedStyles(hwnd);

        var outer = borderless ? client : OuterRectFor(hwnd, client, newStyle, newExStyle);
        if (!borderless)
            outer = KeepOnMonitor(outer, Monitors.ForWindow(hwnd).Bounds);

        if (!SetWindowPos(hwnd, IntPtr.Zero, outer.Left, outer.Top, outer.Width, outer.Height,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_FRAMECHANGED))
            throw Win32Failure(hwnd, "redraw the window frame");

        // If the estimate was off (unusual frames), correct the client size without moving it again.
        var now = GetClientScreenRect(hwnd);
        if (now.Size != client.Size)
            MoveClientTo(hwnd, new PixelRect(now.Left, now.Top, client.Width, client.Height), force: false);

        return Describe(hwnd);
    }

    /// <summary>Moves a window (for example Guillotine's own flyout) without resizing or activating it.</summary>
    public static void MoveWindow(IntPtr hwnd, int x, int y, bool topmost)
    {
        SetWindowPos(hwnd, topmost ? HWND_TOPMOST : IntPtr.Zero, x, y, 0, 0,
            SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER | (topmost ? 0 : SWP_NOZORDER));
    }

    public static void SetTopmost(IntPtr hwnd, bool topmost)
    {
        EnsureWindow(hwnd);
        if (!SetWindowPos(hwnd, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER))
            throw Win32Failure(hwnd, topmost ? "make the window always on top" : "clear always on top");
    }

    /// <summary>Puts back the original frame (around the current client area) and turns off always on top.</summary>
    public static WindowInfo Restore(IntPtr hwnd)
    {
        SetBorderlessKeepingClient(hwnd, false);
        SetTopmost(hwnd, false);
        return Describe(hwnd);
    }

    /// <summary>
    /// The styles for the requested frame state. Going borderless saves the current styles on the window
    /// (once); restoring uses them, or a standard frame if none were saved. The topmost bit is kept.
    /// </summary>
    private static (uint Style, uint ExStyle) ComputeFrameStyles(IntPtr hwnd, bool borderless)
    {
        uint style = GetWindowStyle(hwnd, GWL_STYLE);
        uint exStyle = GetWindowStyle(hwnd, GWL_EXSTYLE);

        if (borderless)
        {
            if (GetProp(hwnd, SavedMarkerProp) == IntPtr.Zero)
            {
                SetProp(hwnd, SavedStyleProp, new IntPtr(unchecked((int)style)));
                SetProp(hwnd, SavedExStyleProp, new IntPtr(unchecked((int)exStyle)));
                SetProp(hwnd, SavedMarkerProp, new IntPtr(1));
            }
            return WindowStyles.MakeBorderless(style, exStyle);
        }

        if (GetProp(hwnd, SavedMarkerProp) != IntPtr.Zero)
        {
            uint savedStyle = unchecked((uint)GetProp(hwnd, SavedStyleProp).ToInt32());
            uint savedExStyle = unchecked((uint)GetProp(hwnd, SavedExStyleProp).ToInt32());
            return (savedStyle, (savedExStyle & ~WindowStyles.WS_EX_TOPMOST) | (exStyle & WindowStyles.WS_EX_TOPMOST));
        }

        return WindowStyles.MakeFramed(style, exStyle);
    }

    /// <summary>The outer window rectangle that gives <paramref name="client"/> with the given frame styles.</summary>
    private static PixelRect OuterRectFor(IntPtr hwnd, PixelRect client, uint style, uint exStyle)
    {
        var rect = new RECT { Left = client.Left, Top = client.Top, Right = client.Right, Bottom = client.Bottom };
        uint dpi = GetDpiForWindow(hwnd);
        if (!AdjustWindowRectExForDpi(ref rect, style, GetMenu(hwnd) != IntPtr.Zero, exStyle, dpi == 0 ? 96 : dpi))
            return client;
        return PixelRect.FromEdges(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    /// <summary>Shifts <paramref name="rect"/> inside <paramref name="area"/> when it fits, so a restored title bar is reachable.</summary>
    private static PixelRect KeepOnMonitor(PixelRect rect, PixelRect area)
    {
        int x = rect.Width <= area.Width ? Math.Clamp(rect.Left, area.Left, area.Right - rect.Width) : rect.Left;
        int y = rect.Height <= area.Height ? Math.Clamp(rect.Top, area.Top, area.Bottom - rect.Height) : Math.Max(rect.Top, area.Top);
        return rect with { Left = x, Top = y };
    }

    private static PixelRect MoveClientTo(IntPtr hwnd, PixelRect target, bool force)
    {
        var window = GetBounds(hwnd);
        var client = GetClientScreenRect(hwnd);

        int left = client.Left - window.Left;
        int top = client.Top - window.Top;
        int right = window.Right - client.Right;
        int bottom = window.Bottom - client.Bottom;

        if (!SetWindowPos(hwnd, IntPtr.Zero,
                target.Left - left, target.Top - top,
                target.Width + left + right, target.Height + top + bottom,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_FRAMECHANGED | (force ? SWP_NOSENDCHANGING : 0)))
            throw Win32Failure(hwnd, "move and resize the window");

        return GetClientScreenRect(hwnd);
    }

    private static void ClearSavedStyles(IntPtr hwnd)
    {
        RemoveProp(hwnd, SavedMarkerProp);
        RemoveProp(hwnd, SavedStyleProp);
        RemoveProp(hwnd, SavedExStyleProp);
    }

    private static bool IsCandidate(IntPtr hwnd)
    {
        if (!IsWindowVisible(hwnd) || GetWindowTextLength(hwnd) == 0 || GetWindow(hwnd, GW_OWNER) != IntPtr.Zero)
            return false;
        if ((GetWindowStyle(hwnd, GWL_EXSTYLE) & WindowStyles.WS_EX_TOOLWINDOW) != 0)
            return false;

        // Suspended UWP apps and windows on other virtual desktops are "cloaked": visible but not shown.
        return DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) != 0 || cloaked == 0;
    }

    private static WindowInfo? TryDescribe(IntPtr hwnd, Dictionary<int, string> processNames)
    {
        if (!IsWindow(hwnd) || !GetWindowRect(hwnd, out var rect))
            return null;

        GetWindowThreadProcessId(hwnd, out uint pid);
        int processId = (int)pid;
        if (!processNames.TryGetValue(processId, out var processName))
            processNames[processId] = processName = GetProcessName(processId);

        return new WindowInfo(
            hwnd,
            GetTitle(hwnd),
            processId,
            processName,
            PixelRect.FromEdges(rect.Left, rect.Top, rect.Right, rect.Bottom),
            GetClientScreenRect(hwnd),
            WindowStyles.IsBorderless(GetWindowStyle(hwnd, GWL_STYLE)),
            (GetWindowStyle(hwnd, GWL_EXSTYLE) & WindowStyles.WS_EX_TOPMOST) != 0);
    }

    private static string GetTitle(IntPtr hwnd)
    {
        int length = GetWindowTextLength(hwnd);
        if (length == 0)
            return "";
        var buffer = new char[length + 1];
        int copied = GetWindowText(hwnd, buffer, buffer.Length);
        return new string(buffer, 0, copied);
    }

    private static string GetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return "";
        }
    }

    private static PixelRect GetBounds(IntPtr hwnd) =>
        GetWindowRect(hwnd, out var r)
            ? PixelRect.FromEdges(r.Left, r.Top, r.Right, r.Bottom)
            : throw Win32Failure(hwnd, "read the window rectangle");

    private static PixelRect GetClientScreenRect(IntPtr hwnd)
    {
        if (!GetClientRect(hwnd, out var r))
            throw Win32Failure(hwnd, "read the client area");
        var origin = new POINT();
        ClientToScreen(hwnd, ref origin);
        return new PixelRect(origin.X, origin.Y, r.Right - r.Left, r.Bottom - r.Top);
    }

    private static void EnsureWindow(IntPtr hwnd)
    {
        if (!IsWindow(hwnd))
            throw new WindowOperationException($"Window 0x{hwnd.ToInt64():X} no longer exists.");
    }

    private static WindowOperationException Win32Failure(IntPtr hwnd, string action)
    {
        int error = Marshal.GetLastPInvokeError();
        var message = $"Could not {action} for window 0x{hwnd.ToInt64():X} (Win32 error {error}).";
        if (error == ERROR_ACCESS_DENIED)
            message += " The game may be running as administrator; run Guillotine as administrator too.";
        return new WindowOperationException(message);
    }
}
