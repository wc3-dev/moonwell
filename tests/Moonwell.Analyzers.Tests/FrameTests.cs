namespace Moonwell.Analyzers.Tests;

/// <summary>MW0043 on the frames package: a player given where a frame's scope goes.</summary>
public class FrameTests
{
    [Fact]
    public void a_player_as_a_frames_scope_is_rejected()
    {
        var found = MapCode.Check("""
            Screen.Add(new Panel(), p);
            Screen.Add(new Panel(), Game.Instance, shownTo: p);
            """);

        Assert.Equal(new[] { "MW0043: p" }, found);
    }

    [Fact]
    public void show_local_runs_only_inside_local_for()
    {
        var found = MapCode.Check("""
            var label = Screen.Add(new Label(), Game.Instance);
            label.ShowLocal("everyone");
            Local.For(p, () => label.ShowLocal("mine"));
            """);

        Assert.Single(found);
        Assert.StartsWith("MW0002: ", found[0]);
    }
}
