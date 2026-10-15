using System.Windows.Input;
using Guillotine.Core;

namespace Guillotine.App;

/// <summary>Converts between WPF keys, hotkey key names ("G", "1", "F5") and Win32 virtual keys.</summary>
public static class HotkeyKeys
{
    /// <summary>The name to store for <paramref name="key"/>, or null for modifier keys and keys that cannot be used.</summary>
    public static string? NameFor(Key key) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.System or Key.None or Key.ImeProcessed or Key.DeadCharProcessed => null,
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        _ => key.ToString(),
    };

    public static bool TryGetVirtualKey(string name, out uint virtualKey)
    {
        virtualKey = 0;
        Key key;
        if (name.Length == 1 && char.IsDigit(name[0]))
            key = Key.D0 + (name[0] - '0');
        else if (!Enum.TryParse(name, ignoreCase: true, out key) || int.TryParse(name, out _))
            return false;

        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        return virtualKey != 0;
    }

    public static HotkeyModifiers ToModifiers(ModifierKeys modifiers)
    {
        var result = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= HotkeyModifiers.Ctrl;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= HotkeyModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= HotkeyModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= HotkeyModifiers.Win;
        return result;
    }
}
