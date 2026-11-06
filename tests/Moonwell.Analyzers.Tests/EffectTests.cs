namespace Moonwell.Analyzers.Tests;

/// <summary>MW0001: what of a special effect may run inside Local.For, its reads, and none of its changes.</summary>
public class EffectTests
{
    [Fact]
    public void reads_are_allowed_inside_and_changes_are_not()
    {
        var found = MapCode.Check("""
            var fx = SpecialEffect.Create("m.mdl", 0f, 0f, Game.Instance);
            Local.For(p, () =>
            {
                var x = fx.X;
                var h = fx.Height;
                var m = fx.Model;
                var d = fx.PlaysDeath;
                var s = fx.IsShownTo(p);
                var f = fx.Facing;
                fx.Height = 5f;
                fx.PlayAnimation("stand");
                SpecialEffect.Create("m.mdl", 0f, 0f, Game.Instance);
            });
            """);

        Assert.Equal(3, found.Count);
        Assert.All(found, f => Assert.StartsWith("MW0001: ", f));
    }

    [Fact]
    public void text_and_sound_reads_are_allowed_inside_and_changes_are_not()
    {
        var found = MapCode.Check("""
            var text = FloatingText.Create("a", 0f, 0f, Game.Instance);
            var hum = Sound.Create("a.wav", Game.Instance);
            Local.For(p, () =>
            {
                var t = text.Text;
                var c = text.Color;
                var d = text.Duration;
                var s = text.IsShownTo(p);
                var v = hum.Volume;
                var h = hum.IsHeardBy(p);
                text.Text = "b";
                hum.Volume = 5f;
                FloatingText.Pop("c", 0f, 0f);
                Sound.Play("a.wav");
                Music.Play("a.mp3");
            });
            """);

        Assert.Equal(5, found.Count);
        Assert.All(found, f => Assert.StartsWith("MW0001: ", f));
    }
}
