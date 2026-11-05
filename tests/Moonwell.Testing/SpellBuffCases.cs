using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>A unit component that logs the level of a game buff on its unit when the unit is hit and when it dies.</summary>
    public sealed class BuffReader : UnitComponent
    {
        private readonly FakeWorld world;
        private readonly int buffId;

        /// <summary>Reads a game buff in a fake world.</summary>
        public BuffReader(FakeWorld world, int buffId)
        {
            this.world = world;
            this.buffId = buffId;
        }

        /// <summary>Logs "hit, buff" and the level.</summary>
        public override void OnDamaged(Damage damage)
        {
            TestLog.Lines.Add("hit, buff " + world.AbilityLevel(Unit.Handle, buffId));
        }

        /// <summary>Logs "death, buff" and the level.</summary>
        public override void OnDeath(Unit? killerUnit)
        {
            TestLog.Lines.Add("death, buff " + world.AbilityLevel(Unit.Handle, buffId));
        }
    }

    /// <summary>A buff class a spell lists, as a map writes one: it counts its applies and ticks, and logs its removal and the death it sees.</summary>
    public sealed class FrozenBuff : Buff
    {
        /// <summary>How many times OnApply has run.</summary>
        public int Applied;

        /// <summary>How many ticks have run.</summary>
        public int Ticks;

        /// <summary>Whether OnApply deals 5 from the source to the unit, as a buff of damage does; a case sets it.</summary>
        public static bool HitsOnApply;

        /// <summary>What OnRemoved runs after it logs; a case sets it.</summary>
        public static System.Action<FrozenBuff>? WhenRemoved;

        /// <summary>Runs after each OnApply, when set.</summary>
        public static System.Action<FrozenBuff>? WhenApplied;

        /// <summary>Ticks every half second.</summary>
        public FrozenBuff()
        {
            Tick = 0.5f;
        }

        /// <summary>Counts the apply, and deals 5 from the source when a case asks.</summary>
        public override void OnApply()
        {
            Applied++;
            WhenApplied?.Invoke(this);
            if (HitsOnApply && Source != null)
            {
                Source.Damage(Unit, 5.0f);
            }
        }

        /// <summary>Counts the tick.</summary>
        public override void OnTick()
        {
            Ticks++;
        }

        /// <summary>Logs "frozen saw death".</summary>
        public override void OnDeath(Unit? killerUnit)
        {
            TestLog.Lines.Add("frozen saw death");
        }

        /// <summary>Logs "frozen removed".</summary>
        protected override void OnRemoved()
        {
            TestLog.Lines.Add("frozen removed");
            WhenRemoved?.Invoke(this);
        }
    }

    /// <summary>A unit component that logs, at each hit on its unit, whether the unit has a FrozenBuff.</summary>
    public sealed class FrozenReader : UnitComponent
    {
        /// <summary>Logs "hit, frozen" and True or False.</summary>
        public override void OnDamaged(Damage damage)
        {
            TestLog.Lines.Add("hit, frozen " + (Unit.Has<FrozenBuff>() ? "on" : "off"));
        }
    }

    /// <summary>Cases for the buffs the game puts on: the fake world's game buffs (as seen 11-03), then the watch that runs a buff class's code for them (decisions 152, 153, 163).</summary>
    public static class SpellBuffCases
    {
        internal static readonly int Frozen = ObjectTypes.FourCC("B010");
        private static readonly int Bolt = ObjectTypes.FourCC("A010");
        private static readonly int Frost = ObjectTypes.FourCC("A020");

        private static Unit NewUnit(FakeWorld world)
        {
            return Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;
        }

        /// <summary>Registers FrozenBuff as the build does for a class a spell (A010, a Storm Bolt) and an aura (A020) list, and begins the watch as the runtime does.</summary>
        internal static void Watch()
        {
            FrozenBuff.HitsOnApply = false;
            FrozenBuff.WhenRemoved = null;
            ObjectTypes.BuffDisplay<FrozenBuff>(Frozen, ObjectTypes.FourCC("A011"));
            ObjectTypes.WatchedBuff<FrozenBuff>(Frozen, () => new FrozenBuff());
            ObjectTypes.ListsBuff(Bolt, Frozen, false);
            ObjectTypes.ListsBuff(Frost, Frozen, true);

            // A case's world has not begun as the game's has, so the watch begins here.
            SpellBuffs.Begin();
        }

        /// <summary>A caster with the spell at a level.</summary>
        private static Unit Caster(FakeWorld world, int level)
        {
            var caster = NewUnit(world);
            world.SetAbilityLevel(caster.Handle, Bolt, level);
            return caster;
        }

        private static int CountOf(string line)
        {
            var count = 0;
            for (var i = 0; i < TestLog.Lines.Count; i++)
            {
                if (TestLog.Lines[i] == line)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>A spell's hit puts its buff on: in the same instant the class's object is on the unit, has run OnApply once, and has the hit's source and the spell's level.</summary>
        public static void AfterHit(FakeWorld world)
        {
            Watch();
            var target = NewUnit(world);
            var caster = Caster(world, 2);
            world.GameBuffs.Hit(caster.Handle, target.Handle, Frozen);
            world.Advance(0.01f);
            var buff = target.Get<FrozenBuff>();
            Expect.True(buff != null, "the buff's object is on");
            Expect.Equal(1, buff!.Applied, "OnApply ran once");
            Expect.Same(caster, buff.Source, "the source is the hit's");
            Expect.Equal(2, buff.Level, "the spell's level");
        }

        /// <summary>A hit just after the buff went on, in the same frame, sees it (the attack after Slow Poison's hit of 0).</summary>
        public static void HitSeesIt(FakeWorld world)
        {
            Watch();
            var target = NewUnit(world);
            target.Add(new FrozenReader());
            var caster = Caster(world, 1);
            TestLog.Lines.Clear();
            world.GameBuffs.Hit(caster.Handle, target.Handle, Frozen);
            world.DealDamage(caster.Handle, target.Handle, 10.0f, true, false, 0, 0, 0);
            Expect.Equal("hit, frozen off", TestLog.Lines[0], "the hit of 0 comes before the buff");
            Expect.Equal("hit, frozen on", TestLog.Lines[1], "the attack's hit sees it");
        }

        /// <summary>A spell with no hit puts its buff on in the effect's frame: it is matched to that cast, with its caster and level.</summary>
        public static void NoHitCast(FakeWorld world)
        {
            Watch();
            var target = NewUnit(world);
            var caster = Caster(world, 3);
            Inbound.SpellEffect(caster.Handle, Bolt, target.Handle, 0.0f, 0.0f);
            world.GameBuffs.Give(target.Handle, Frozen);
            world.Advance(0.01f);
            var buff = target.Get<FrozenBuff>();
            Expect.True(buff != null, "the buff's object is on");
            Expect.Same(caster, buff!.Source, "the source is the cast's caster");
            Expect.Equal(3, buff.Level, "the cast's level");
        }

        /// <summary>A spell with no target checks the units within its area around the caster; one outside is found by the sweep within a second, with no source.</summary>
        public static void Area(FakeWorld world)
        {
            Watch();
            world.AbilityAreas[Bolt] = 300.0f;
            var caster = Caster(world, 1);
            var near = NewUnit(world);
            var far = NewUnit(world);
            world.PlaceUnit(caster.Handle, 0.0f, 0.0f);
            world.PlaceUnit(near.Handle, 100.0f, 0.0f);
            world.PlaceUnit(far.Handle, 1000.0f, 0.0f);
            Inbound.SpellEffect(caster.Handle, Bolt, null, 0.0f, 0.0f);
            world.GameBuffs.Give(near.Handle, Frozen);
            world.GameBuffs.Give(far.Handle, Frozen);
            world.Advance(0.01f);
            Expect.Same(caster, near.Get<FrozenBuff>()?.Source, "the near unit's buff is the caster's");
            Expect.True(!far.Has<FrozenBuff>(), "the far one is not checked by the cast");
            world.Advance(1.1f);
            Expect.True(far.Has<FrozenBuff>(), "the sweep found it");
            Expect.True(far.Get<FrozenBuff>()!.Source == null, "with no source");
        }

        /// <summary>A game aura's buff is found within 0.1 s of a unit coming into reach, from the strongest holder in reach; a stronger holder coming applies it again; it ends within 0.1 s of the game taking it off.</summary>
        public static void Aura(FakeWorld world)
        {
            Watch();
            world.AbilityAreas[Frost] = 300.0f;
            var weak = NewUnit(world);
            var strong = NewUnit(world);
            var other = NewUnit(world);
            world.PlaceUnit(weak.Handle, 0.0f, 0.0f);
            world.PlaceUnit(strong.Handle, 2000.0f, 0.0f);
            world.PlaceUnit(other.Handle, 1000.0f, 0.0f);
            world.SetAbilityLevel(weak.Handle, Frost, 1);
            world.SetAbilityLevel(strong.Handle, Frost, 3);
            world.GameBuffs.Aura(weak.Handle, Frost, Frozen, 300.0f);
            world.GameBuffs.Aura(strong.Handle, Frost, Frozen, 300.0f);
            world.Advance(1.1f);
            world.PlaceUnit(other.Handle, 100.0f, 0.0f);
            world.Advance(0.11f);
            var buff = other.Get<FrozenBuff>();
            Expect.True(buff != null, "on within 0.1 s of coming into reach");
            Expect.Same(weak, buff!.Source, "from the holder in reach");
            Expect.Equal(1, buff.Level, "at its level");
            world.PlaceUnit(strong.Handle, 0.0f, 100.0f);
            world.Advance(0.11f);
            Expect.Same(strong, buff.Source, "the stronger holder's now");
            Expect.Equal(3, buff.Level, "at its level");
            Expect.Equal(2, buff.Applied, "OnApply ran again");
            world.PlaceUnit(other.Handle, 1000.0f, 0.0f);
            world.Advance(3.15f);
            Expect.True(buff.HasEnded, "it ended within 0.1 s of the game taking it off");
        }

        /// <summary>A second caster's hit while the buff is on applies it again: the new source and level, and OnApply again, once.</summary>
        public static void GivenAgain(FakeWorld world)
        {
            Watch();
            var target = NewUnit(world);
            var first = Caster(world, 1);
            var second = Caster(world, 2);
            world.GameBuffs.Hit(first.Handle, target.Handle, Frozen);
            world.Advance(0.01f);
            var buff = target.Get<FrozenBuff>()!;
            world.GameBuffs.Hit(second.Handle, target.Handle, Frozen);
            world.Advance(0.01f);
            Expect.Same(buff, target.Get<FrozenBuff>(), "the same object");
            Expect.Same(second, buff.Source, "the new source");
            Expect.Equal(2, buff.Level, "the new level");
            Expect.Equal(2, buff.Applied, "OnApply ran twice in all");
        }

        /// <summary>The game's buff running out ends the object within 0.1 s, with no tick after.</summary>
        public static void EndsWithGame(FakeWorld world)
        {
            Watch();
            var target = NewUnit(world);
            world.GameBuffs.Hit(Caster(world, 1).Handle, target.Handle, Frozen, 2.0f);
            world.Advance(0.01f);
            var buff = target.Get<FrozenBuff>()!;
            world.Advance(2.1f);
            Expect.True(buff.HasEnded, "ended with the game's buff");
            var ticks = buff.Ticks;
            world.Advance(1.0f);
            Expect.Equal(ticks, buff.Ticks, "no tick after");
            Expect.True(ticks >= 3 && ticks <= 4, "it ticked while on: " + ticks);
        }

        /// <summary>A unit that came in with no Moonwell code, given the buff with no event, has its object within a second, once.</summary>
        public static void Sweep(FakeWorld world)
        {
            Watch();
            var raw = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            Inbound.Entered(raw);
            world.GameBuffs.Give(raw, Frozen);
            world.Advance(1.1f);
            var unit = Unit.Of(raw)!;
            Expect.Equal(1, unit.GetAll<FrozenBuff>().Count, "one object");
            world.Advance(1.1f);
            Expect.Equal(1, unit.GetAll<FrozenBuff>().Count, "still one");
        }

        /// <summary>Removing the buff in code takes the game's buff off too.</summary>
        public static void RemovedFromCode(FakeWorld world)
        {
            Watch();
            var target = NewUnit(world);
            world.GameBuffs.Hit(Caster(world, 1).Handle, target.Handle, Frozen);
            world.Advance(0.01f);
            Expect.True(target.Remove<FrozenBuff>(), "removed");
            Expect.Equal(0, world.AbilityLevel(target.Handle, Frozen), "the game's buff is off");
            world.Advance(1.1f);
            Expect.True(!target.Has<FrozenBuff>(), "and stays off");
        }

        /// <summary>A unit's death: the buff sees it, then ends after the death's reactions.</summary>
        public static void Death(FakeWorld world)
        {
            Watch();
            var target = NewUnit(world);
            world.GameBuffs.Hit(Caster(world, 1).Handle, target.Handle, Frozen);
            world.Advance(0.01f);
            var buff = target.Get<FrozenBuff>()!;
            TestLog.Lines.Clear();
            target.Kill();
            Expect.True(buff.HasEnded, "ended at the death");
            Expect.Equal("frozen saw death", TestLog.Lines[0], "it saw the death first");
            Expect.Equal(1, CountOf("frozen removed"), "removed once");
        }

        /// <summary>A plain attack from a unit that has the spell gives the buff nothing: only a spell's or an attack's hit of 0 gives it again.</summary>
        public static void PlainAttackGivesNothing(FakeWorld world)
        {
            Watch();
            var target = NewUnit(world);
            var caster = Caster(world, 1);
            var other = Caster(world, 2);
            world.GameBuffs.Hit(caster.Handle, target.Handle, Frozen);
            world.Advance(0.01f);
            var buff = target.Get<FrozenBuff>()!;
            world.DealDamage(caster.Handle, target.Handle, 10.0f, true, false, 0, 0, 0);
            world.DealDamage(other.Handle, target.Handle, 10.0f, true, false, 0, 0, 0);
            world.Advance(0.01f);
            Expect.Equal(1, buff.Applied, "OnApply ran once");
            Expect.Same(caster, buff.Source, "the source stays");
        }

        /// <summary>With the damage package, a hit of 0 from code, or one a reaction set to 0, gives the buff nothing: only a hit of 0 the game starts does.</summary>
        public static void PackageCodeHitOfZeroGivesNothing(FakeWorld world)
        {
            Watch();
            DamageSystem.Install(new FakeDamageWorld(world));
            var target = NewUnit(world);
            var caster = Caster(world, 1);
            world.GameBuffs.Hit(caster.Handle, target.Handle, Frozen);
            world.Advance(0.01f);
            var buff = target.Get<FrozenBuff>()!;
            caster.Damage(target, 0.0f);
            world.Advance(0.01f);
            Expect.Equal(1, buff.Applied, "OnApply ran once");
        }

        /// <summary>A buff whose OnApply deals damage from its source does not give itself again.</summary>
        public static void OnApplyDamageDoesNotLoop(FakeWorld world)
        {
            Watch();
            FrozenBuff.HitsOnApply = true;
            var target = NewUnit(world);
            world.GameBuffs.Hit(Caster(world, 1).Handle, target.Handle, Frozen);
            world.Advance(0.01f);
            FrozenBuff.HitsOnApply = false;
            Expect.Equal(1, target.Get<FrozenBuff>()!.Applied, "OnApply ran once");
        }

        /// <summary>A dead aura holder gives nothing: the buff follows the living one in reach.</summary>
        public static void DeadHolderGivesNothing(FakeWorld world)
        {
            Watch();
            world.AbilityAreas[Frost] = 300.0f;
            var weak = NewUnit(world);
            var strong = NewUnit(world);
            var other = NewUnit(world);
            world.PlaceUnit(weak.Handle, 0.0f, 0.0f);
            world.PlaceUnit(strong.Handle, 0.0f, 100.0f);
            world.PlaceUnit(other.Handle, 100.0f, 0.0f);
            world.SetAbilityLevel(weak.Handle, Frost, 1);
            world.SetAbilityLevel(strong.Handle, Frost, 3);
            world.GameBuffs.Aura(weak.Handle, Frost, Frozen, 300.0f);
            world.GameBuffs.Aura(strong.Handle, Frost, Frozen, 300.0f);
            world.Advance(1.1f);
            var buff = other.Get<FrozenBuff>()!;
            Expect.Same(strong, buff.Source, "the stronger first");
            strong.Kill();
            world.Advance(0.11f);
            Expect.Same(weak, buff.Source, "the living one once the stronger is dead");
            Expect.Equal(1, buff.Level, "at its level");
        }

        /// <summary>A spell with no target and no area (a self-buff) puts its buff on its caster, matched to the cast.</summary>
        public static void SelfBuff(FakeWorld world)
        {
            Watch();
            var caster = Caster(world, 2);
            Inbound.SpellEffect(caster.Handle, Bolt, null, 0.0f, 0.0f);
            world.GameBuffs.Give(caster.Handle, Frozen);
            world.Advance(0.01f);
            var buff = caster.Get<FrozenBuff>();
            Expect.True(buff != null, "on at once");
            Expect.Same(caster, buff!.Source, "the caster's own");
            Expect.Equal(2, buff.Level, "at the cast's level");
        }

        /// <summary>A spell with no point whose effect gives a stray target (Thunder Clap, seen 11-03) still checks its area around the caster.</summary>
        public static void StrayTargetArea(FakeWorld world)
        {
            Watch();
            world.AbilityAreas[Bolt] = 300.0f;
            var caster = Caster(world, 1);
            var stray = NewUnit(world);
            var near = NewUnit(world);
            world.PlaceUnit(caster.Handle, 0.0f, 0.0f);
            world.PlaceUnit(stray.Handle, 3000.0f, 0.0f);
            world.PlaceUnit(near.Handle, 100.0f, 0.0f);
            Inbound.SpellEffect(caster.Handle, Bolt, stray.Handle, 0.0f, 0.0f);
            world.GameBuffs.Give(near.Handle, Frozen);
            world.Advance(0.01f);
            Expect.Same(caster, near.Get<FrozenBuff>()?.Source, "the near unit's buff is the caster's, at once");
        }

        /// <summary>Map code ending another watched buff while the step ends one does not break the step.</summary>
        public static void StepSurvivesEnds(FakeWorld world)
        {
            Watch();
            var caster = Caster(world, 1);
            var first = NewUnit(world);
            var second = NewUnit(world);
            world.GameBuffs.Hit(caster.Handle, first.Handle, Frozen);
            world.GameBuffs.Hit(caster.Handle, second.Handle, Frozen);
            world.Advance(0.01f);
            FrozenBuff.WhenRemoved = buff =>
            {
                if (buff.Unit == second)
                {
                    world.DealDamage(caster.Handle, first.Handle, 1.0f, false, false, 0, 0, 0);
                }
            };
            world.GameBuffs.Take(first.Handle, Frozen);
            world.GameBuffs.Take(second.Handle, Frozen);
            world.Advance(0.11f);
            FrozenBuff.WhenRemoved = null;
            Expect.True(!first.Has<FrozenBuff>() && !second.Has<FrozenBuff>(), "both ended");
        }

        /// <summary>Two auras of one level: the buff keeps its holder while that one is still in reach, as units move.</summary>
        public static void EqualAurasKeepSource(FakeWorld world)
        {
            Watch();
            world.AbilityAreas[Frost] = 300.0f;
            var left = NewUnit(world);
            var right = NewUnit(world);
            var other = NewUnit(world);
            world.PlaceUnit(left.Handle, 0.0f, 0.0f);
            world.PlaceUnit(right.Handle, 200.0f, 0.0f);
            world.PlaceUnit(other.Handle, 50.0f, 0.0f);
            world.SetAbilityLevel(left.Handle, Frost, 1);
            world.SetAbilityLevel(right.Handle, Frost, 1);
            world.GameBuffs.Aura(left.Handle, Frost, Frozen, 300.0f);
            world.GameBuffs.Aura(right.Handle, Frost, Frozen, 300.0f);
            world.Advance(1.1f);
            var buff = other.Get<FrozenBuff>()!;
            var first = buff.Source;
            var applied = buff.Applied;
            world.PlaceUnit(other.Handle, first == left ? 180.0f : 20.0f, 0.0f);
            world.Advance(0.11f);
            Expect.Same(first, buff.Source, "the holder stays");
            Expect.Equal(applied, buff.Applied, "no OnApply for the move");
        }
        /// <summary>A spell whose hit of 0 comes in its effect's instant (Slow, seen 11-03) gives a buff that is on again once, not once for the hit and once for the cast.</summary>
        public static void HitAndCastOnce(FakeWorld world)
        {
            Watch();
            var target = NewUnit(world);
            var first = Caster(world, 1);
            var second = Caster(world, 2);
            world.GameBuffs.Hit(first.Handle, target.Handle, Frozen);
            world.Advance(0.01f);
            var buff = target.Get<FrozenBuff>()!;
            Inbound.SpellEffect(second.Handle, Bolt, target.Handle, 0.0f, 0.0f);
            world.GameBuffs.Hit(second.Handle, target.Handle, Frozen);
            world.Advance(0.01f);
            Expect.Equal(2, buff.Applied, "OnApply ran once for the second cast");
            Expect.Same(second, buff.Source, "the second caster's");
        }

        /// <summary>The sweep finds the holders of an aura before it checks the units, so a buff it makes in reach of one has that holder as its source.</summary>
        public static void SweepFindsHoldersFirst(FakeWorld world)
        {
            Watch();
            world.AbilityAreas[Frost] = 300.0f;
            var other = NewUnit(world);
            var holder = NewUnit(world);
            world.PlaceUnit(other.Handle, 100.0f, 0.0f);
            world.PlaceUnit(holder.Handle, 0.0f, 0.0f);
            world.SetAbilityLevel(holder.Handle, Frost, 2);
            world.GameBuffs.Aura(holder.Handle, Frost, Frozen, 300.0f);
            world.Advance(1.1f);
            var buff = other.Get<FrozenBuff>();
            Expect.True(buff != null, "found by the sweep");
            Expect.Same(holder, buff!.Source, "the holder's");
            Expect.Equal(2, buff.Level, "at its level");
        }
        /// <summary>Apply of a class a spell lists is refused, and reported, in every build (decision 154).</summary>
        public static void ApplyRefused(FakeWorld world)
        {
            Watch();
            var unit = NewUnit(world);
            var before = world.Printed.Count;
            var buff = unit.Apply(new FrozenBuff());
            Expect.True(!unit.Has<FrozenBuff>(), "not applied");
            Expect.True(world.Printed.Count > before && world.Printed[before].Contains("Unit.Apply"), "reported, by its label, which a release build shows too");
            Expect.True(!buff.HasEnded, "the given buff is left as it is");
        }
        /// <summary>A unit that dies and is removed in one frame ends its buff once.</summary>
        public static void UnitRemoved(FakeWorld world)
        {
            Watch();
            var target = NewUnit(world);
            world.GameBuffs.Hit(Caster(world, 1).Handle, target.Handle, Frozen);
            world.Advance(0.01f);
            TestLog.Lines.Clear();
            target.Kill();
            target.Remove();
            world.Advance(1.2f);
            Expect.Equal(1, CountOf("frozen removed"), "removed once");
        }

        /// <summary>A game buff given for 2 s reads level 1, then 0 once it ran out.</summary>
        public static void FakeGameBuffReads(FakeWorld world)
        {
            var unit = NewUnit(world);
            world.GameBuffs.Give(unit.Handle, Frozen, 2.0f);
            Expect.Equal(1, world.AbilityLevel(unit.Handle, Frozen), "on");
            world.Advance(2.1f);
            Expect.Equal(0, world.AbilityLevel(unit.Handle, Frozen), "run out");
        }

        /// <summary>A spell's hit fires its damage event before the buff is on, and the buff is on after the call.</summary>
        public static void FakeSpellHitOrder(FakeWorld world)
        {
            var target = NewUnit(world);
            var caster = NewUnit(world);
            target.Add(new BuffReader(world, Frozen));
            TestLog.Lines.Clear();
            world.GameBuffs.Hit(caster.Handle, target.Handle, Frozen);
            Expect.Equal(1, TestLog.Lines.Count, "one hit");
            Expect.Equal("hit, buff 0", TestLog.Lines[0], "the buff is not on inside the hit");
            Expect.Equal(1, world.AbilityLevel(target.Handle, Frozen), "on after the call");
        }

        /// <summary>A dying unit's game buffs are off before its death event.</summary>
        public static void FakeBuffOffAtDeath(FakeWorld world)
        {
            var unit = NewUnit(world);
            unit.Add(new BuffReader(world, Frozen));
            world.GameBuffs.Give(unit.Handle, Frozen);
            TestLog.Lines.Clear();
            unit.Kill();
            Expect.True(TestLog.Lines.Contains("death, buff 0"), "off inside the death");
        }

        /// <summary>A unit moved into a fake aura's reach has its buff at the next step, and loses it 3 s after it leaves.</summary>
        public static void FakeAura(FakeWorld world)
        {
            var holder = NewUnit(world);
            var other = NewUnit(world);
            world.PlaceUnit(holder.Handle, 0.0f, 0.0f);
            world.PlaceUnit(other.Handle, 1000.0f, 0.0f);
            world.GameBuffs.Aura(holder.Handle, ObjectTypes.FourCC("AHad"), Frozen, 300.0f);
            Expect.Equal(0, world.AbilityLevel(other.Handle, Frozen), "out of reach");
            world.PlaceUnit(other.Handle, 100.0f, 0.0f);
            world.Advance(0.1f);
            Expect.Equal(1, world.AbilityLevel(other.Handle, Frozen), "in reach");
            world.PlaceUnit(other.Handle, 1000.0f, 0.0f);
            world.Advance(0.1f);
            world.Advance(2.8f);
            Expect.Equal(1, world.AbilityLevel(other.Handle, Frozen), "still on 2.9 s after leaving");
            world.Advance(0.3f);
            Expect.Equal(0, world.AbilityLevel(other.Handle, Frozen), "off 3 s after leaving");
        }
    }
}
