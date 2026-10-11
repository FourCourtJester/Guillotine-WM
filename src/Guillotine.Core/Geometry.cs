using System.Globalization;

namespace Guillotine.Core;

/// <summary>A width and height in physical pixels.</summary>
public readonly record struct PixelSize(int Width, int Height)
{
    /// <summary>Parses "2560x1440" (also accepts "X" or "*" as the separator).</summary>
    public static PixelSize Parse(string text)
    {
        if (TryParse(text, out var size))
            return size;
        throw new FormatException($"Expected a size like 2560x1440, got '{text}'.");
    }

    public static bool TryParse(string? text, out PixelSize size)
    {
        size = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text.Trim().Split('x', 'X', '*');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var width)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var height)
            || width <= 0 || height <= 0)
            return false;

        size = new PixelSize(width, height);
        return true;
    }

    public override string ToString() => $"{Width}x{Height}";
}

/// <summary>
/// A rectangle in physical (virtual-screen) pixels. Right and Bottom are exclusive,
/// matching the Win32 RECT convention.
/// </summary>
public readonly record struct PixelRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
    public PixelSize Size => new(Width, Height);

    public static PixelRect FromEdges(int left, int top, int right, int bottom) =>
        new(left, top, right - left, bottom - top);

    public override string ToString() => $"{Width}x{Height} at ({Left}, {Top})";
}
