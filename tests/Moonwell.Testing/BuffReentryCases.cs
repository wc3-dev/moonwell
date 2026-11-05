namespace Moonwell.Testing
{
    /// <summary>A strongest buff that applies itself again, once, from its OnExpire, held by the same holder.</summary>
    public sealed class Renewing : Buff
    {
        /// <summary>The holder the apply from OnExpire is held by.</summary>
        public static Scope? Holder;

        /// <summary>How many more times OnExpire applies the buff again.</summary>
        public static int RenewsLeft;

        /// <summary>Makes a strongest buff of strength 1 for 5 seconds.</summary>
        public Renewing()
        {
            Stacking = Stacking.Strongest;
            Strength = 1.0f;
            Duration = 5.0f;
        }

        /// <summary>Applies the buff again while RenewsLeft is above 0.</summary>
        public override void OnExpire()
        {
            if (RenewsLeft > 0)
            {
                RenewsLeft--;
                Unit.Apply(new Renewing(), Source, heldBy: Holder);
            }
        }
    }

    /// <summary>A mark a death ends, which refreshes when applied again.</summary>
    public sealed class Marked : Buff
    {
        /// <summary>Makes a mark a death ends.</summary>
        public Marked()
        {
            EndsOnDeath = true;
        }
    }

    /// <summary>A game component that marks each unit as it dies.</summary>
    public sealed class Remarker : GameComponent
    {
        /// <summary>Applies a Marked to the dying unit.</summary>
        public override void OnUnitDied(Unit dyingUnit, Unit? killerUnit)
        {
            dyingUnit.Apply(new Marked());
        }
    }

    /// <summary>A debuff, of which a cleanse removes every one.</summary>
    public class Debuff : Buff
    {
        /// <summary>How many times OnApply ran.</summary>
        public int Applied;

        /// <summary>Counts the apply.</summary>
        public override void OnApply()
        {
            Applied++;
        }
    }

    /// <summary>A debuff whose removal cleanses its unit of every debuff, and logs how many times the cleanse removed one.</summary>
    public sealed class Curse : Debuff
    {
        /// <summary>Removes debuffs while there are any, at most ten times.</summary>
        protected override void OnRemoved()
        {
            var removed = 0;
            while (removed < 10 && Unit.Remove<Debuff>())
            {
                removed++;
            }

            TestLog.Lines.Add("cleansed " + removed);
        }
    }

    /// <summary>A plain debuff.</summary>
    public sealed class Hex : Debuff
    {
    }

    /// <summary>A buff that applies a new one of its class as it is removed, once.</summary>
    public sealed class Phoenix : Buff
    {
        /// <summary>Whether the next removal applies a new Phoenix.</summary>
        public static bool Rebirth;

        /// <summary>Applies a new Phoenix to the unit, once.</summary>
        protected override void OnRemoved()
        {
            if (Rebirth)
            {
                Rebirth = false;
                Unit.Apply(new Phoenix());
            }
        }
    }

    /// <summary>Cases for buffs and components applied, removed or found from inside their own reactions.</summary>
    public static class BuffReentryCases
    {
        /// <summary>A strongest buff that applies itself again from OnExpire, held by the same holder, stays on for the new duration.</summary>
        public static void StrongestRenewedInExpire(FakeWorld world)
        {
            var target = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;
            var caster = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;
            Renewing.Holder = caster;
            Renewing.RenewsLeft = 1;
            target.Apply(new Renewing(), caster, heldBy: caster);
            world.Advance(5.1f);
            Expect.True(target.Has<Renewing>(), "on after its first expiry, which applied it again");
            world.Advance(5.0f);
            Expect.True(!target.Has<Renewing>(), "off after the second expiry");
        }

        /// <summary>A buff a death ends, applied again by a reaction to that death, stays: that apply has not seen the death.</summary>
        public static void ReappliedByDeathStays(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;
            unit.Apply(new Marked());
            Game.Instance.Add(new Remarker());
            unit.Kill();
            Expect.True(unit.Has<Marked>(), "the mark the death's reaction applied");
        }

        /// <summary>A debuff whose OnRemoved removes every debuff: the loop ends, as the debuff being removed is not found.</summary>
        public static void CleanseInsideRemoval(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;
            unit.Apply(new Curse());
            unit.Apply(new Hex());
            TestLog.Lines.Clear();
            unit.Remove<Curse>();
            Expect.Equal("cleansed 1", string.Join(", ", TestLog.Lines), "the log");
            Expect.True(!unit.Has<Debuff>(), "no debuff left");
            Expect.Equal(0, unit.ComponentCount, "no component left");
        }

        /// <summary>A buff applied again from inside the OnRemoved of its class's buff is a new buff, which stays on.</summary>
        public static void AppliedInsideRemoval(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;
            var first = unit.Apply(new Phoenix());
            Phoenix.Rebirth = true;
            unit.Remove<Phoenix>();
            var now = unit.Get<Phoenix>();
            Expect.True(now != null && now != first && !now.HasEnded, "a new Phoenix is on");
        }

        /// <summary>An ended buff given to Apply is reported and left as it is, also where a buff of its class is on the unit.</summary>
        public static void EndedBuffApplyRefused(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;
            var on = unit.Apply(new Hex());
            var ended = new Hex();
            ended.End();
            var before = world.Logged.Count;
            unit.Apply(ended);
            Expect.Equal(before + 1, world.Logged.Count, "the apply reported");
            Expect.Equal(1, on.Applied, "the buff on the unit is not applied again");
        }
    }
}
