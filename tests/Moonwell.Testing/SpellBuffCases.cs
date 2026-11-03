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

    /// <summary>Cases for the buffs the game puts on: the fake world's game buffs (as seen 11-03), then the watch that runs a buff class's code for them (decisions 152, 153, 163).</summary>
    public static class SpellBuffCases
    {
        private static readonly int Frozen = ObjectTypes.FourCC("B010");

        private static Unit NewUnit(FakeWorld world)
        {
            return Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;
        }

        /// <summary>A game buff given for 2 s reads level 1, then 0 once it ran out.</summary>
        public static void FakeGameBuffReads(FakeWorld world)
        {
            var unit = NewUnit(world);
            world.GiveGameBuff(unit.Handle, Frozen, 2.0f);
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
            world.SpellHit(caster.Handle, target.Handle, Frozen);
            Expect.Equal(1, TestLog.Lines.Count, "one hit");
            Expect.Equal("hit, buff 0", TestLog.Lines[0], "the buff is not on inside the hit");
            Expect.Equal(1, world.AbilityLevel(target.Handle, Frozen), "on after the call");
        }

        /// <summary>A dying unit's game buffs are off before its death event.</summary>
        public static void FakeBuffOffAtDeath(FakeWorld world)
        {
            var unit = NewUnit(world);
            unit.Add(new BuffReader(world, Frozen));
            world.GiveGameBuff(unit.Handle, Frozen);
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
            world.AddFakeAura(holder.Handle, ObjectTypes.FourCC("AHad"), Frozen, 300.0f);
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
