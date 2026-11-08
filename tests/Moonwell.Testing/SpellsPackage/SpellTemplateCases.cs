using Moonwell.Spells;

namespace Moonwell.Testing
{
    /// <summary>Cases that check the spell templates (decision 226) on the fake worlds: what each hits, when, and for how much.</summary>
    public static class SpellTemplateCases
    {
        private static readonly int Id = ObjectTypes.FourCC("A0SP");
        private static readonly int Wolf = ObjectTypes.FourCC("o0SW");

        private sealed class TestBlast : Blast
        {
            public TestBlast(bool around)
            {
                Radius = 300.0f;
                Damage = new PerLevel(50.0f, 100.0f);
                AroundCaster = around;
            }

            public override void OnHit(Cast cast, Unit target) => TestLog.Lines.Add("hit " + (int)target.X);
        }

        private sealed class TestPulse : PulseChannel
        {
            public TestPulse()
            {
                Interval = 1.0f;
                Damage = 10.0f;
            }

            public override void OnHit(Cast cast, Unit target) => TestLog.Lines.Add("pulse");
        }

        private sealed class TestChain : Chain
        {
            public TestChain()
            {
                Damage = 100.0f;
                Jumps = 2;
                Falloff = 0.5f;
                Look = string.Empty;
            }

            public override void OnHit(Cast cast, Unit target) => TestLog.Lines.Add("chain " + (int)target.X);
        }

        private sealed class SpiritWolf : Unit
        {
        }

        private sealed class TestSummon : Summon<SpiritWolf>
        {
            public TestSummon()
            {
                Count = 3;
                Lifetime = 30.0f;
                Limit = 4;
            }
        }

        private sealed class TestBolt : Bolt
        {
            public TestBolt()
            {
                Damage = 75.0f;
            }

            public override void OnHit(Cast cast, Unit target) => TestLog.Lines.Add("bolt " + (int)target.X);
        }

        private sealed class TestDash : Dash
        {
            public TestDash()
            {
                Damage = 40.0f;
                Distance = 500.0f;
            }

            public override void OnHit(Cast cast, Unit target) => TestLog.Lines.Add("dash " + (int)target.Y);
        }

        private sealed class TestWave : Wave
        {
            public TestWave()
            {
                Damage = 30.0f;
                Distance = 700.0f;
            }

            public override void OnHit(Cast cast, Unit target) => TestLog.Lines.Add("wave " + (int)target.X);
        }

        private sealed class TestLeap : Leap
        {
            public TestLeap()
            {
                Damage = 60.0f;
                Radius = 200.0f;
            }

            public override void OnHit(Cast cast, Unit target) => TestLog.Lines.Add("leap " + (int)target.X);
        }

        /// <summary>Installs the interactions, physics and terrain packages on fake worlds, with flat ground.</summary>
        private static void Packages()
        {
            Interactions.Install(new FakeInteractionWorld());
            Physics.Install(new FakePhysicsWorld());
            var flat = GridText.Corner(0.0f, 2);
            Terrain.Install(new FakeTerrainWorld(), 2, 2, 0.0f, 0.0f, GridText.Tile(flat, flat, flat, flat));
            TestLog.Lines.Clear();
        }

        /// <summary>A footman of a slot at a place.</summary>
        private static Unit At(FakeWorld world, int slot, float x, float y)
        {
            var handle = world.NewUnit(ObjectTypes.FourCC("hfoo"), slot);
            world.MoveUnit(handle, x, y);
            return Unit.Of(handle)!;
        }

        /// <summary>Casts the registered template from a caster at level 1 or the given one.</summary>
        private static void Cast(FakeWorld world, Unit caster, Unit? target, float x, float y, int level = 1)
        {
            world.SetAbilityLevel(caster.Handle, Id, level);
            Inbound.SpellEffect(caster.Handle, Id, target?.Handle, x, y);
        }

        /// <summary>Moves the game on in steps of the interactions package.</summary>
        private static void Steps(FakeWorld world, int count)
        {
            for (var i = 0; i < count; i++)
            {
                world.Advance(Interactions.Step);
            }
        }

        /// <summary>A blast hits the enemies within its radius of the place, once, at the cast's level, and not allies or the far.</summary>
        public static void BlastHitsEnemiesNear(FakeWorld world)
        {
            Packages();
            ObjectTypes.Ability(Id, () => new TestBlast(false));
            var caster = At(world, 0, 0.0f, 0.0f);
            At(world, 1, 1100.0f, 0.0f);
            At(world, 1, 1500.0f, 0.0f);
            At(world, 0, 1050.0f, 0.0f);
            Cast(world, caster, null, 1000.0f, 0.0f, level: 2);
            Expect.Equal("hit 1100", string.Join(",", TestLog.Lines), "the units hit");
            Expect.Equal(1, world.Dealt.Count, "damage dealt once");
            Expect.True(world.Dealt[0].StartsWith("100 ", System.StringComparison.Ordinal), "level 2's damage: " + world.Dealt[0]);
        }

        /// <summary>A blast around the caster hits near the caster, wherever the cast's place reads.</summary>
        public static void BlastAroundCaster(FakeWorld world)
        {
            Packages();
            ObjectTypes.Ability(Id, () => new TestBlast(true));
            var caster = At(world, 0, 2000.0f, 0.0f);
            At(world, 1, 2100.0f, 0.0f);
            At(world, 1, 100.0f, 0.0f);
            Cast(world, caster, null, 0.0f, 0.0f);
            Expect.Equal("hit 2100", string.Join(",", TestLog.Lines), "the unit near the caster");
        }

