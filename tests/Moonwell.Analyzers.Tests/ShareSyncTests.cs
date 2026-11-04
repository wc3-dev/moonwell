namespace Moonwell.Analyzers.Tests;

/// <summary>A scope's Share runs its first lambda on one player's machine, so the analyzer checks it as a Local.For block (decision 179).</summary>
public class ShareSyncTests
{
    [Fact]
    public void a_change_in_the_read_is_rejected()
    {
        var found = MapCode.Check("""
            Game.Instance.Share(p, () => { Natives.KillUnit(u); return 1; }, n => { });
            """);

        Assert.Equal(new[] { "MW0001: Natives.KillUnit(u)" }, found);
    }

    [Fact]
    public void a_machine_local_read_in_the_read_is_allowed()
    {
        Assert.Empty(MapCode.Check("""
            Game.Instance.Share(p, () => RealTime.LocalNow.Hour, n => { });
            Game.Instance.Share(p, () => PlayerFile.Read("a.txt"), text => { });
            Game.Instance.Share(p, () => Natives.GetLocalPlayer() == p.Handle, isMe => { });
            """));
    }

    [Fact]
    public void the_callback_runs_on_every_machine()
    {
        var found = MapCode.Check("""
            Game.Instance.Share(p, () => 1, n => { var h = RealTime.LocalNow.Hour; });
            """);

        Assert.Equal(new[] { "MW0002: RealTime.LocalNow" }, found);
    }

    [Fact]
    public void machine_local_reads_outside_are_rejected()
    {
        var found = MapCode.Check("""
            var h = RealTime.LocalNow.Hour;
            var t = PlayerFile.Read("a.txt");
            """);

        Assert.Equal(new[] { "MW0002: RealTime.LocalNow", "MW0002: PlayerFile.Read(\"a.txt\")" }, found);
    }
}
