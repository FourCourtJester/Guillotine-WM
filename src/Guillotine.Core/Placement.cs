namespace Guillotine.Core;

/// <summary>Everything needed to place one window. Resolve it with <see cref="GuillotineSettings.PlacementFor"/>.</summary>
public sealed record Placement(PixelSize Size, Zone Zone, bool Borderless, bool Topmost, bool UseWorkArea);
