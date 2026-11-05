namespace Moonwell.Analyzers.Tests;

/// <summary>
/// Delegates that reach Local.For through a parameter that is stored, returned or a primary constructor's, through a
/// variable the analyzer cannot see every value of, or through a method the block hands them to; and a variable that is
/// its own initializer, which the analyzer must not loop on.
/// </summary>
public class DelegateFlowTests
{
    [Fact]
    public void a_parameter_kept_in_a_field_is_followed_to_its_callers_arguments()
    {
        var found = MapCode.Check(
            """
            var job = new Job(() => Natives.KillUnit(u));
            Local.For(p, job.Run);
            Local.For(p, () => job.Run());
            var quiet = new Quiet(() => p.Print("hi"));
            Local.For(p, quiet.Run);
            """,
            """
                public sealed class Job { public Action Run; public Job(Action run) { Run = run; } }
                public sealed class Quiet { public Action Run; public Quiet(Action run) { Run = run; } }
            """);

        Assert.Equal(new[] { "MW0001: job.Run", "MW0001: job.Run" }, found);
    }

    [Fact]
    public void a_kept_parameter_whose_callers_cannot_be_seen_is_mw0039()
    {
        var found = MapCode.Check(
            """
            var actions = new List<Action>();
            var job = new Job(actions[0]);
            Local.For(p, job.Run);
            Func<Action, Keeper> make = Keeper.Make;
            Local.For(p, Keeper.Last);
            """,
            """
                public sealed class Job { public Action Run; public Job(Action run) { Run = run; } }
                public sealed class Keeper
                {
                    public static Action? Last;
                    public static Keeper Make(Action a) { Last = a; return new Keeper(); }
                }
            """);

        Assert.Equal(new[] { "MW0039: job.Run", "MW0039: Keeper.Last" }, found);
    }

    [Fact]
    public void a_primary_constructor_parameter_given_to_local_for_checks_its_callers_lambdas()
    {
        var found = MapCode.Check(
            """
            new Notifier(() => Natives.KillUnit(u)).Show(p);
            new Notifier(() => p.Print("hi")).Show(p);
            """,
            """
                public sealed class Notifier(Action show) { public void Show(Player p) => Local.For(p, show); }
            """);

        Assert.Equal(new[] { "MW0001: Natives.KillUnit(u)" }, found);
    }

    [Fact]
    public void a_returned_parameter_is_checked_where_it_is_given()
    {
        var found = MapCode.Check(
            """
            Local.For(p, Pick(() => Natives.KillUnit(u)));
            var actions = new List<Action>();
            Local.For(p, Pick(actions[0]));
            Local.For(p, () => Pick(actions[0])());
            Local.For(p, Chosen());
            Local.For(p, Pick(() => p.Print("hi")));
            """,
            """
                private static readonly List<Action> Kept = new List<Action>();
                private static Action Pick(Action a) => a;
                private static Action Chosen() => Pick(Kept[0]);
            """);

        Assert.Equal(new[] { "MW0001: Natives.KillUnit(u)", "MW0039: actions[0]", "MW0039: actions[0]", "MW0039: Chosen()" }, found);
    }

    [Fact]
    public void an_unknown_delegate_handed_to_a_method_inside_the_block_is_mw0039()
    {
        var found = MapCode.Check(
            """
            var numbers = new List<int>();
            var counters = new List<Action<int>>();
            var actions = new List<Action>();
            Local.For(p, () => numbers.ForEach(counters[0]));
            Local.For(p, () => Twice(actions[0]));
            Local.For(p, () => numbers.ForEach(n => p.Print(n.ToString())));
            Local.For(p, () => { var mine = new List<Action>(); mine.Add(actions[0]); });
            numbers.ForEach(counters[0]);
            """,
            """
                private static void Twice(Action a) { a(); a(); }
            """);

        Assert.Equal(new[] { "MW0039: counters[0]", "MW0039: actions[0]" }, found);
    }

