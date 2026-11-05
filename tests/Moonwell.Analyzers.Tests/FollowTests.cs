namespace Moonwell.Analyzers.Tests;

/// <summary>MW0001 for what Local.For reaches: the map code it follows, whatever the order of the code.</summary>
public class FollowTests
{
    private const string Mutual = """
            private static void A(int n)
            {
                if (n > 0) { B(n - 1); }
                Natives.KillUnit(Natives.GetTriggerUnit());
            }

            private static void B(int n)
            {
                if (n > 0) { A(n - 1); }
            }
        """;

    [Theory]
    [InlineData("Local.For(p, () => A(1));\nLocal.For(p, () => B(1));")]
    [InlineData("Local.For(p, () => B(1));\nLocal.For(p, () => A(1));")]
    public void mutual_recursion_is_reported_whatever_comes_first(string body)
    {
        var found = MapCode.Check(body, Mutual);

        Assert.Equal(2, found.Count);
        Assert.All(found, f => Assert.StartsWith("MW0001: ", f));
    }

    [Fact]
    public void a_method_reached_again_through_a_longer_cycle_is_reported()
    {
        var found = MapCode.Check(
            """
            Local.For(p, () => C(1));
            Local.For(p, () => D(1));
            Local.For(p, () => E(1));
            """,
            """
                private static void C(int n) { if (n > 0) { D(n - 1); } Natives.KillUnit(Natives.GetTriggerUnit()); }
                private static void D(int n) { if (n > 0) { E(n - 1); } }
                private static void E(int n) { if (n > 0) { C(n - 1); } }
            """);

        Assert.Equal(new[] { "MW0001: C(1)", "MW0001: D(1)", "MW0001: E(1)" }, found);
    }

    [Fact]
    public void instance_initializers_are_followed_when_a_class_is_made_inside()
    {
        var found = MapCode.Check(
            """
            Local.For(p, () => { var f = new Fx(); });
            Local.For(p, () => { var g = new Grouped(); });
            Local.For(p, () => { var h = new Plain(); });
            Local.For(p, () => { var d = new Derived(); });
            """,
            """
                public sealed class Fx { public readonly effect E = Natives.AddSpecialEffect("x", 0f, 0f); }
                public sealed class Grouped { private readonly group g = Natives.CreateGroup(); public Grouped() { } }
                public sealed class Plain { public int X = 1; public effect? E { get; set; } }
                public class Base { protected group G { get; } = Natives.CreateGroup(); }
                public sealed class Derived : Base { }
            """);

        Assert.Equal(new[] { "MW0001: new Fx()", "MW0001: new Grouped()", "MW0001: new Derived()" }, found);
    }

    [Fact]
    public void static_initializers_are_followed_when_a_class_is_touched_inside()
    {
        var found = MapCode.Check(
            """
            Local.For(p, () => { var g = Pool.G; });
            Local.For(p, () => Hud.Show("hi"));
            Local.For(p, () => { var n = Counter.N; });
            Local.For(p, () => { var k = Safe.K + Safe.L.Length; });
            Local.For(p, () => { var o = Own; });
            """,
            """
                private static readonly group Own = Natives.CreateGroup();

                public static class Pool { public static readonly group G = Natives.CreateGroup(); }

                public static class Hud
                {
                    private static readonly framehandle Box = Natives.BlzCreateFrameByType("TEXT", "Box", Natives.BlzGetOriginFrame(Natives.ORIGIN_FRAME_GAME_UI, 0), "", 0);

                    public static void Show(string text) => Natives.BlzFrameSetText(Box, text);
                }

                public static class Counter
                {
                    public static int N;

                    static Counter() { Natives.CreateTimer(); }
                }

                public static class Safe { public const int K = 3; public static readonly int[] L = new int[4]; }
            """);

        // The class whose code runs is already there: its own statics are not touched for the first time.
        Assert.Equal(new[] { "MW0001: Pool.G", "MW0001: Hud.Show(\"hi\")", "MW0001: Counter.N" }, found);
    }

    [Fact]
    public void a_static_initializer_names_its_path()
    {
        Assert.Equal(
            "'Pool.G' creates a handle (through Pool.G > static Pool() > CreateGroup); inside Local.For it runs on one machine only, which desyncs the game",
            MapCode.Message(
                "Local.For(p, () => { var g = Pool.G; });",
                "public static class Pool { public static readonly group G = Natives.CreateGroup(); }"));
    }

