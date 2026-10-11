using Guillotine.Core;

namespace Guillotine.Core.Tests;

public class SettingsTests
{
    [Fact]
    public void Repository_settings_file_loads()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "settings.json");

        var settings = GuillotineSettings.Load(path);

        Assert.Equal(new PixelSize(2560, 1440), settings.DesiredSize);
        Assert.Equal(Zone.Center, Zone.Parse(settings.DefaultZone));
        Assert.True(settings.Borderless);
        Assert.False(settings.Topmost);
        var game = Assert.Single(settings.Games);
        Assert.Equal("Heroes of the Storm", game.Match);
        Assert.Equal(MatchBy.Title, game.By);
    }

    [Fact]
    public void Missing_fields_use_defaults()
    {
        var settings = GuillotineSettings.FromJson("{}");

        Assert.Equal(new GuillotineSettings().ToJson(), settings.ToJson());
    }

    [Fact]
    public void Per_game_overrides_load()
    {
        var settings = GuillotineSettings.FromJson("""
            {
              // comments and trailing commas are allowed
              "games": [
                { "match": "Wow", "by": "process", "desiredWidth": 1920, "desiredHeight": 1080, "zone": "top-left", "topmost": true },
              ],
            }
            """);

        var game = Assert.Single(settings.Games);
        Assert.Equal(MatchBy.Process, game.By);
        Assert.Equal(1920, game.DesiredWidth);
        Assert.Equal("top-left", game.Zone);
        Assert.True(game.Topmost);
        Assert.Null(game.Borderless);
    }

    [Theory]
    [InlineData("""{ "desiredWidth": 0 }""")]
    [InlineData("""{ "defaultZone": "middle" }""")]
    [InlineData("""{ "games": [ { "match": "" } ] }""")]
    [InlineData("""{ "games": [ { "match": "x", "zone": "nowhere" } ] }""")]
    [InlineData("""{ "games": [ { "match": "x", "desiredHeight": -1 } ] }""")]
    public void Invalid_settings_are_rejected(string json)
    {
        Assert.Throws<InvalidDataException>(() => GuillotineSettings.FromJson(json));
    }
}
