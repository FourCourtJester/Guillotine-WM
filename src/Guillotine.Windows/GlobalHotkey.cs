using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Guillotine.Core;
using static Guillotine.Windows.NativeMethods;

namespace Guillotine.Windows;

/// <summary>
/// A system-wide hotkey delivered to a window as WM_HOTKEY. The owner window's message hook must call
/// <see cref="IsHotkeyMessage"/> to recognise it.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GlobalHotkey(IntPtr window, int id) : IDisposable
{
    public const int WmHotkey = WM_HOTKEY;

    public Hotkey? Current { get; private set; }

    /// <summary>
    /// Registers <paramref name="hotkey"/> (virtual key <paramref name="virtualKey"/>), replacing the
    /// previous one. Returns false and keeps the previous hotkey if Windows refuses, usually because
    /// another program already uses that combination.
    /// </summary>
    public bool TryRegister(Hotkey hotkey, uint virtualKey, out string? error)
    {
        var previous = Current;
        Unregister();

        if (RegisterHotKey(window, id, (uint)hotkey.Modifiers | MOD_NOREPEAT, virtualKey))
        {
            Current = hotkey;
            error = null;
            return true;
        }

        error = Marshal.GetLastPInvokeError() == 1409 // ERROR_HOTKEY_ALREADY_REGISTERED
            ? $"{hotkey} is already used by another program."
            : $"Windows would not register {hotkey} (error {Marshal.GetLastPInvokeError()}).";
        Current = null;
        return false;
    }

    public bool IsHotkeyMessage(int message, IntPtr wParam) => message == WM_HOTKEY && wParam.ToInt32() == id;

    public void Unregister()
    {
        if (Current is not null)
            UnregisterHotKey(window, id);
        Current = null;
    }

    public void Dispose() => Unregister();
}