    [Fact]
    public void a_lambda_given_through_a_delegate_parameter_is_a_local_block()
    {
        var found = MapCode.Check(
            """
            var unit = Unit.Of(u)!;
            ShowTo(p, () => unit.Kill());
            ShowTo(p, () => { var x = Natives.GetCameraTargetPositionX(); p.Print("x " + x); });
            Twice(p, () => Natives.KillUnit(u));
            RunInside(p, () => Natives.RemoveUnit(u));
            ShowTo(p, Kill);
            """,
            """
                public static void ShowTo(Player who, Action action) => Local.For(who, action);
                public static void Twice(Player who, Action action) => ShowTo(who, action);
                public static void RunInside(Player who, Action action) => Local.For(who, () => action());
                private static void Kill() => Natives.KillUnit(Natives.GetTriggerUnit());
            """);

        Assert.Equal(new[] { "MW0001: unit.Kill()", "MW0001: Natives.KillUnit(u)", "MW0001: Natives.RemoveUnit(u)", "MW0001: Kill" }, found);
    }

    [Fact]
    public void a_delegate_parameter_given_to_local_for_and_passed_on_outside_is_mw0040()
    {
        var found = MapCode.Check("Show(p, () => { }); Back(p, () => { });", """
                private static Action? Kept;

                public static void Show(Player who, Action draw)
                {
                    Local.For(who, draw);
                    Game.Instance.After(1f, draw);
                    Kept = draw;
                    var same = draw;
                    if (draw != null) { }
                }

                public static Action Back(Player who, Action draw)
                {
                    Local.For(who, draw);
                    return draw;
                }
            """);

        Assert.Equal(new[] { "MW0040: draw", "MW0040: draw", "MW0040: draw" }, found);
    }

    [Fact]
    public void an_implicit_deconstruct_is_followed()
    {
        var found = MapCode.Check("""
            var slot = new Slot();
            var slots = new List<Slot>();
            Local.For(p, () => { var (owner, uses) = slot; });
            Local.For(p, () => { foreach (var (o, n) in slots) { } });
            Local.For(p, () => { if (slot is (var o, var n)) { } });
            var pair = new Pair();
            Local.For(p, () => { var (a, b) = pair; });
            """, """
                public sealed class Slot
                {
                    public void Deconstruct(out unit owner, out int uses) { owner = Natives.CreateUnit(Natives.Player(0), 0, 0f, 0f, 0f); uses = 1; }
                }

                public sealed class Pair
                {
                    public void Deconstruct(out int a, out int b) { a = 1; b = 2; }
                }
            """);

        Assert.Equal(new[] { "MW0001: var (owner, uses) = slot", "MW0001: slots", "MW0001: (var o, var n)" }, found);
    }

    [Fact]
    public void a_delegate_parameter_given_to_local_for_and_called_outside_is_mw0040()
    {
        var found = MapCode.Check("Both(p, () => { });", """
                public static void Both(Player who, Action action)
                {
                    action();
                    Local.For(who, action);
                }
            """);

        Assert.Equal(new[] { "MW0040: action" }, found);
    }

    [Fact]
    public void a_delegate_variable_field_or_event_is_followed()
    {
        var found = MapCode.Check(
            """
            Action kill = () => Natives.KillUnit(u);
            Local.For(p, kill);
            Local.For(p, OnShow);
            Local.For(p, () => Changed?.Invoke());
            Local.For(p, () => Fire());
            Action quiet = () => p.Print("hi");
            Local.For(p, quiet);
            """,
            """
                public static Action OnShow = () => Natives.KillUnit(Natives.GetTriggerUnit());
                public static event Action? Changed;
                public static Action? Later;

                public static void Wire()
                {
                    Changed += Spawn;
                    Later = () => Natives.CreateGroup();
                }

                private static void Spawn() => Natives.CreateUnit(Natives.Player(0), 0, 0f, 0f, 0f);

                private static void Fire() => Later?.Invoke();
            """);

        Assert.Equal(new[] { "MW0001: kill", "MW0001: OnShow", "MW0001: Changed", "MW0001: Fire()" }, found);
    }

