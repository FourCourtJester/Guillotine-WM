namespace Guillotine.Core;

/// <summary>Modifier keys for a global hotkey. The values match Win32 RegisterHotKey's MOD_* flags.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// A global hotkey such as "Ctrl+Alt+G". <see cref="Key"/> is a key name (a letter, digit, or a name
/// such as F5 or Space); the app maps it to a virtual key.
/// </summary>
public readonly record struct Hotkey(HotkeyModifiers Modifiers, string Key)
{
    public static readonly Hotkey Default = new(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "G");

    /// <summary>
    /// True when the hotkey would not get in the way of typing: it needs Ctrl, Alt or Win
    /// (Shift alone only changes letters to capitals).
    /// </summary>
    public bool IsUsable =>
        !string.IsNullOrEmpty(Key) &&
        (Modifiers & (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Win)) != 0;

    public static Hotkey Parse(string text) =>
        TryParse(text, out var hotkey)
            ? hotkey
            : throw new FormatException($"'{text}' is not a hotkey like Ctrl+Alt+G (it needs Ctrl, Alt or Win plus one key).");

    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var modifiers = HotkeyModifiers.None;
        string? key = null;
        foreach (var raw in text.Split('+'))
        {
            var part = raw.Trim();
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= HotkeyModifiers.Ctrl; break;
                case "alt": modifiers |= HotkeyModifiers.Alt; break;
                case "shift": modifiers |= HotkeyModifiers.Shift; break;
                case "win" or "windows": modifiers |= HotkeyModifiers.Win; break;
                default:
                    if (key is not null || part.Length == 0 || !part.All(char.IsLetterOrDigit))
                        return false;
                    key = part.Length == 1 ? part.ToUpperInvariant() : char.ToUpperInvariant(part[0]) + part[1..];
                    break;
            }
        }

        hotkey = new Hotkey(modifiers, key ?? "");
        return hotkey.IsUsable;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(Key);
        return string.Join("+", parts);
    }
}
