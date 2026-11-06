namespace Moonwell.Analyzers.Tests;

/// <summary>MW0001: what of weather, lightning, vision, pings and minimap icons may run inside Local.For: reads and pings, and none of the changes.</summary>
public class LookTests
{
    [Fact]
    public void reads_and_pings_are_allowed_inside_and_changes_are_not()
    {
        var found = MapCode.Check("""
            var rain = Weather.Create(WeatherKind.LordaeronHeavyRain, Game.Instance);
            var bolt = Lightning.Create(LightningKind.DrainLife, 0f, 0f, 1f, 1f, Game.Instance);
            var sight = p.Reveal(0f, 0f, 100f, Game.Instance);
            var icon = MinimapIcon.Create(MinimapIcon.Boss, 0f, 0f, Color.Red, Game.Instance);
            Local.For(p, () =>
            {
                var k = rain.Kind;
                var x = bolt.X1;
                var h = bolt.FromHeight;
                var who = sight.Player;
                var y = icon.Y;
                var fog = Game.Fog;
                Game.Ping(0f, 0f);
                p.Ping(0f, 0f, Color.Gold, 2f, PingStyle.Flashy);
                rain.End();
                bolt.MoveTo(0f, 0f, 2f, 2f);
                p.Hide(0f, 0f, 100f, Game.Instance);
                Game.BlackMask = false;
            });
            """);

        Assert.Equal(4, found.Count);
        Assert.All(found, f => Assert.StartsWith("MW0001: ", f));
    }

    [Fact]
    public void a_player_where_the_scope_goes_is_MW0043()
    {
        var found = MapCode.Check("""
            p.Reveal(0f, 0f, 100f, p);
            Weather.Create(WeatherKind.NorthrendLightSnow, p);
            """);

        Assert.Equal(2, found.Count);
        Assert.All(found, f => Assert.StartsWith("MW0043: ", f));
    }
}
