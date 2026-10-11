using System.Text.Json;
using System.Text.Json.Serialization;

namespace Guillotine.Core;

/// <summary>How a <see cref="GameRule"/> identifies its window.</summary>
public enum MatchBy
{
    Title,
    Process,
}

/// <summary>A game the tool should recognise, with optional per-game overrides.</summary>
public sealed record GameRule
{
    /// <summary>Text to look for: part of the window title, or the process name without ".exe".</summary>
    public string Match { get; init; } = "";
    public MatchBy By { get; init; } = MatchBy.Title;

    public int? DesiredWidth { get; init; }
    public int? DesiredHeight { get; init; }
    public string? Zone { get; init; }
    public bool? Borderless { get; init; }
    public bool? Topmost { get; init; }
}

/// <summary>
/// User settings loaded from settings.json. Only sizes and preferences live here;
/// zone coordinates are always computed at runtime.
/// </summary>
public sealed record GuillotineSettings
{
    public int DesiredWidth { get; init; } = 2560;
    public int DesiredHeight { get; init; } = 1440;
    public string DefaultZone { get; init; } = "center";
    public bool Borderless { get; init; } = true;
    public bool Topmost { get; init; } = false;

    /// <summary>When true, zones are computed from the work area (excluding the taskbar) instead of the full monitor.</summary>
    public bool UseWorkArea { get; init; } = false;

    public List<GameRule> Games { get; init; } = [];

    public PixelSize DesiredSize => new(DesiredWidth, DesiredHeight);

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static GuillotineSettings FromJson(string json)
    {
        var settings = JsonSerializer.Deserialize<GuillotineSettings>(json, JsonOptions)
            ?? throw new InvalidDataException("settings.json is empty.");
        settings.Validate();
        return settings;
    }

    public static GuillotineSettings Load(string path) => FromJson(File.ReadAllText(path));

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    private void Validate()
    {
        if (DesiredWidth <= 0 || DesiredHeight <= 0)
            throw new InvalidDataException("desiredWidth and desiredHeight must be positive.");
        if (!Core.Zone.TryParse(DefaultZone, out _))
            throw new InvalidDataException($"defaultZone '{DefaultZone}' is not a known zone.");

        foreach (var game in Games)
        {
            if (string.IsNullOrWhiteSpace(game.Match))
                throw new InvalidDataException("Every entry in games needs a non-empty match.");
            if (game.DesiredWidth is <= 0 || game.DesiredHeight is <= 0)
                throw new InvalidDataException($"Game '{game.Match}' has a non-positive desired size.");
            if (game.Zone is not null && !Core.Zone.TryParse(game.Zone, out _))
                throw new InvalidDataException($"Game '{game.Match}' has unknown zone '{game.Zone}'.");
        }
    }
}
