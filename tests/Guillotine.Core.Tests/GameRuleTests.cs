using Guillotine.Core;

namespace Guillotine.Core.Tests;

public class GameRuleTests
{
    [Theory]
    [InlineData("Heroes of the Storm", true)]
    [InlineData("heroes of the storm", true)]
    [InlineData("Heroes of the Storm - Loading", true)]
    [InlineData("Heroes", false)]
    public void Title_rule_matches_substring_ignoring_case(string title, bool expected)
    {
        var rule = new GameRule { Match = "Heroes of the Storm", By = MatchBy.Title };

        Assert.Equal(expected, rule.Matches(title, "HeroesOfTheStorm_x64"));
    }

    [Theory]
    [InlineData("HeroesOfTheStorm_x64", "HeroesOfTheStorm_x64", true)]
    [InlineData("HeroesOfTheStorm_x64.exe", "heroesofthestorm_x64", true)]
    [InlineData("HeroesOfTheStorm_x64", "HeroesOfTheStorm_x64.EXE", true)]
    [InlineData("HeroesOfTheStorm", "HeroesOfTheStorm_x64", false)]
    public void Process_rule_matches_whole_name(string match, string processName, bool expected)
    {
        var rule = new GameRule { Match = match, By = MatchBy.Process };

        Assert.Equal(expected, rule.Matches("any title", processName));
    }

    [Fact]
    public void Placement_uses_defaults_without_a_game()
    {
        var settings = new GuillotineSettings();

        Assert.Equal(new Placement(new PixelSize(2560, 1440), Zone.Center, true, false, false), settings.PlacementFor(null));
    }

    [Fact]
    public void Placement_applies_game_overrides()
    {
        var settings = GuillotineSettings.FromJson("""
            {
              "useWorkArea": true,
              "games": [
                { "match": "Game", "desiredHeight": 1080, "zone": "bottom-left", "borderless": false, "topmost": true }
              ]
            }
            """);

        var game = settings.FindGame("My Game", "game");

        Assert.NotNull(game);
        Assert.Equal(
            new Placement(new PixelSize(2560, 1080), Zone.Parse("bottom-left"), false, true, true),
            settings.PlacementFor(game));
    }

    [Fact]
    public void FindGame_returns_first_match_or_null()
    {
        var settings = GuillotineSettings.FromJson("""
            { "games": [ { "match": "Storm" }, { "match": "Heroes" }, { "match": "wow", "by": "process" } ] }
            """);

        Assert.Equal("Storm", settings.FindGame("Heroes of the Storm", "x")?.Match);
        Assert.Equal("wow", settings.FindGame("World of Warcraft", "Wow.exe")?.Match);
        Assert.Null(settings.FindGame("Notepad", "notepad"));
    }
}