    [Fact]
    public void code_the_analyzer_cannot_follow_inside_local_for_is_mw0039()
    {
        var found = MapCode.Check(
            """
            var actions = new List<Action>();
            Local.For(p, actions[0]);
            Local.For(p, () => actions[0]());
            Action<Action> run = a => Local.For(p, a);
            Local.For(p, Make());
            Local.For(p, () => Make()());
            """,
            """
                private static Action Make() => () => { };
            """);

        Assert.Equal(new[] { "MW0039: actions[0]", "MW0039: actions[0]()", "MW0039: a" }, found);
    }

    [Fact]
    public void code_the_analyzer_cannot_follow_says_what_to_write()
    {
        Assert.Equal(
            "'actions[0]' runs code the analyzer cannot follow; inside Local.For, write the lambda in place or call a method of the map",
            MapCode.Message("var actions = new List<Action>(); Local.For(p, actions[0]);"));
    }

    [Fact]
    public void overrides_and_implementations_are_followed()
    {
        var found = MapCode.Check(
            """
            View v = new Spawner();
            Local.For(p, () => v.Refresh());
            Panel w = new Bad();
            Local.For(p, () => w.Refresh());
            IShow s = new Shower();
            Local.For(p, () => s.Show());
            var quiet = new Quiet();
            Local.For(p, () => quiet.Refresh());
            """,
            """
                public class View { public virtual void Refresh() { } }
                public sealed class Spawner : View { public override void Refresh() { Natives.CreateUnit(Natives.Player(0), 0, 0f, 0f, 0f); } }
                public sealed class Quiet : View { public override void Refresh() { } }
                public abstract class Panel { public abstract void Refresh(); }
                public sealed class Bad : Panel { public override void Refresh() { Natives.KillUnit(Natives.GetTriggerUnit()); } }
                public interface IShow { void Show(); }
                public sealed class Shower : IShow { public void Show() => Natives.RemoveUnit(Natives.GetTriggerUnit()); }
            """);

        Assert.Equal(new[] { "MW0001: v.Refresh()", "MW0001: w.Refresh()", "MW0001: s.Show()" }, found);
    }

    [Fact]
    public void an_override_names_its_path()
    {
        Assert.Equal(
            "'View.Refresh' creates a handle (through View.Refresh > Spawner.Refresh > CreateGroup); inside Local.For it runs on one machine only, which desyncs the game",
            MapCode.Message("View v = new Spawner(); Local.For(p, () => v.Refresh());", """
                public class View { public virtual void Refresh() { } }
                public sealed class Spawner : View { public override void Refresh() { Natives.CreateGroup(); } }
            """));
    }

    [Fact]
    public void operators_dispose_enumerators_and_compound_reads_are_followed()
    {
        var found = MapCode.Check(
            """
            var a = new Pt();
            Local.For(p, () => { var b = a + a; });
            Local.For(p, () => { using (var k = new Killer()) { } });
            Local.For(p, () => { using var k = new Killer(); });
            Local.For(p, () => { foreach (var x in new Spawns()) { } });
            Local.For(p, () => { Score += 1; });
            Local.For(p, () => { int n = a; });
            """,
            """
                public sealed class Pt
                {
                    public static Pt operator +(Pt x, Pt y) { Natives.CreateGroup(); return x; }
                    public static implicit operator int(Pt x) { Natives.KillUnit(Natives.GetTriggerUnit()); return 0; }
                }

                public sealed class Killer : IDisposable { public void Dispose() { Natives.KillUnit(Natives.GetTriggerUnit()); } }

                public sealed class Spawns
                {
                    public Spawner GetEnumerator() => new Spawner();
                }

                public sealed class Spawner
                {
                    public unit Current => Natives.GetTriggerUnit();
                    public bool MoveNext() { Natives.CreateUnit(Natives.Player(0), 0, 0f, 0f, 0f); return false; }
                }

                public static int Score { get { Natives.CreateGroup(); return 0; } set { } }
            """);

        Assert.Equal(new[]
        {
            "MW0001: a + a",
            "MW0001: var k = new Killer()",
            "MW0001: using var k = new Killer();",
            "MW0001: new Spawns()",
            "MW0001: Score",
            "MW0001: a",
        }, found);
    }

