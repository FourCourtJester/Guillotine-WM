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

    /// <summary>A title rule matches a window whose title contains <see cref="Match"/>, ignoring case.</summary>
    public const int TitleContains = 1;
    /// <summary>The title starts with <see cref="Match"/>.</summary>
    public const int TitleStartsWith = 2;
    /// <summary>The title is exactly <see cref="Match"/>, or a process rule matched.</summary>
    public const int Exact = 3;

    /// <summary>
    /// How well a window matches: 0 for no match, up to <see cref="Exact"/>. Process rules match the
    /// process name exactly (".exe" is optional); title rules rank exact over prefix over substring,
    /// so a window titled "Heroes of the Storm" beats a terminal or browser tab that only mentions it.
    /// Both ignore case.
    /// </summary>
    public int MatchQuality(string title, string processName)
    {
        const StringComparison ignoreCase = StringComparison.OrdinalIgnoreCase;
        return By switch
        {
            MatchBy.Process => string.Equals(StripExe(processName), StripExe(Match), ignoreCase) ? Exact : 0,
            MatchBy.Title when title.Equals(Match, ignoreCase) => Exact,
            MatchBy.Title when title.StartsWith(Match, ignoreCase) => TitleStartsWith,
            MatchBy.Title when title.Contains(Match, ignoreCase) => TitleContains,
            _ => 0,
        };
    }

    public bool Matches(string title, string processName) => MatchQuality(title, processName) > 0;

    private static string StripExe(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
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

    /// <summary>Global hotkey that shows or hides the app's flyout, such as "Ctrl+Alt+G". Empty turns it off.</summary>
    public string Hotkey { get; init; } = Core.Hotkey.Default.ToString();

    /// <summary>When true, zones are computed from the work area (excluding the taskbar) instead of the full monitor.</summary>
    public bool UseWorkArea { get; init; } = false;

    public List<GameRule> Games { get; init; } = [];

    public PixelSize DesiredSize => new(DesiredWidth, DesiredHeight);

    /// <summary>The game rule that best matches the window (the first one on a tie), or null.</summary>
    public GameRule? FindGame(string title, string processName) =>
        BestMatch(Games, title, processName).Game;

    /// <summary>How well the window matches any configured game (0 for none).</summary>
    public int MatchQuality(string title, string processName) =>
        BestMatch(Games, title, processName).Quality;

    /// <summary>The rule from <paramref name="rules"/> that best matches the window, with its quality.</summary>
    public static (GameRule? Game, int Quality) BestMatch(IEnumerable<GameRule> rules, string title, string processName)
    {
        (GameRule? Game, int Quality) best = (null, 0);
        foreach (var rule in rules)
        {
            int quality = rule.MatchQuality(title, processName);
            if (quality > best.Quality)
                best = (rule, quality);
        }
        return best;
    }

    /// <summary>The global defaults with any overrides from <paramref name="game"/> applied.</summary>
    public Placement PlacementFor(GameRule? game) => new(
        new PixelSize(game?.DesiredWidth ?? DesiredWidth, game?.DesiredHeight ?? DesiredHeight),
        Core.Zone.Parse(game?.Zone ?? DefaultZone),
        game?.Borderless ?? Borderless,
        game?.Topmost ?? Topmost,
        UseWorkArea);

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
        if (!string.IsNullOrEmpty(Hotkey) && !Core.Hotkey.TryParse(Hotkey, out _))
            throw new InvalidDataException($"hotkey '{Hotkey}' is not valid; use something like Ctrl+Alt+G.");
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
