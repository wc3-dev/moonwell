namespace Moonwell.Analyzers.Tests;

/// <summary>
/// MW0039 for a member of a library inside Local.For: an assembly that is not the map's, Moonwell's or .NET's own, whose
/// code the analyzer cannot see, unless the library marks the member [LocalSafe] or [MachineLocal] (decision 267).
/// </summary>
public class LibraryCallTests
{
    private const string Library = """
        using Moonwell;

        namespace MyLib
        {
            public static class Rules
            {
                public static int Rounds;

                public static void KillAll() { }

                public static int Round { get; set; }

                [LocalSafe]
                public static int Count() => 0;

                [LocalSafe]
                public static int Limit { get; set; }

                [MachineLocal]
                public static void Flash() { }
            }

            [LocalSafe]
            public sealed class Tally
            {
                public int Total { get; set; }

                public void Add(int amount) { }
            }

            public class Panel
            {
                public virtual void Draw() { }

                [LocalSafe]
                public virtual void Hover() { }
            }

            public struct Point
            {
                public Point(float x) { X = x; }

                public float X { get; }

                public static Point operator +(Point a, Point b) => a;
            }

            public delegate void Handler();
        }
        """;

    [Fact]
    public void a_library_member_inside_local_for_is_mw0039()
    {
        var found = MapCode.Check("""
            var point = new MyLib.Point(1f);
            Local.For(p, () =>
            {
                MyLib.Rules.KillAll();
                var round = MyLib.Rules.Round;
                MyLib.Rules.Round = 2;
                var panel = new MyLib.Panel();
                var sum = point + point;
                var x = point.X;
            });
            """, library: Library);

        Assert.Equal(new[]
        {
            "MW0039: MyLib.Rules.KillAll()",
            "MW0039: MyLib.Rules.Round",
            "MW0039: MyLib.Rules.Round",
            "MW0039: new MyLib.Panel()",
            "MW0039: point + point",
            "MW0039: point.X",
        }, found);
    }

    [Fact]
    public void one_reached_through_a_map_method_is_too()
    {
        var found = MapCode.Check("Local.For(p, Helper); Local.For(p, () => Helper());", "private static void Helper() => MyLib.Rules.KillAll();", Library);

        Assert.Equal(new[] { "MW0039: Helper", "MW0039: Helper()" }, found);
    }

    [Fact]
    public void it_says_what_the_library_can_do()
    {
        Assert.Equal(
            "'Rules.KillAll' runs a library's code, which the analyzer cannot follow; inside Local.For, call only what a library marks [LocalSafe] or [MachineLocal]",
            MapCode.Message("Local.For(p, () => MyLib.Rules.KillAll());", library: Library));
        Assert.Equal(
            "'Code.Helper' runs a library's code, which the analyzer cannot follow (through Code.Helper > Rules.KillAll); inside Local.For, call only what a library marks [LocalSafe] or [MachineLocal]",
            MapCode.Message("Local.For(p, Helper);", "private static void Helper() => MyLib.Rules.KillAll();", Library));
    }

    [Fact]
    public void one_its_library_marks_local_safe_or_machine_local_is_allowed()
    {
        Assert.Empty(MapCode.Check("""
            Local.For(p, () =>
            {
                var count = MyLib.Rules.Count() + MyLib.Rules.Limit;
                MyLib.Rules.Flash();
                var tally = new MyLib.Tally();
                tally.Add(1);
                tally.Total = tally.Total + 1;
            });
            """, library: Library));
    }

    [Fact]
    public void local_safe_on_a_property_covers_its_getter_only()
    {
        Assert.Equal(new[] { "MW0039: MyLib.Rules.Limit" }, MapCode.Check("Local.For(p, () => MyLib.Rules.Limit = 3);", library: Library));
    }

    [Fact]
    public void a_machine_local_library_method_outside_local_for_is_mw0002()
    {
        Assert.Equal(new[] { "MW0002: MyLib.Rules.Flash()" }, MapCode.Check("MyLib.Rules.Flash();", library: Library));
    }

    [Fact]
    public void outside_local_for_a_library_is_called_freely()
    {
        Assert.Empty(MapCode.Check("MyLib.Rules.KillAll(); MyLib.Rules.Round = 2; var panel = new MyLib.Panel();", library: Library));
    }

    [Fact]
    public void a_block_marked_sync_unchecked_calls_one_that_cannot_be_marked()
    {
        Assert.Empty(MapCode.Check("Local.For(p, [SyncUnchecked] () => MyLib.Rules.KillAll());", library: Library));
    }

    [Fact]
    public void the_maps_override_of_a_library_method_is_still_followed()
    {
        const string Bad = "public sealed class Bad : MyLib.Panel { public override void Hover() { Natives.KillUnit(Natives.GetTriggerUnit()); } }";
        const string Quiet = "public sealed class Quiet : MyLib.Panel { public override void Draw() { } public override void Hover() { } }";

        // A marked member's override in the map is followed; an unmarked one is refused whatever the map's override does.
        Assert.Equal(new[] { "MW0001: bad.Hover()" }, MapCode.Check("MyLib.Panel bad = new Bad(); Local.For(p, () => bad.Hover());", Bad, Library));
        Assert.Empty(MapCode.Check("MyLib.Panel quiet = new Quiet(); Local.For(p, () => quiet.Hover());", Quiet, Library));
        Assert.Equal(new[] { "MW0039: quiet.Draw()" }, MapCode.Check("MyLib.Panel quiet = new Quiet(); Local.For(p, () => quiet.Draw());", Quiet, Library));
    }

    [Fact]
    public void a_librarys_field_and_its_delegate_type_run_none_of_its_code()
    {
        Assert.Empty(MapCode.Check("""
            MyLib.Handler handler = () => { };
            Local.For(p, () =>
            {
                var rounds = MyLib.Rules.Rounds;
                handler();
            });
            """, library: Library));
    }

    [Fact]
    public void dotnets_own_members_are_as_before()
    {
        // Decision 101's list still names the calls that change a shared collection; nothing else of .NET's is refused.
        var found = MapCode.Check("""
            Local.For(p, () =>
            {
                var made = new List<int> { 3, 1 };
                made.Add(2);
                made.Sort();
                var text = string.Join(",", made.Select(n => n + 1)) + Math.Max(1, 2);
                var builder = new System.Text.StringBuilder();
                builder.Append(text);
                Scores.Add(made.Count);
            });
            """, "public static List<int> Scores = new List<int>();", Library);

        Assert.Equal(new[] { "MW0048: Scores" }, found);
    }

    [Fact]
    public void moonwells_own_members_are_as_before()
    {
        // A member Moonwell does not mark is still MW0001, not MW0039, and one it marks is allowed.
        var found = MapCode.Check("""
            var unit = Unit.Of(u)!;
            Local.For(p, () =>
            {
                var name = unit.Name;
                unit.Name = "Guard";
                var parsed = Moonwell.Json.JsonNode.Parse("{}");
            });
            """, library: Library);

        Assert.Equal(new[] { "MW0001: unit.Name" }, found);
    }
}
