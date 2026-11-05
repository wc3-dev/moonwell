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
}
