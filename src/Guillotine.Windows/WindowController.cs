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
    /// Windows that belong to this process are left out.
    /// </summary>
    public static IReadOnlyList<WindowInfo> GetWindows()
    {
        var handles = new List<IntPtr>();
        EnumWindows((hwnd, _) =>
        {
            handles.Add(hwnd);
            return true;
        }, IntPtr.Zero);

        int ownProcessId = Environment.ProcessId;
        var processNames = new Dictionary<int, string>();
        var windows = new List<WindowInfo>();

        foreach (var hwnd in handles)
        {
            if (!IsCandidate(hwnd))
                continue;

            var info = TryDescribe(hwnd, processNames);
            if (info is not null && info.ProcessId != ownProcessId)
                windows.Add(info);
        }
        return windows;
    }

    /// <summary>Windows whose title or process matches <paramref name="game"/>.</summary>
    public static IReadOnlyList<WindowInfo> Find(GameRule game) =>
        GetWindows().Where(w => game.Matches(w.Title, w.ProcessName)).ToList();

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

        var monitor = Monitors.Get(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST));
        var area = placement.UseWorkArea ? monitor.WorkArea : monitor.Bounds;
        var target = ZoneCalculator.Compute(area, placement.Size, placement.Zone);

        // Some windows adjust their frame after the first resize (menus wrapping, DPI changes),
        // so measure the frame again and correct once if the first attempt is off.
        var actual = MoveClientTo(hwnd, target);
        if (actual != target)
            actual = MoveClientTo(hwnd, target);

        return new ApplyResult(Describe(hwnd), monitor, target, actual);
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

        uint newStyle, newExStyle;
        if (borderless)
        {
            if (GetProp(hwnd, SavedMarkerProp) == IntPtr.Zero)
            {
                SetProp(hwnd, SavedStyleProp, new IntPtr(unchecked((int)style)));
                SetProp(hwnd, SavedExStyleProp, new IntPtr(unchecked((int)exStyle)));
                SetProp(hwnd, SavedMarkerProp, new IntPtr(1));
            }
            (newStyle, newExStyle) = WindowStyles.MakeBorderless(style, exStyle);
        }
        else if (GetProp(hwnd, SavedMarkerProp) != IntPtr.Zero)
        {
            newStyle = unchecked((uint)GetProp(hwnd, SavedStyleProp).ToInt32());
            newExStyle = unchecked((uint)GetProp(hwnd, SavedExStyleProp).ToInt32());
            // Keep the current topmost state; that is toggled separately.
            newExStyle = (newExStyle & ~WindowStyles.WS_EX_TOPMOST) | (exStyle & WindowStyles.WS_EX_TOPMOST);
        }
        else
        {
            (newStyle, newExStyle) = WindowStyles.MakeFramed(style, exStyle);
        }

        if (!SetWindowStyle(hwnd, GWL_STYLE, newStyle) || !SetWindowStyle(hwnd, GWL_EXSTYLE, newExStyle))
            throw Win32Failure(hwnd, "change the window style");

        if (!borderless)
            ClearSavedStyles(hwnd);

        // Style changes only take effect on screen after SWP_FRAMECHANGED.
        if (!SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_FRAMECHANGED))
            throw Win32Failure(hwnd, "redraw the window frame");
    }

    public static void SetTopmost(IntPtr hwnd, bool topmost)
    {
        EnsureWindow(hwnd);
        if (!SetWindowPos(hwnd, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER))
            throw Win32Failure(hwnd, topmost ? "make the window always on top" : "clear always on top");
    }

    /// <summary>Puts back the original frame and turns off always on top.</summary>
    public static WindowInfo Restore(IntPtr hwnd)
    {
        SetBorderless(hwnd, false);
        SetTopmost(hwnd, false);
        return Describe(hwnd);
    }

    private static PixelRect MoveClientTo(IntPtr hwnd, PixelRect target)
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
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_FRAMECHANGED))
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
