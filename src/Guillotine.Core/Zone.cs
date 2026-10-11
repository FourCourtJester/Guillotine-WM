namespace Guillotine.Core;

public enum HorizontalZone
{
    Left,
    Center,
    Right,
}

public enum VerticalZone
{
    Top,
    Center,
    Bottom,
}

/// <summary>
/// A placement on a monitor, such as "center" or "top-left". Zones are never pixel
/// coordinates; <see cref="ZoneCalculator"/> resolves them against the current monitor.
/// </summary>
public readonly record struct Zone(HorizontalZone Horizontal, VerticalZone Vertical)
{
    public static readonly Zone Center = new(HorizontalZone.Center, VerticalZone.Center);

    public static IEnumerable<Zone> All =>
        from vertical in Enum.GetValues<VerticalZone>()
        from horizontal in Enum.GetValues<HorizontalZone>()
        select new Zone(horizontal, vertical);

    /// <summary>
    /// Parses a zone name. A single word sets one axis and centers the other
    /// ("left", "center", "bottom"). Two words set both, vertical first ("top-left",
    /// "bottom-center"); "-", "_" and spaces are accepted as separators.
    /// </summary>
    public static Zone Parse(string text)
    {
        if (TryParse(text, out var zone))
            return zone;
        throw new FormatException(
            $"Unknown zone '{text}'. Use left, center, right, top, bottom, or a pair such as top-left.");
    }

    public static bool TryParse(string? text, out Zone zone)
    {
        zone = Center;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var words = text.Trim().ToLowerInvariant()
            .Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        switch (words.Length)
        {
            case 1:
                if (words[0] == "center")
                    return true;
                if (TryParseHorizontal(words[0], out var h))
                {
                    zone = zone with { Horizontal = h };
                    return true;
                }
                if (TryParseVertical(words[0], out var v))
                {
                    zone = zone with { Vertical = v };
                    return true;
                }
                return false;

            case 2:
                if (TryParseVertical(words[0], out var vertical) && TryParseHorizontal(words[1], out var horizontal))
                {
                    zone = new Zone(horizontal, vertical);
                    return true;
                }
                return false;

            default:
                return false;
        }
    }

    private static bool TryParseHorizontal(string word, out HorizontalZone value) => TryParseName(word, out value);

    private static bool TryParseVertical(string word, out VerticalZone value) => TryParseName(word, out value);

    // Enum.TryParse also accepts numbers ("1"), so only take names.
    private static bool TryParseName<T>(string word, out T value) where T : struct, Enum
    {
        value = default;
        return word.All(char.IsLetter) && Enum.TryParse(word, ignoreCase: true, out value);
    }

    public override string ToString() => (Horizontal, Vertical) switch
    {
        (HorizontalZone.Center, VerticalZone.Center) => "center",
        (_, VerticalZone.Center) => Horizontal.ToString().ToLowerInvariant(),
        (HorizontalZone.Center, _) => Vertical.ToString().ToLowerInvariant(),
        _ => $"{Vertical.ToString().ToLowerInvariant()}-{Horizontal.ToString().ToLowerInvariant()}",
    };
}