    [Fact]
    public void a_delegate_chosen_by_a_condition_or_a_coalesce_is_followed()
    {
        var found = MapCode.Check("""
            OnA = () => Natives.KillUnit(u);
            var flag = u != null;
            Local.For(p, flag ? OnA : OnB);
            Local.For(p, OnA ?? OnB);
            Local.For(p, flag ? OnB : OnB);
            """, """
                private static Action OnA = () => { };
                private static Action OnB = () => { };
            """);

        Assert.Equal(new[] { "MW0001: OnA", "MW0001: OnA" }, found);
    }

    [Fact]
    public void an_interface_method_a_base_class_implements_is_followed()
    {
        var found = MapCode.Check("""
            IRefresh r = new Hud();
            Local.For(p, () => r.Refresh());
            """, """
                public interface IRefresh { void Refresh(); }
                public class Base { public void Refresh() { Natives.KillUnit(Natives.GetTriggerUnit()); } }
                public class Hud : Base, IRefresh { }
            """);

        Assert.Equal(new[] { "MW0001: r.Refresh()" }, found);
    }

    [Fact]
    public void partial_methods_and_properties_are_followed_into_their_bodies()
    {
        var found = MapCode.Check("""
            Local.For(p, () => Hud.Refresh());
            Local.For(p, () => { var n = Hud.Count; });
            """, """
                public static partial class Hud
                {
                    public static partial void Refresh();
                    public static partial void Refresh() { Natives.KillUnit(Natives.GetTriggerUnit()); }
                    public static partial int Count { get; }
                    public static partial int Count { get { Natives.KillUnit(Natives.GetTriggerUnit()); return 1; } }
                }
            """);

        Assert.Equal(new[] { "MW0001: Hud.Refresh()", "MW0001: Hud.Count" }, found);
    }

    [Fact]
    public void a_primary_constructors_base_call_is_followed()
    {
        var found = MapCode.Check("""
            Local.For(p, () => new Hud());
            Local.For(p, () => new Wide(1));
            Local.For(p, () => new Bare());
            Local.For(p, () => new Plain());
            """, """
                public class Panel
                {
                    public Panel() { Natives.CreateUnit(Natives.Player(0), 0, 0f, 0f, 0f); }
                    public Panel(int x) : this() { }
                }
                public class Hud() : Panel() { }
                public class Wide(int x) : Panel(x) { }
                public class Bare() : Panel { }
                public class Plain : Panel { }
            """);

        Assert.Equal(new[] { "MW0001: new Hud()", "MW0001: new Wide(1)", "MW0001: new Bare()", "MW0001: new Plain()" }, found);
    }

    [Fact]
    public void an_extension_method_that_runs_its_action_locally_is_seen_in_extension_form()
    {
        var found = MapCode.CheckFile("""
            using System;
            using Moonwell;

            namespace Map
            {
                public static class Ext
                {
                    public static void Mine(this Player who, Action a) => Local.For(who, a);
                }

                public static class Code
                {
                    public static void Run(Player p, unit u)
                    {
                        p.Mine(() => Natives.KillUnit(u));
                        p.Mine(a: () => Natives.KillUnit(u));
                        Ext.Mine(p, () => Natives.KillUnit(u));
                    }
                }
            }
            """);

        Assert.Equal(new[] { "MW0001: Natives.KillUnit(u)", "MW0001: Natives.KillUnit(u)", "MW0001: Natives.KillUnit(u)" }, found);
    }

    [Fact]
    public void a_name_in_nameof_runs_nothing()
    {
        // nameof reads no getter and touches no class, so nothing it names is followed.
        Assert.Empty(MapCode.Check("""
            Local.For(p, () => p.Print(nameof(Holder.Value) + nameof(Statics.X) + nameof(Natives.KillUnit) + nameof(Hurt)));
            Local.For(p, () => Name());
            """, """
            static void Hurt() { Natives.KillUnit(Natives.GetTriggerUnit()); }
            static string Name() => nameof(Holder.Value);
            public sealed class Holder { public static int Value { get { Natives.KillUnit(Natives.GetTriggerUnit()); return 1; } } }
            public static class Statics { public static group X = Natives.CreateGroup(); }
            """));
    }
}
