namespace Moonwell.Analyzers.Tests;

/// <summary>MW0047: a reaction called by hand, which runs it with no event behind it.</summary>
public class ReactionCallTests
{
    [Fact]
    public void calling_a_reaction_is_an_error()
    {
        Assert.Equal(new[]
        {
            "MW0047: OnDeath(null)",
            "MW0047: this.OnDeath(null)",
            "MW0047: OnLeave()",
            "MW0047: OnKill(unit)",
            "MW0047: guard.OnDeath(null)",
        }, MapCode.CheckFile("""
            using Moonwell;

            namespace MyMap
            {
                public class Guard : Unit
                {
                    protected override void OnCreated()
                    {
                        OnDeath(null);
                        this.OnDeath(null);
                    }

                    public void Greet()
                    {
                        OnLeave();
                    }
                }

                public class Counter : UnitComponent
                {
                    public void Run(Unit unit)
                    {
                        OnKill(unit);
                    }
                }

                public static class Code
                {
                    public static void Run(Guard guard)
                    {
                        guard.OnDeath(null);
                    }
                }
            }
            """));
    }

    [Fact]
    public void a_lifecycle_hook_counts_too()
    {
        Assert.Equal(new[] { "MW0047: OnAdded()", "MW0047: OnEnd()" }, MapCode.CheckFile("""
            using Moonwell;

            namespace MyMap
            {
                public class Counter : UnitComponent
                {
                    public void Run()
                    {
                        OnAdded();
                    }
                }

                public class Rules : Interaction<Unit, Unit>
                {
                    public void Run()
                    {
                        OnEnd();
                    }
                }
            }
            """));
    }

    [Fact]
    public void an_interface_reaction_counts_too()
    {
        Assert.Equal(new[]
        {
            "MW0047: OnHurt(hit)",
            "MW0047: guard.OnHurt(hit)",
            "MW0047: ((IDamageTarget)guard).OnHurt(hit)",
        }, MapCode.CheckFile("""
            using Moonwell;

            namespace MyMap
            {
                public class Guard : Unit, IDamageTarget
                {
                    public void OnHurt(Hit hit)
                    {
                    }

                    public void Run(Hit hit)
                    {
                        OnHurt(hit);
                    }
                }

                public static class Code
                {
                    public static void Run(Guard guard, Hit hit)
                    {
                        guard.OnHurt(hit);
                        ((IDamageTarget)guard).OnHurt(hit);
                    }
                }
            }
            """));
    }

    [Fact]
    public void a_method_group_of_one_is_an_error()
    {
        Assert.Equal(new[] { "MW0047: OnTick" }, MapCode.CheckFile("""
            using Moonwell;

            namespace MyMap
            {
                public class Burn : Buff
                {
                    public override void OnApply()
                    {
                        Every(1f, OnTick);
                    }
                }
            }
            """));
    }

    [Fact]
    public void base_in_the_same_reaction_is_right()
    {
        Assert.Empty(MapCode.CheckFile("""
            using Moonwell;

            namespace MyMap
            {
                public class Guard : Unit
                {
                    protected override void OnCreated()
                    {
                        base.OnCreated();
                    }

                    public override void OnDeath(Unit? killerUnit)
                    {
                        base.OnDeath(killerUnit);
                    }
                }

                public class Burn : Buff
                {
                    public override void OnTick()
                    {
                        After(1f, () => base.OnTick());
                    }
                }

                public class Elite : Guard, IDamageTarget
                {
                    public virtual void OnHurt(Hit hit)
                    {
                    }
                }

                public class Captain : Elite
                {
                    public override void OnHurt(Hit hit)
                    {
                        base.OnHurt(hit);
                    }
                }
            }
            """));
    }

    [Fact]
    public void base_in_another_method_is_an_error()
    {
        Assert.Equal(new[] { "MW0047: base.OnDeath(null)" }, MapCode.CheckFile("""
            using Moonwell;

            namespace MyMap
            {
                public class Guard : Unit
                {
                    public void Run()
                    {
                        base.OnDeath(null);
                    }
                }
            }
            """));
    }

    [Fact]
    public void a_maps_own_methods_and_a_name_are_left_alone()
    {
        Assert.Empty(MapCode.CheckFile("""
            using Moonwell;

            namespace MyMap
            {
                public class Guard : Unit
                {
                    public virtual void OnAlarm()
                    {
                    }

                    public void Shout()
                    {
                        OnAlarm();
                        Report();
                    }

                    public override void OnDeath(Unit? killerUnit)
                    {
                        Report();
                    }

                    private void Report()
                    {
                        Owner.Print(nameof(OnDeath));
                    }
                }

                public class Ring : Area
                {
                    public override bool Accepts(Unit unit)
                    {
                        return base.Accepts(unit);
                    }

                    public bool Takes(Unit unit)
                    {
                        return Accepts(unit);
                    }
                }
            }
            """));
    }

    [Fact]
    public void the_message_says_what_to_do()
    {
        Assert.Contains("a method of your own", MapCode.Message("unit.OnDeath(null);", "private static readonly Unit unit = null!;"));
    }
}
