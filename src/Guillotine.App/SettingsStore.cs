using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Guillotine.Core;

namespace Guillotine.App;

/// <summary>
/// Finds, loads and updates settings.json. Updates change only the keys they touch, so the user's other
/// settings and games are kept. If the folder next to the exe is not writable (for example under
/// Program Files), settings are saved to %APPDATA%\Guillotine instead, which is then read first.
/// </summary>
public sealed class SettingsStore
{
    private static readonly string ExeSettings = Path.Combine(AppContext.BaseDirectory, "settings.json");
    private static readonly string UserSettings = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Guillotine", "settings.json");

    public GuillotineSettings Settings { get; private set; } = new();
    public string? LoadError { get; private set; }
    public string Path { get; private set; } = ExeSettings;

    public void Load()
    {
        LoadError = null;
        foreach (var candidate in new[] { UserSettings, ExeSettings, System.IO.Path.GetFullPath("settings.json") })
        {
            if (!File.Exists(candidate))
                continue;
            Path = candidate;
            try
            {
                Settings = GuillotineSettings.Load(candidate);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or FormatException or UnauthorizedAccessException)
            {
                Settings = new GuillotineSettings();
                LoadError = $"{candidate}: {ex.Message} Using defaults.";
            }
            return;
        }
        Path = ExeSettings;
        Settings = new GuillotineSettings();
    }

    /// <summary>Sets one top-level key and saves. Returns an error message, or null on success.</summary>
    public string? Save(string key, JsonNode? value)
    {
        JsonObject root;
        try
        {
            root = File.Exists(Path) ? ParseObject(File.ReadAllText(Path)) : new JsonObject();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            return $"Could not read {Path}: {ex.Message}";
        }
        root[key] = value;
        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        foreach (var target in Path == UserSettings ? [UserSettings] : new[] { Path, UserSettings })
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
                File.WriteAllText(target, json);
                Path = target;
                Settings = GuillotineSettings.FromJson(json);
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Try the next location.
            }
            catch (InvalidDataException ex)
            {
                return ex.Message;
            }
        }
        return $"Could not save settings to {Path} or {UserSettings}.";
    }

    private static JsonObject ParseObject(string json) =>
        JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        }) as JsonObject ?? new JsonObject();
}
