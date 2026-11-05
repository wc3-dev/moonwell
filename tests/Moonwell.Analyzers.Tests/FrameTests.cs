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
    public void making_adding_and_setting_frames_inside_local_for_is_mw0001()
    {
        var found = MapCode.Check("""
            var label = Screen.Add(new Label(), Game.Instance);
            Local.For(p, () => Screen.Add(new Label(), Game.Instance));
            Local.For(p, () => label.Add(new Image()));
            Local.For(p, () => label.Text = "mine");
            Local.For(p, () => { var text = label.Text; var made = new Label(); });
            """);

        Assert.Equal(new[] { "MW0001: Screen.Add(new Label(), Game.Instance)", "MW0001: label.Add(new Image())", "MW0001: label.Text" }, found);
    }

    [Fact]
    public void subscribing_inside_local_for_is_refused()
    {
        var found = MapCode.Check("""
            var label = Screen.Add(new Label(), Game.Instance);
            label.Wheel += (who, notches) => { };
            Local.For(p, () => label.Wheel += (who, notches) => { });
            """);

        Assert.Equal(new[] { "MW0048: label.Wheel" }, found);
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

    [Fact]
    public void subscribing_inside_local_for_to_a_frame_from_a_property_or_a_method_is_refused()
    {
        var found = MapCode.Check("""
            Local.For(p, () => Shown.Wheel += (who, notches) => { });
            Local.For(p, () => Find().Wheel += (who, notches) => { });
            Local.For(p, () => { var mine = new Label(); mine.Wheel += (who, notches) => { }; });
            """, """
            private static Label kept = new Label();
            private static Label Shown => kept;
            private static Label Find() => kept;
            """);

        Assert.Equal(new[] { "MW0048: Shown.Wheel", "MW0048: Find().Wheel" }, found);
    }
}