    [Fact]
    public void a_variable_bound_without_an_initializer_is_mw0039()
    {
        var found = MapCode.Check(
            """
            var actions = new List<Action>();
            var table = new Dictionary<int, Action>();
            object o = actions[0];
            Local.For(p, () => { foreach (var a in actions) a(); });
            foreach (Action b in actions) Local.For(p, b);
            if (table.TryGetValue(1, out var c)) Local.For(p, c);
            if (o is Action d) Local.For(p, d);
            var (n, e) = (1, actions[0]);
            Local.For(p, e);
            Action f = () => { };
            table.TryGetValue(2, out f);
            Local.For(p, f);
            Action g = () => { };
            (n, g) = (2, actions[0]);
            Local.For(p, g);
            Action h = () => { };
            h = () => p.Print("hi");
            Local.For(p, h);
            """);

        Assert.Equal(new[] { "MW0039: a()", "MW0039: b", "MW0039: c", "MW0039: d", "MW0039: e", "MW0039: f", "MW0039: g" }, found);
    }

    [Fact]
    public void a_variable_that_is_its_own_initializer_ends()
    {
        var found = MapCode.CheckBrokenFile(
            """
            using System.Collections.Generic;
            using Moonwell;

            namespace Map
            {
                public static class Code
                {
                    public static void Run(Player p)
                    {
                        Local.For(p, () => { List<int> l = l; l.Add(1); });
                        Local.For(p, () => { IList<int> m = (List<int>)m; m.Add(1); });
                        Local.For(p, () => Fill());
                    }

                    private static void Fill() { List<int> l = l; l.Add(1); }
                }
            }
            """);

        Assert.Empty(found);
    }

    [Fact]
    public void a_delegate_given_by_deconstruction_or_an_out_argument_is_followed()
    {
        var found = MapCode.Check(
            """
            (Cb, _) = ((Action)(() => Natives.KillUnit(u)), 0);
            Local.For(p, () => Cb());
            Make(out Given);
            Local.For(p, () => Given());
            """,
            """
                private static Action Cb = () => { };
                private static Action Given = () => { };
                private static void Make(out Action a) { a = () => Natives.KillUnit(Natives.GetTriggerUnit()); }
            """);

        Assert.Equal(new[] { "MW0039: Cb()", "MW0039: Given()" }, found);
    }

    [Fact]
    public void a_returned_delegate_the_analyzer_cannot_follow_is_mw0039()
    {
        var found = MapCode.Check(
            """
            Local.For(p, Pick());
            Local.For(p, Kill());
            """,
            """
                private static List<Action> Handlers = new List<Action>();
                private static Action Pick() => Handlers[0];
                private static Action Kill() => () => Natives.KillUnit(Natives.GetTriggerUnit());
            """);

        Assert.Equal(new[] { "MW0039: Pick()", "MW0001: Kill()" }, found);
    }

    [Fact]
    public void a_parameter_a_member_or_a_local_function_captures_is_followed_to_its_callers()
    {
        var found = MapCode.Check(
            """
            var n = new Notifier(() => Natives.KillUnit(u));
            Local.For(p, () => n.Show());
            ShowTo(p, () => Natives.KillUnit(u));
            """,
            """
                public sealed class Notifier(Action show) { public void Show() => show(); }
                private static void ShowTo(Player w, Action a) { void Inner() => a(); Local.For(w, Inner); }
            """);

        Assert.Equal(new[] { "MW0001: n.Show()", "MW0001: Inner" }, found);
    }

    [Fact]
    public void a_property_an_indexer_or_a_local_function_returning_a_delegate_the_analyzer_cannot_follow_is_mw0039()
    {
        var found = MapCode.Check(
            """
            var handlers = new List<Action>();
            Action Pick() => handlers[0];
            Local.For(p, Pick());
            Local.For(p, Current);
            Local.For(p, new Bag()[0]);
            Local.For(p, Made);
            """,
            """
                private static List<Action> Handlers = new List<Action>();
                private static Action Current => Handlers[0];
                private static Action Made => () => { };
                public sealed class Bag { public Action this[int i] => Handlers[i]; }
            """);

        Assert.Equal(new[] { "MW0039: Pick()", "MW0039: Current", "MW0039: new Bag()[0]" }, found);
    }

    [Fact]
    public void a_delegate_written_through_a_ref_is_not_seen()
    {
        var found = MapCode.Check(
            """
            ref Action r = ref Cb;
            r = () => Natives.KillUnit(u);
            Local.For(p, () => Cb());
            Action cb = () => { };
            ref Action s = ref cb;
            s = () => Natives.KillUnit(u);
            Local.For(p, cb);
            """,
            """
                private static Action Cb = () => { };
            """);

        Assert.Equal(new[] { "MW0039: Cb()", "MW0039: cb" }, found);
    }

