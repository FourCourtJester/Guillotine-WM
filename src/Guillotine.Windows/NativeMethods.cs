using System.Runtime.InteropServices;

namespace Guillotine.Windows;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    internal const uint MONITORINFOF_PRIMARY = 0x1;
    internal const int MDT_EFFECTIVE_DPI = 0;
    internal static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

    internal const int GWL_STYLE = -16;
    internal const int GWL_EXSTYLE = -20;
    internal const uint GW_HWNDPREV = 3;
    internal const uint GW_OWNER = 4;
    internal const uint MONITOR_DEFAULTTONEAREST = 2;
    internal const int DWMWA_CLOAKED = 14;
    internal const int SW_RESTORE = 9;
    internal const int ERROR_ACCESS_DENIED = 5;

    internal static readonly IntPtr HWND_TOPMOST = new(-1);
    internal static readonly IntPtr HWND_NOTOPMOST = new(-2);

    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_FRAMECHANGED = 0x0020;
    internal const uint SWP_NOOWNERZORDER = 0x0200;
    internal const uint SWP_NOSENDCHANGING = 0x0400;

    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>Reads GWL_STYLE or GWL_EXSTYLE. Styles are 32-bit flags on every architecture.</summary>
    internal static uint GetWindowStyle(IntPtr hWnd, int index) =>
        IntPtr.Size == 8 ? unchecked((uint)(long)GetWindowLongPtr64(hWnd, index)) : unchecked((uint)GetWindowLong32(hWnd, index));

    /// <summary>Writes GWL_STYLE or GWL_EXSTYLE. Returns false and sets the last error on failure.</summary>
    internal static bool SetWindowStyle(IntPtr hWnd, int index, uint value)
    {
        // A zero return is only a failure if the last error is set, because the previous value may have been 0.
        Marshal.SetLastPInvokeError(0);
        long previous = IntPtr.Size == 8
            ? (long)SetWindowLongPtr64(hWnd, index, new IntPtr(unchecked((int)value)))
            : SetWindowLong32(hWnd, index, unchecked((int)value));
        return previous != 0 || Marshal.GetLastPInvokeError() == 0;
    }

    [DllImport("user32.dll", EntryPoint = "SetPropW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetProp(IntPtr hWnd, string lpString, IntPtr hData);

    [DllImport("user32.dll", EntryPoint = "GetPropW", CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetProp(IntPtr hWnd, string lpString);

    [DllImport("user32.dll", EntryPoint = "RemovePropW", CharSet = CharSet.Unicode)]
    internal static extern IntPtr RemoveProp(IntPtr hWnd, string lpString);

    [DllImport("kernel32.dll")]
    internal static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetMenu(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AdjustWindowRectExForDpi(ref RECT lpRect, uint dwStyle,
        [MarshalAs(UnmanagedType.Bool)] bool bMenu, uint dwExStyle, uint dpi);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    internal const uint MOD_NOREPEAT = 0x4000;
    internal const int WM_HOTKEY = 0x0312;

    [StructLayout(LayoutKind.Sequential)]
    internal struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    internal const uint ABM_GETTASKBARPOS = 0x00000005;

    [DllImport("shell32.dll")]
    internal static extern UIntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);

    internal const uint TH32CS_SNAPPROCESS = 0x00000002;
    internal static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct PROCESSENTRY32W
    {
        public int dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr hObject);

    internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetProcessDpiAwarenessContext(IntPtr value);
}
