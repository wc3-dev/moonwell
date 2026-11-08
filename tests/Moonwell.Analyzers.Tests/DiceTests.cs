namespace Moonwell.Analyzers.Tests;

/// <summary>MW0001: Game.Random and a Dice draw inside Local.For, where only one machine would draw (decision 217).</summary>
public class DiceTests
{
    [Fact]
    public void dice_are_allowed_outside_and_not_inside()
    {
        var found = MapCode.Check("""
            var dice = new Dice(42);
            var roll = Game.Random.Int(1, 6) + dice.Int(1, 6);
            Local.For(p, () =>
            {
                var a = Game.Random.Int(1, 6);
                var b = dice.Chance(0.5f);
            });
            """);

        Assert.Equal(new[] { "MW0001: Game.Random.Int(1, 6)", "MW0001: dice.Chance(0.5f)" }, found);
    }
}