        /// <summary>A pulsing channel pulses at once and every interval, and stops with the channel.</summary>
        public static void PulseUntilChannelEnds(FakeWorld world)
        {
            Packages();
            ObjectTypes.Ability(Id, () => new TestPulse());
            var caster = At(world, 0, 0.0f, 0.0f);
            At(world, 1, 100.0f, 0.0f);
            Cast(world, caster, null, 100.0f, 0.0f);
            Expect.Equal(1, TestLog.Lines.Count, "a pulse at once");
            world.Advance(2.05f);
            Expect.Equal(3, TestLog.Lines.Count, "a pulse each second");
            Inbound.SpellEnded(caster.Handle, Id);
            world.Advance(3.0f);
            Expect.Equal(3, TestLog.Lines.Count, "none after the channel");
        }

        /// <summary>A chain hits its target, then jumps to the nearest it has not hit, each jump after its delay and weaker by its falloff, as many jumps as set.</summary>
        public static void ChainJumps(FakeWorld world)
        {
            Packages();
            ObjectTypes.Ability(Id, () => new TestChain());
            var caster = At(world, 0, 0.0f, 0.0f);
            var first = At(world, 1, 100.0f, 0.0f);
            At(world, 1, 400.0f, 0.0f);
            At(world, 1, 800.0f, 0.0f);
            At(world, 1, 1200.0f, 0.0f);
            Cast(world, caster, first, 100.0f, 0.0f);
            Expect.Equal("chain 100", string.Join(",", TestLog.Lines), "the first hit at once");
            world.Advance(0.25f);
            world.Advance(0.25f);
            Expect.Equal("chain 100,chain 400,chain 800", string.Join(",", TestLog.Lines), "two jumps, the nearest first, and no more");
            Expect.True(world.Dealt[0].StartsWith("100 ", System.StringComparison.Ordinal) && world.Dealt[1].StartsWith("50 ", System.StringComparison.Ordinal) && world.Dealt[2].StartsWith("25 ", System.StringComparison.Ordinal), "each jump half the last");
        }

        /// <summary>A summon template summons its count for its lifetime, kept to its limit.</summary>
        public static void SummonsToLimit(FakeWorld world)
        {
            Packages();
            ObjectTypes.Unit(Wolf, () => new SpiritWolf());
            ObjectTypes.Ability(Id, () => new TestSummon());
            var caster = At(world, 0, 0.0f, 0.0f);
            Cast(world, caster, null, 0.0f, 0.0f);
            Expect.Equal(3, caster.Summons.Count, "three summoned");
            Expect.True(caster.Summons[0] is SpiritWolf, "of the class");
            Expect.Near(30.0f, world.TimedLives[caster.Summons[0].Handle], "their lifetime");
            Cast(world, caster, null, 0.0f, 0.0f);
            Expect.Equal(4, caster.Summons.Count, "kept to four");
        }

        /// <summary>A bolt at a unit flies to it and hits it once with its damage.</summary>
        public static void BoltHitsTarget(FakeWorld world)
        {
            Packages();
            ObjectTypes.Ability(Id, () => new TestBolt());
            var caster = At(world, 0, 100.0f, 500.0f);
            var target = At(world, 1, 400.0f, 500.0f);
            Cast(world, caster, target, 400.0f, 500.0f);
            Steps(world, 64);
            Expect.Equal("bolt 400", string.Join(",", TestLog.Lines), "the target hit once");
            Expect.True(world.Dealt.Count == 1 && world.Dealt[0].StartsWith("75 ", System.StringComparison.Ordinal), "its damage");
        }

        /// <summary>A dash pushes the caster towards the place and hits each unit it passes once.</summary>
        public static void DashHitsAlongTheWay(FakeWorld world)
        {
            Packages();
            ObjectTypes.Ability(Id, () => new TestDash());
            var caster = At(world, 0, 0.0f, 300.0f);
            At(world, 1, 200.0f, 300.0f);
            At(world, 1, 200.0f, 900.0f);
            Cast(world, caster, null, 400.0f, 300.0f);
            Steps(world, 32);
            Expect.Equal("dash 300", string.Join(",", TestLog.Lines), "the unit on the way, once");
            Expect.True(caster.X > 300.0f, "the caster went most of the way: " + caster.X);
        }

        /// <summary>A wave goes its distance and hits each unit it passes once, and none past its end.</summary>
        public static void WaveHitsEachOnce(FakeWorld world)
        {
            Packages();
            ObjectTypes.Ability(Id, () => new TestWave());
            var caster = At(world, 0, 0.0f, 500.0f);
            At(world, 1, 300.0f, 500.0f);
            At(world, 1, 600.0f, 500.0f);
            At(world, 1, 1000.0f, 500.0f);
            Cast(world, caster, null, 200.0f, 500.0f);
            Steps(world, 64);
            Expect.Equal("wave 300,wave 600", string.Join(",", TestLog.Lines), "each unit on the way, once");
        }

        /// <summary>A leap slams where the caster lands, after its flight, hitting what is near the landing.</summary>
        public static void LeapSlamsOnLanding(FakeWorld world)
        {
            Packages();
            ObjectTypes.Ability(Id, () => new TestLeap());
            var caster = At(world, 0, 0.0f, 500.0f);
            At(world, 1, 500.0f, 500.0f);
            At(world, 1, 100.0f, 500.0f);
            Cast(world, caster, null, 500.0f, 500.0f);
            Expect.Equal(0, TestLog.Lines.Count, "nothing hit on the way up");
            Steps(world, 96);
            Expect.Equal("leap 500", string.Join(",", TestLog.Lines), "the unit at the landing");
            Expect.True(caster.X > 400.0f, "the caster landed near the place: " + caster.X);
        }
    }
}
