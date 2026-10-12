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
    public void FindGame_returns_best_match_or_null()
    {
        var settings = GuillotineSettings.FromJson("""
            { "games": [ { "match": "Storm" }, { "match": "Heroes" }, { "match": "wow", "by": "process" } ] }
            """);

        // "Heroes" matches the start of the title, which beats "Storm" appearing somewhere in it.
        Assert.Equal("Heroes", settings.FindGame("Heroes of the Storm", "x")?.Match);
        Assert.Equal("Storm", settings.FindGame("A Storm Is Coming", "x")?.Match);
        Assert.Equal("wow", settings.FindGame("World of Warcraft", "Wow.exe")?.Match);
        Assert.Null(settings.FindGame("Notepad", "notepad"));
    }
}

public class MatchQualityTests
{
    private static readonly GameRule Heroes = new() { Match = "Heroes of the Storm", By = MatchBy.Title };

    [Theory]
    [InlineData("Heroes of the Storm", GameRule.Exact)]
    [InlineData("heroes of the storm", GameRule.Exact)]
    [InlineData("Heroes of the Storm - Loading", GameRule.TitleStartsWith)]
    [InlineData("Administrator: guillotine-cli apply --title \"Heroes of the Storm\"", GameRule.TitleContains)]
    [InlineData("Notepad", 0)]
    public void Title_matches_are_ranked(string title, int expected)
    {
        Assert.Equal(expected, Heroes.MatchQuality(title, "whatever"));
    }

    [Fact]
    public void Process_match_counts_as_exact()
    {
        var rule = new GameRule { Match = "HeroesOfTheStorm_x64", By = MatchBy.Process };

        Assert.Equal(GameRule.Exact, rule.MatchQuality("anything", "HeroesOfTheStorm_x64"));
    }

    [Fact]
    public void FindGame_prefers_the_better_match_over_rule_order()
    {
        var settings = GuillotineSettings.FromJson("""
            { "games": [ { "match": "Storm" }, { "match": "Heroes of the Storm" } ] }
            """);

        Assert.Equal("Heroes of the Storm", settings.FindGame("Heroes of the Storm", "x")?.Match);
        Assert.Equal(GameRule.Exact, settings.MatchQuality("Heroes of the Storm", "x"));
        Assert.Equal(0, settings.MatchQuality("Notepad", "notepad"));
    }
}
