namespace Moonwell.Analyzers.Tests;

/// <summary>MW0001: a player's team and a team's players are read inside Local.For; moving a player, winning, losing and ending the game are changes (decisions 197 and 198).</summary>
public class TeamTests
{
    [Fact]
    public void reads_are_allowed_inside_and_changes_are_not()
    {
        var found = MapCode.Check("""
            var t = p.Team;
            Local.For(p, () =>
            {
                var name = p.Team.Name;
                var number = t.Number;
                var count = t.Players.Count;
                var shared = t.SharedVision;
                p.Team = t;
                p.Win();
                p.Lose("Out");
                Game.Over(t);
                Game.Over(p);
            });
            """);

        Assert.Equal(5, found.Count);
        Assert.All(found, f => Assert.StartsWith("MW0001: ", f));
    }
}
