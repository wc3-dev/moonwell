namespace Moonwell.Analyzers.Tests;

/// <summary>
/// MW0038 and MW0002 for .NET's own machine-local values: Random without a seed, the clock, and an object's address
/// (GetHashCode, a handle as text). In the game's Lua math.random is the game's synced generator, and CoreSystem's
/// Random without a seed reseeds it with os.time() (seen 2026-10-26).
/// </summary>
public class ClockTests
{
    [Fact]
    public void random_without_a_seed_is_rejected_everywhere()
    {
        var found = MapCode.Check("""
            var r = new Random();
            Random s = new();
            Local.For(p, () => { var t = new Random(); });
            var seeded = new Random(Natives.GetRandomInt(0, 1000)).Next(0, 9);
            """);

        Assert.Equal(new[] { "MW0038: new Random()", "MW0038: new()", "MW0038: new Random()" }, found);
    }

    [Fact]
    public void random_without_a_seed_says_what_to_write()
    {
        Assert.Equal(
            "'Random' seeds itself from this machine's clock and reseeds the game's random generator with it, which desyncs every random number after it, inside Local.For too; use Game.Random, or new Dice(seed) with a seed every machine shares",
            MapCode.Message("var r = new Random();"));
    }

    [Fact]
    public void random_shared_is_not_in_the_lua_core()
    {
        Assert.Equal(new[] { "MW0030: Shared" }, MapCode.Check("var r = Random.Shared.Next(0, 9);"));
    }

    [Fact]
    public void the_clock_is_rejected_outside_and_read_inside()
    {
        var found = MapCode.Check("""
            var now = DateTime.Now.Second;
            var utc = DateTime.UtcNow;
            var day = DateTime.Today;
            var tick = Environment.TickCount;
            if (Environment.TickCount64 > 0) { }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var seconds = watch.Elapsed.Seconds;
            if (System.Diagnostics.Stopwatch.GetTimestamp() > 0) { }
            Local.For(p, () =>
            {
                var shown = DateTime.Now.Second + Environment.TickCount + watch.Elapsed.Seconds;
            });
            """);

        Assert.Equal(new[]
        {
            "MW0002: DateTime.Now",
            "MW0002: DateTime.UtcNow",
            "MW0002: DateTime.Today",
            "MW0002: Environment.TickCount",
            "MW0002: Environment.TickCount64",
            "MW0002: watch.Elapsed",
            "MW0002: System.Diagnostics.Stopwatch.GetTimestamp()",
        }, found.Where(f => f.StartsWith("MW0002")));
        // DateTime itself fails in the game, inside Local.For too (MW0030).
        Assert.Equal(4, found.Count(f => f == "MW0030: DateTime"));
    }

    [Fact]
    public void the_clock_says_what_to_write()
    {
        Assert.Equal(
            "'Environment.TickCount' gives a different answer on each machine; use it only inside Local.For or a [MachineLocal] method; it is this machine's clock: time game logic with After or Every on a scope",
            MapCode.Message("var tick = Environment.TickCount;"));
    }

    [Fact]
    public void sync_unchecked_leaves_a_DateTime_rejected()
    {
        Assert.Equal(new[] { "MW0030: DateTime" }, MapCode.Check("Clock();", """
                [SyncUnchecked]
                private static void Clock()
                {
                    var year = DateTime.Now.Year;
                }
            """));
    }

    [Fact]
    public void an_address_is_rejected_outside_and_read_inside()
    {
        var found = MapCode.Check(
            """
            var a = u.GetHashCode();
            var b = new Fx().GetHashCode();
            var c = new Keyed().GetHashCode() + "x".GetHashCode() + 5.GetHashCode() + new Pt().GetHashCode();
            var d = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(u);
            var e = u.ToString();
            var f = $"{u}";
            var g = "unit " + u;
            var h = new Fx().ToString() + p.ToString();
            Local.For(p, () =>
            {
                var shown = u.GetHashCode() + u.ToString() + $"{u}" + "unit " + u;
            });
            """,
            """
                public sealed class Fx { }
                public sealed class Keyed { public override int GetHashCode() => 1; }
                public struct Pt { public int X; }
            """);

        Assert.Equal(new[]
        {
            "MW0002: u.GetHashCode()",
            "MW0002: new Fx().GetHashCode()",
            "MW0002: System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(u)",
            "MW0002: u.ToString()",
            "MW0002: u",
            "MW0002: u",
        }, found);
    }

    [Fact]
    public void an_address_says_what_to_write()
    {
        Assert.Equal(new[]
        {
            "'Object.GetHashCode' gives a different answer on each machine; use it only inside Local.For or a [MachineLocal] method; in the game's Lua it is the object's address: key by the object itself, or by a number the map assigns",
            "'u' gives a different answer on each machine; use it only inside Local.For or a [MachineLocal] method; in the game's Lua a handle as text is its address",
        }, MapCode.MessagesOfFile("""
            using Moonwell;

            namespace Map
            {
                public static class Use
                {
                    public static string Run(unit u) => u.GetHashCode() + " " + u;
                }
            }
            """));
    }

    [Fact]
    public void sync_unchecked_silences_them()
    {
        Assert.Empty(MapCode.Check("Clock(u);", """
                [SyncUnchecked]
                private static void Clock(unit u)
                {
                    var r = new Random().Next();
                    var now = Environment.TickCount + u.GetHashCode();
                }
            """));
    }

    [Fact]
    public void a_handle_joined_with_plus_equals_or_filled_into_a_handlers_hole_is_mw0002()
    {
        var found = MapCode.Check("""
            string log = "hit ";
            log += u;
            log += 1;
            var text = new System.Text.StringBuilder();
            text.Append($"{u} and {1}");
            Local.For(p, () => { var mine = "a"; mine += u; new System.Text.StringBuilder().Append($"{u}"); });
            """);

        Assert.Equal(new[] { "MW0002: u", "MW0002: u" }, found);
    }

    [Fact]
    public void a_class_of_random_that_gives_no_seed_is_mw0038()
    {
        var found = MapCode.CheckFile("""
            namespace Map
            {
                public sealed class Dice : System.Random
                {
                }

                public sealed class Dice2 : System.Random
                {
                    public Dice2() { }

                    public Dice2(int seed) : base(seed) { }

                    public Dice2(string name) : this(name.Length) { }
                }

                public sealed class Dice3 : System.Random
                {
                    public Dice3() : base() { }
                }

                public sealed class Seeded(int seed) : System.Random(seed)
                {
                }
            }
            """);

        Assert.Equal(new[] { "MW0038: Dice", "MW0038: Dice2", "MW0038: Dice3" }, found);
    }

    [Fact]
    public void real_times_own_members_run_inside_local_for_and_its_local_now_stays_one_machines()
    {
        // RealTime is [LocalSafe] as a whole: its arithmetic, text and comparisons change nothing (lead 9 of the fourth review).
        var found = MapCode.Check("""
            Local.For(p, () =>
            {
                var t = RealTime.LocalNow.AddDays(1).AddHours(1).AddMinutes(1).AddSeconds(1);
                p.Print(t.ToString());
                var later = t > RealTime.LocalNow && t != RealTime.LocalNow && t.Equals(RealTime.LocalNow) && t.CompareTo(RealTime.LocalNow) > 0;
                var hash = t.GetHashCode();
            });
            var outside = RealTime.LocalNow;
            """);

        Assert.Equal(new[] { "MW0002: RealTime.LocalNow" }, found);
    }
}