    [Fact]
    public void a_partial_methods_parameters_are_its_callers()
    {
        var found = MapCode.Check(
            """
            Hud.Run(p, () => Natives.KillUnit(u));
            Hud.Keep(() => Natives.KillUnit(u));
            Local.For(p, () => Hud.Stored());
            """,
            """
            }

            public static partial class Hud
            {
                public static Action Stored = () => { };
                public static partial void Run(Player p, Action a);
                public static partial void Keep(Action a);
            }

            public static partial class Hud
            {
                public static partial void Run(Player p, Action a) { Local.For(p, a); }
                public static partial void Keep(Action a) { Stored = a; }
            """);

        Assert.Equal(new[] { "MW0001: Natives.KillUnit(u)", "MW0001: Hud.Stored" }, found);
    }

    [Fact]
    public void an_interface_or_abstract_delegate_property_has_the_values_of_those_over_it()
    {
        var found = MapCode.Check(
            """
            var h = new Has();
            h.Cb = () => Natives.KillUnit(u);
            IHas i = h;
            Local.For(p, () => i.Cb());
            Base b = new Derived();
            Local.For(p, () => b.Cb());
            """,
            """
                public interface IHas { Action Cb { get; } }
                public sealed class Has : IHas { public Action Cb { get; set; } = () => { }; }
                public abstract class Base { public abstract Action Cb { get; } }
                public sealed class Derived : Base { public override Action Cb { get; } = () => Natives.KillUnit(Natives.GetTriggerUnit()); }
            """);

        Assert.Equal(new[] { "MW0001: i.Cb", "MW0001: b.Cb" }, found);
    }

    [Fact]
    public void a_delegate_kept_through_a_conditional_call_is_followed()
    {
        var found = MapCode.Check(
            """
            Keeper? k = new Keeper();
            k?.Keep(() => Natives.KillUnit(u));
            Local.For(p, () => Keeper.Stored());
            """,
            """
                public sealed class Keeper { public static Action Stored = () => { }; public void Keep(Action a) { Stored = a; } }
            """);

        Assert.Equal(new[] { "MW0001: Keeper.Stored" }, found);
    }

    [Fact]
    public void a_parameter_given_to_local_for_through_a_condition_or_a_coalesce_checks_its_callers_lambdas()
    {
        var found = MapCode.Check(
            """
            Run(p, () => Natives.KillUnit(u), true);
            Run2(p, () => Natives.KillUnit(u));
            Local.For(p, true ? () => Natives.KillUnit(u) : Noop);
            """,
            """
                private static void Noop() { }
                private static void Run(Player who, Action a, bool flag) => Local.For(who, flag ? a : Noop);
                private static void Run2(Player who, Action? a) => Local.For(who, a ?? Noop);
            """);

        Assert.Equal(new[] { "MW0001: Natives.KillUnit(u)", "MW0001: Natives.KillUnit(u)", "MW0001: Natives.KillUnit(u)" }, found);
    }

    [Fact]
    public void a_delegate_read_in_an_initializer_or_given_with_a_coalescing_assignment_is_followed()
    {
        var found = MapCode.Check(
            """
            Local.For(p, () => B());
            Local.For(p, () => C());
            Action? d = null;
            d ??= A;
            Local.For(p, d);
            """,
            """
                private static Action A = () => Natives.KillUnit(Natives.GetTriggerUnit());
                private static Action B = A;
                private static Action C { get; } = A;
            """);

        Assert.Equal(new[] { "MW0001: B", "MW0001: C", "MW0001: d" }, found);
    }

    [Fact]
    public void a_setters_or_adders_value_given_to_local_for_checks_what_is_assigned()
    {
        var found = MapCode.Check(
            """
            var hud = new Hud(p);
            hud.Show = () => Natives.KillUnit(u);
            hud.Shown += () => Natives.KillUnit(u);
            var actions = new List<Action>();
            hud.Show = actions[0];
            hud.Show = () => p.Print("hi");
            """,
            """
                public sealed class Hud(Player P)
                {
                    public Action Show { set => Local.For(P, value); }
                    public event Action Shown { add => Local.For(P, value); remove { } }
                }
            """);

        Assert.Equal(new[] { "MW0001: Natives.KillUnit(u)", "MW0001: Natives.KillUnit(u)", "MW0039: actions[0]" }, found);
    }
}
