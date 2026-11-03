using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases for dummy casters (B13, decisions 168 to 172, 185 and 186): a real spell cast through a hidden dummy for a unit or a player.</summary>
    public static class DummyCases
    {
        private static readonly int DummyType = ObjectTypes.FourCC("hMWd");
        private static readonly int Bolt = ObjectTypes.FourCC("A010");
        private static readonly int Storm = ObjectTypes.FourCC("A011");
        private static readonly int Clap = ObjectTypes.FourCC("A012");
        private static readonly int Spark = ObjectTypes.FourCC("A013");
        private static readonly int Big = ObjectTypes.FourCC("A030");
        private static readonly int Sheep = ObjectTypes.FourCC("A040");
        private static readonly int Research = ObjectTypes.FourCC("Rhst");

        /// <summary>The build's registrations reach the runtime: the dummy's type, each spell's order and whether it lands on its caster, and its requirements.</summary>
        public static void Registry(FakeWorld world)
        {
            Expect.Equal(0, ObjectTypes.DummyTypeId, "no dummy type before the registry names one");
            Expect.Equal(null, ObjectTypes.OrderOf(Bolt), "no order before one is registered");
            ObjectTypes.DummyType(ObjectTypes.FourCC("hMWd"));
            ObjectTypes.CastOrder(Bolt, "thunderbolt", false);
            ObjectTypes.CastOrder(Big, "avatar", true);
            ObjectTypes.Requires(Sheep, ObjectTypes.FourCC("Rhst"), 2);
            Expect.Equal(ObjectTypes.FourCC("hMWd"), ObjectTypes.DummyTypeId, "the dummy's type");
            Expect.Equal("thunderbolt", ObjectTypes.OrderOf(Bolt), "Storm Bolt's order");
            Expect.Equal(false, ObjectTypes.SelfOnly(Bolt), "Storm Bolt does not land on its caster");
            Expect.Equal(true, ObjectTypes.SelfOnly(Big), "Avatar lands on its caster");
            var requires = ObjectTypes.RequirementsOf(Sheep);
            Expect.Equal(1, requires.Count, "one requirement");
            Expect.Equal(ObjectTypes.FourCC("Rhst"), requires[0].Tech, "its research");
            Expect.Equal(2, requires[0].Level, "at its level");
            Expect.Equal(0, ObjectTypes.RequirementsOf(Bolt).Count, "none for a spell with none");
        }

        /// <summary>Registers what the build would for a map that casts TestBolt (A010, at a unit), a point spell (A011), a spell with no target (A012) and another at a unit (A013), and defines them in the fake world; each effect logs its name.</summary>
        private static void Spells(FakeWorld world)
        {
            ObjectTypes.DummyType(DummyType);
            ObjectTypes.Id<TestBolt>(Bolt);
            ObjectTypes.CastOrder(Bolt, "thunderbolt", false);
            ObjectTypes.CastOrder(Storm, "blizzard", false);
            ObjectTypes.CastOrder(Clap, "thunderclap", false);
            ObjectTypes.CastOrder(Spark, "firebolt", false);
            world.Dummies.Spell(Bolt, "thunderbolt", SpellKind.Target, (caster, target, x, y) => TestLog.Lines.Add("bolt"));
            world.Dummies.Spell(Storm, "blizzard", SpellKind.Point, (caster, target, x, y) => TestLog.Lines.Add("storm"));
            world.Dummies.Spell(Clap, "thunderclap", SpellKind.None, (caster, target, x, y) => TestLog.Lines.Add("clap"));
            world.Dummies.Spell(Spark, "firebolt", SpellKind.Target, (caster, target, x, y) => TestLog.Lines.Add("spark"));
            TestLog.Lines.Clear();
        }

        /// <summary>A unit of a slot at a place.</summary>
        private static Unit At(FakeWorld world, int slot, float x, float y)
        {
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot))!;
            world.PlaceUnit(unit.Handle, x, y);
            return unit;
        }

        /// <summary>The dummies the runtime has cast with, each once, in the order first used.</summary>
        private static List<unit> DummiesUsed(FakeWorld world)
        {
            var used = new List<unit>();
            foreach (var field in world.Dummies.Fields)
            {
                if (!used.Contains(field.Unit))
                {
                    used.Add(field.Unit);
                }
            }

            return used;
        }

        private static int CountOf(string line)
        {
            var count = 0;
            foreach (var one in TestLog.Lines)
            {
                if (one == line)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>CastOn at a unit: a dummy of the dummy type, the caster's player's, at the caster's place, with the spell at level 1 freed, ordered on the target; the spell takes effect inside the call.</summary>
        public static void CastOn(FakeWorld world)
        {
            Spells(world);
            var caster = At(world, 0, 100.0f, 200.0f);
            var target = At(world, 1, 900.0f, 200.0f);
            Expect.True(caster.CastOn<TestBolt>(target, Game.Instance), "the game took the cast");
            var dummies = DummiesUsed(world);
            Expect.Equal(1, dummies.Count, "one dummy");
            var dummy = dummies[0];
            Expect.Equal(DummyType, world.UnitTypeId(dummy), "of the dummy type");
            Expect.Same(world.PlayerAt(0), world.OwnerOf(dummy), "the caster's player's");
            Expect.Near(100.0f, world.UnitX(dummy), "at the caster's x");
            Expect.Near(200.0f, world.UnitY(dummy), "at the caster's y");
            Expect.Equal(Bolt, world.Dummies.Fields[0].Ability, "the spell freed");
            Expect.Equal(1, world.Dummies.Fields[0].Level, "at level 1");
            Expect.Equal("target thunderbolt", world.Orders[world.Orders.Count - 1], "ordered by its order");
            Expect.Same(target.Handle, world.OrderTargets[world.OrderTargets.Count - 1], "on the target");
            Expect.Equal("bolt", string.Join(",", TestLog.Lines), "the spell took effect inside the call");
        }

        /// <summary>CastAt with a point spell orders the dummy, at the caster, to the place.</summary>
        public static void CastAt(FakeWorld world)
        {
            Spells(world);
            var caster = At(world, 0, 100.0f, 200.0f);
            Expect.True(caster.CastAt(Storm, 700.0f, 300.0f, Game.Instance), "the game took the cast");
            var dummy = DummiesUsed(world)[0];
            Expect.Near(100.0f, world.UnitX(dummy), "the dummy stands at the caster");
            Expect.Equal("point blizzard", world.Orders[world.Orders.Count - 1], "ordered to the place");
            Expect.Near(700.0f, world.OrderPoints[world.OrderPoints.Count - 2], "the place's x");
            Expect.Near(300.0f, world.OrderPoints[world.OrderPoints.Count - 1], "the place's y");
            Expect.Equal("storm", string.Join(",", TestLog.Lines), "the spell took effect");
        }

        /// <summary>CastAt with a spell with no target puts the dummy at the place and orders it as it is.</summary>
        public static void CastAround(FakeWorld world)
        {
            Spells(world);
            var caster = At(world, 0, 100.0f, 200.0f);
            Expect.True(caster.CastAt(Clap, 700.0f, 300.0f, Game.Instance), "the game took the cast");
            var dummy = DummiesUsed(world)[0];
            Expect.Near(700.0f, world.UnitX(dummy), "the dummy stands at the place's x");
            Expect.Near(300.0f, world.UnitY(dummy), "and y");
            Expect.Equal("immediate thunderclap", world.Orders[world.Orders.Count - 1], "ordered as it is");
            Expect.Equal("clap", string.Join(",", TestLog.Lines), "the spell took effect");
        }

        /// <summary>A level given is the dummy's spell's level; one past the spell's last keeps the game's answer.</summary>
        public static void Level(FakeWorld world)
        {
            Spells(world);
            world.SetLevelCount(Bolt, 3);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            caster.CastOn<TestBolt>(target, Game.Instance, level: 2);
            var dummy = DummiesUsed(world)[0];
            Expect.Equal(2, world.Dummies.Fields[0].Level, "freed at level 2");
            Expect.Equal(2, world.AbilityLevel(dummy, Bolt), "the dummy's spell is at level 2");
            caster.CastOn<TestBolt>(target, Game.Instance, level: 5);
            Expect.Equal(3, world.Dummies.Fields[1].Level, "a level past the last: the game's 3");
        }

        /// <summary>A player's cast: the dummy is that player's and stands at the target.</summary>
        public static void ForPlayer(FakeWorld world)
        {
            Spells(world);
            var target = At(world, 1, 600.0f, 400.0f);
            Expect.True(Player.NeutralAggressive.CastOn<TestBolt>(target, Game.Instance), "the game took the cast");
            var dummy = DummiesUsed(world)[0];
            Expect.Same(world.PlayerAt(world.NeutralAggressiveSlot()), world.OwnerOf(dummy), "the player's dummy");
            Expect.Near(600.0f, world.UnitX(dummy), "at the target's x");
            Expect.Near(400.0f, world.UnitY(dummy), "at the target's y");
            Expect.Equal("bolt", string.Join(",", TestLog.Lines), "the spell took effect");
        }

        /// <summary>A cast the game refuses gives false, and its dummy is free again at once.</summary>
        public static void Refused(FakeWorld world)
        {
            Spells(world);
            world.Dummies.Refuse(Bolt);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            Expect.True(!caster.CastOn<TestBolt>(target, Game.Instance), "refused");
            Expect.True(caster.CastOn(Spark, target, Game.Instance), "the next cast is taken");
            Expect.Equal(1, DummiesUsed(world).Count, "by the same dummy");
            Expect.Equal("spark", string.Join(",", TestLog.Lines), "only the second took effect");
        }

        /// <summary>A unit cast for that is gone casts nothing; a dead one casts, as a dead unit's damage deals (seen 10-27).</summary>
        public static void GoneOrDeadCaster(FakeWorld world)
        {
            Spells(world);
            var gone = At(world, 0, 0.0f, 0.0f);
            var dead = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            gone.Remove();
            var orders = world.Orders.Count;
            Expect.True(!gone.CastOn<TestBolt>(target, Game.Instance), "a gone unit's cast is false");
            Expect.Equal(orders, world.Orders.Count, "and orders nothing");
            Expect.Equal(0, DummiesUsed(world).Count, "and takes no dummy");
            dead.Kill();
            Expect.True(dead.CastOn<TestBolt>(target, Game.Instance), "a dead unit casts");
        }

        /// <summary>A scope already ended casts nothing.</summary>
        public static void EndedScope(FakeWorld world)
        {
            Spells(world);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            var scope = new TestScope("cast", new List<string>());
            scope.End();
            var orders = world.Orders.Count;
            Expect.True(!caster.CastOn<TestBolt>(target, scope), "false");
            Expect.Equal(orders, world.Orders.Count, "nothing ordered");
            Expect.Equal(0, DummiesUsed(world).Count, "no dummy taken");
        }

        /// <summary>A target the player does not see is seen for the instant of the order (decision 186): a patch made before the order, which then works, and stopped after; a seen target needs none. The game cannot tell an invisible target from one in fog (seen 11-03), so an invisible one gets a patch too, and the game refuses it.</summary>
        public static void Fog(FakeWorld world)
        {
            Spells(world);
            var caster = At(world, 0, 0.0f, 0.0f);
            var hidden = At(world, 1, 500.0f, 0.0f);
            var seen = At(world, 1, 600.0f, 0.0f);
            var invisible = At(world, 1, 700.0f, 0.0f);
            world.Dummies.Hide(world.PlayerAt(0), hidden.Handle);
            world.Dummies.Invisible(invisible.Handle);
            Expect.True(caster.CastOn<TestBolt>(hidden, Game.Instance), "the hidden target is cast at");
            Expect.Equal("made,stopped", string.Join(",", world.Dummies.Patches), "one patch, made and stopped");
            Expect.True(caster.CastOn<TestBolt>(seen, Game.Instance), "the seen target is cast at");
            Expect.Equal("made,stopped", string.Join(",", world.Dummies.Patches), "no patch for it");
            Expect.True(!caster.CastOn<TestBolt>(invisible, Game.Instance), "the invisible one is refused");
            Expect.Equal("made,stopped,made,stopped", string.Join(",", world.Dummies.Patches), "with a patch, stopped");
        }

        /// <summary>An order the game takes whose spell does not take effect inside it is no cast, as for a dummy standing on a target its player did not see (seen 11-03): false, the dummy stopped, and free for the next cast.</summary>
        public static void HeldOrder(FakeWorld world)
        {
            Spells(world);
            world.Dummies.Hold(Bolt);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            Expect.True(!caster.CastOn<TestBolt>(target, Game.Instance), "no cast");
            Expect.Equal("immediate stop", world.Orders[world.Orders.Count - 1], "the dummy stopped");
            Expect.True(caster.CastOn(Spark, target, Game.Instance), "the next cast is taken");
            Expect.Equal(1, DummiesUsed(world).Count, "by the same dummy");
            Expect.Equal("spark", string.Join(",", TestLog.Lines), "only the second took effect");
        }

        /// <summary>A dummy cast inside a dummy cast's effect, again and again, stops at 16 deep (the design's call 14).</summary>
        public static void Nested(FakeWorld world)
        {
            Spells(world);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            var calls = 0;
            world.Dummies.Spell(Spark, "firebolt", SpellKind.Target, (dummy, aimed, x, y) =>
            {
                TestLog.Lines.Add("spark");
                if (++calls < 20)
                {
                    caster.CastOn(Spark, target, Game.Instance);
                }
            });
            caster.CastOn(Spark, target, Game.Instance);
            Expect.Equal(16, CountOf("spark"), "sixteen casts took effect");
        }

        /// <summary>Fifty units of two players cast in one instant: fifty dummies, each its unit's player's (the plan's review focus 2).</summary>
        public static void FiftyAtOnce(FakeWorld world)
        {
            Spells(world);
            var target = At(world, 2, 500.0f, 0.0f);
            var casters = new List<Unit>();
            for (var i = 0; i < 50; i++)
            {
                var caster = At(world, i % 2, 0.0f, 0.0f);
                casters.Add(caster);
                caster.CastOn<TestBolt>(target, Game.Instance);
            }

            Expect.Equal(50, DummiesUsed(world).Count, "fifty dummies");
            for (var i = 0; i < 50; i++)
            {
                Expect.Same(world.PlayerAt(i % 2), world.OwnerOf(world.Dummies.Fields[i].Unit), "dummy " + i + " is its caster's player's");
            }
        }

        private static readonly int Echo = ObjectTypes.FourCC("A014");
        private static readonly int Frozen = ObjectTypes.FourCC("B010");

        /// <summary>The fake spell at a unit (A010) deals its amount from the dummy to its target, as a spell's hit does.</summary>
        private static void Hits(FakeWorld world, float amount)
        {
            world.Dummies.Spell(Bolt, "thunderbolt", SpellKind.Target, (dummy, target, x, y) => world.DealDamage(dummy, target!, amount, false, false, 0, 0, 0));
        }

        /// <summary>Map code hears nothing of a dummy: not its entry (even before the pool knows it, as the game's entry comes inside CreateUnit), not its orders; Unit.Of gives no object for it.</summary>
        public static void Hidden(FakeWorld world)
        {
            Spells(world);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            Game.Instance.Add(new DummyEars());
            TestLog.Lines.Clear();
            var early = world.NewUnit(DummyType, 0);
            Inbound.Entered(early);
            caster.CastOn<TestBolt>(target, Game.Instance);
            var dummy = DummiesUsed(world)[0];
            Inbound.Entered(dummy);
            Expect.Equal("bolt", string.Join(",", TestLog.Lines), "no OnUnitCreated, no OnUnitOrdered, no OnUnitCast of a dummy");
            Expect.True(Unit.Of(dummy) == null, "no object for a dummy");
            Expect.True(Unit.Of(early) == null, "nor for a unit of the dummy's type");
        }

        /// <summary>A dummy's cast runs no cast reactions (decision 171): not the spell class's OnCast, not the unit cast for's components', not the map's.</summary>
        public static void NoCastReactions(FakeWorld world)
        {
            Spells(world);
            ObjectTypes.Ability(Echo, () => new EchoBolt());
            ObjectTypes.CastOrder(Echo, "echo", false);
            world.Dummies.Spell(Echo, "echo", SpellKind.Target, (dummy, target, x, y) => TestLog.Lines.Add("echo"));
            var caster = At(world, 0, 0.0f, 0.0f);
            caster.Add(new CastEars());
            var target = At(world, 1, 500.0f, 0.0f);
            Game.Instance.Add(new DummyEars());
            TestLog.Lines.Clear();
            Expect.True(caster.CastOn<EchoBolt>(target, Game.Instance), "the game took the cast");
            Expect.Equal("echo", string.Join(",", TestLog.Lines), "only the spell's effect: no cast reaction ran");
        }

        /// <summary>A dummy's hit is the unit cast for's (decision 168): the target sees it as the source, its OnDealtDamage runs, and so do its player's reactions.</summary>
        public static void HitCredit(FakeWorld world)
        {
            Spells(world);
            Hits(world, 50.0f);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            var dealt = caster.Add(new CreditEars());
            var taken = target.Add(new CreditEars());
            Player.Get(0).Add(new PlayerEars("player 0"));
            TestLog.Lines.Clear();
            caster.CastOn<TestBolt>(target, Game.Instance);
            Expect.Same(caster, taken.Source, "the target's OnDamaged sees the unit cast for as the source");
            Expect.Equal(1, dealt.Dealt, "the unit cast for's OnDealtDamage ran");
            Expect.True(TestLog.Lines.Contains("player 0 damaged"), "its player's OnUnitDamaged ran");
        }

        /// <summary>A dummy's kill is the unit cast for's: the dying unit's OnDeath, the unit's OnKill and the map's OnUnitDied name it.</summary>
        public static void KillCredit(FakeWorld world)
        {
            Spells(world);
            Hits(world, 50.0f);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            world.SetLife(target.Handle, 30.0f);
            var killer = caster.Add(new CreditEars());
            var dying = target.Add(new CreditEars());
            var ears = new DummyEars();
            Game.Instance.Add(ears);
            caster.CastOn<TestBolt>(target, Game.Instance);
            Expect.Same(caster, dying.Killer, "OnDeath gets the unit cast for");
            Expect.Same(target, killer.Killed, "its OnKill runs");
            Expect.Same(caster, ears.Killer, "OnUnitDied gets it");
        }

        /// <summary>A player's cast names no unit (decision 172): the hit's source and the killer are null.</summary>
        public static void PlayerCredit(FakeWorld world)
        {
            Spells(world);
            Hits(world, 50.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            world.SetLife(target.Handle, 30.0f);
            var dying = target.Add(new CreditEars());
            var ears = new DummyEars();
            Game.Instance.Add(ears);
            Player.NeutralAggressive.CastOn<TestBolt>(target, Game.Instance);
            Expect.Equal(1, dying.Taken, "the hit came");
            Expect.True(dying.Source == null, "with no source");
            Expect.True(ears.Died && ears.Killer == null, "and no killer");
        }

        /// <summary>Registers FrozenBuff as the build does for a class the dummy's spell (A010) lists, and begins the watch.</summary>
        private static void WatchFrozen()
        {
            FrozenBuff.HitsOnApply = false;
            FrozenBuff.WhenRemoved = null;
            ObjectTypes.BuffDisplay<FrozenBuff>(Frozen, ObjectTypes.FourCC("A0C1"));
            ObjectTypes.WatchedBuff<FrozenBuff>(Frozen, () => new FrozenBuff());
            ObjectTypes.ListsBuff(Bolt, Frozen, false);
            SpellBuffs.Begin();
        }

        /// <summary>A spell buff the dummy's spell puts on is the unit cast for's: after its hit, with the dummy's spell level; with no hit, matched to the dummy's cast; for a player's cast, no one's.</summary>
        public static void SpellBuff(FakeWorld world)
        {
            Spells(world);
            WatchFrozen();
            var caster = At(world, 0, 0.0f, 0.0f);
            var hit = At(world, 1, 500.0f, 0.0f);
            var given = At(world, 1, 600.0f, 0.0f);
            var players = At(world, 1, 700.0f, 0.0f);
            world.Dummies.Spell(Bolt, "thunderbolt", SpellKind.Target, (dummy, target, x, y) => world.GameBuffs.Hit(dummy, target!, Frozen));
            caster.CastOn<TestBolt>(hit, Game.Instance, level: 2);
            world.Advance(0.01f);
            Expect.Same(caster, hit.Get<FrozenBuff>()?.Source, "after the dummy's hit: the unit cast for");
            Expect.Equal(2, hit.Get<FrozenBuff>()?.Level ?? 0, "at the dummy's spell level");
            world.Dummies.Spell(Bolt, "thunderbolt", SpellKind.Target, (dummy, target, x, y) => world.GameBuffs.Give(target!, Frozen));
            caster.CastOn<TestBolt>(given, Game.Instance);
            Player.NeutralAggressive.CastOn<TestBolt>(players, Game.Instance);
            world.Advance(0.01f);
            Expect.Same(caster, given.Get<FrozenBuff>()?.Source, "with no hit, matched to the dummy's cast: the unit cast for");
            Expect.True(players.Get<FrozenBuff>() != null && players.Get<FrozenBuff>()!.Source == null, "a player's cast: no one's");
        }

        /// <summary>The unit cast for removed between a spell's two hits (Storm Bolt's 0, then its damage): both name its object, and its own reactions run only for the first (the plan's review focus 1).</summary>
        public static void CasterGoneBetweenHits(FakeWorld world)
        {
            Spells(world);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            var dealt = caster.Add(new CreditEars());
            var taken = target.Add(new CreditEars());
            var sources = new List<Unit?>();
            taken.Seen = sources;
            world.Dummies.Spell(Bolt, "thunderbolt", SpellKind.Target, (dummy, aimed, x, y) =>
            {
                world.DealDamage(dummy, aimed!, 0.0f, false, false, 0, 0, 0);
                caster.Remove();
                world.DealDamage(dummy, aimed!, 50.0f, false, false, 0, 0, 0);
            });
            caster.CastOn<TestBolt>(target, Game.Instance);
            Expect.Equal(2, sources.Count, "two hits");
            Expect.Same(caster, sources[0], "the first names it");
            Expect.Same(caster, sources[1], "the second names it, gone");
            Expect.Equal(1, dealt.Dealt, "its OnDealtDamage ran only for the first");
        }

        /// <summary>The damage package's hit of a dummy names the unit cast for as its source.</summary>
        public static void PackageHitCredit(FakeWorld world)
        {
            StageCases.Arena(world, out var source, out var target);
            ObjectTypes.DummyType(DummyType);
            ObjectTypes.CastOrder(Bolt, "thunderbolt", false);
            world.Dummies.Spell(Bolt, "thunderbolt", SpellKind.Target, (dummy, aimed, x, y) => StageCases.Fake.Hit(dummy, aimed!, 10.0f));
            Unit? seen = null;
            target.Taken = hit => seen = hit.Source;
            Expect.True(source.CastOn(Bolt, target, Game.Instance), "the game took the cast");
            Expect.Same(source, seen, "the hit's source is the unit cast for");
        }

        /// <summary>With the damage package, a dummy's hit on a unit Moonwell does not hold yet still reaches the unit cast for, as core's path does (the branch review's first finding).</summary>
        public static void PackageUnknownTarget(FakeWorld world)
        {
            StageCases.Arena(world, out var source, out _);
            ObjectTypes.DummyType(DummyType);
            ObjectTypes.CastOrder(Clap, "thunderclap", false);
            var raw = world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 1);
            world.Dummies.Spell(Clap, "thunderclap", SpellKind.None, (dummy, aimed, x, y) => StageCases.Fake.Hit(dummy, raw, 10.0f));
            var dealt = source.Add(new CreditEars());
            Expect.True(source.CastAt(Clap, 0.0f, 0.0f, Game.Instance), "the game took the cast");
            Expect.Equal(1, dealt.Dealt, "the unit cast for's OnDealtDamage ran for its dummy's hit on a unit Moonwell did not hold");
        }

        private static int CountOrders(FakeWorld world, string order)
        {
            var count = 0;
            foreach (var one in world.Orders)
            {
                if (one == order)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>A dummy's spell keeps it busy, with its player, until its missile's flight and a second's margin have passed after the spell ends; then the next cast takes it again.</summary>
        public static void Reuse(FakeWorld world)
        {
            Spells(world);
            world.Dummies.MissileSpeed(Bolt, 1000);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 1000.0f, 0.0f);
            caster.CastOn<TestBolt>(target, Game.Instance);
            world.Advance(1.9f);
            caster.CastOn<TestBolt>(target, Game.Instance);
            world.Advance(0.2f);
            caster.CastOn<TestBolt>(target, Game.Instance);
            var fields = world.Dummies.Fields;
            Expect.True(fields[1].Unit != fields[0].Unit, "1.9 s later the first is still busy: a second dummy");
            Expect.Same(fields[0].Unit, fields[2].Unit, "2.1 s later the first casts again");
        }

        /// <summary>A spell's duration keeps its dummy busy too: the longer of normal and hero, then the flight and the margin.</summary>
        public static void BusyDuration(FakeWorld world)
        {
            Spells(world);
            world.Dummies.Durations(Bolt, 5.0f, 3.0f);
            world.Dummies.MissileSpeed(Bolt, 1000);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 1500.0f, 0.0f);
            caster.CastOn<TestBolt>(target, Game.Instance);
            world.Advance(7.4f);
            caster.CastOn<TestBolt>(target, Game.Instance);
            world.Advance(0.2f);
            caster.CastOn<TestBolt>(target, Game.Instance);
            var fields = world.Dummies.Fields;
            Expect.True(fields[1].Unit != fields[0].Unit, "at 7.4 s, busy for 5 s of duration, 1.5 s of flight and the margin");
            Expect.Same(fields[0].Unit, fields[2].Unit, "at 7.6 s free again");
        }

        /// <summary>Ending a channel's scope stops the dummy (one stop order), its end-cast comes, and the dummy stays busy for the margin after.</summary>
        public static void ChannelStop(FakeWorld world)
        {
            Spells(world);
            world.Dummies.Spell(Storm, "blizzard", SpellKind.Point, (dummy, target, x, y) => TestLog.Lines.Add("storm"), channel: true);
            var caster = At(world, 0, 0.0f, 0.0f);
            var scope = new TestScope("channel", new List<string>());
            caster.CastAt(Storm, 500.0f, 0.0f, scope);
            world.Advance(2.0f);
            Expect.Equal(0, CountOrders(world, "immediate stop"), "the channel goes on while its scope lasts");
            scope.End();
            Expect.Equal(1, CountOrders(world, "immediate stop"), "its scope's end stops it");
            world.Advance(0.9f);
            caster.CastAt(Storm, 500.0f, 0.0f, Game.Instance);
            world.Advance(0.2f);
            caster.CastAt(Storm, 500.0f, 0.0f, Game.Instance);
            var fields = world.Dummies.Fields;
            Expect.True(fields[1].Unit != fields[0].Unit, "busy for the margin after its end");
            Expect.Same(fields[0].Unit, fields[2].Unit, "then free");
        }

        /// <summary>A scope that ends inside the channel's own effect stops it once, after the effect (the plan's review focus 5).</summary>
        public static void ScopeEndsInEffect(FakeWorld world)
        {
            Spells(world);
            var scope = new TestScope("channel", new List<string>());
            world.Dummies.Spell(Storm, "blizzard", SpellKind.Point, (dummy, target, x, y) => scope.End(), channel: true);
            var caster = At(world, 0, 0.0f, 0.0f);
            caster.CastAt(Storm, 500.0f, 0.0f, scope);
            Expect.Equal(1, CountOrders(world, "immediate stop"), "one stop");
            Expect.True(world.Orders.IndexOf("immediate stop") > world.Orders.IndexOf("point blizzard"), "after the spell's order");
        }

        /// <summary>A unit that changes hands moves its busy dummies to its new player (the design's call 11), through Owner and through the game's change-owner event; a free dummy is left alone.</summary>
        public static void OwnerChange(FakeWorld world)
        {
            Spells(world);
            world.Dummies.Spell(Storm, "blizzard", SpellKind.Point, (dummy, target, x, y) => { }, channel: true);
            world.Dummies.Refuse(Spark);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            caster.CastAt(Storm, 500.0f, 0.0f, Game.Instance);
            caster.CastOn(Spark, target, Game.Instance);
            var busy = world.Dummies.Fields[0].Unit;
            var free = world.Dummies.Fields[1].Unit;
            caster.Owner = Player.Get(2);
            Expect.Same(world.PlayerAt(2), world.OwnerOf(busy), "the busy dummy goes with its unit");
            Expect.Same(world.PlayerAt(0), world.OwnerOf(free), "a free one is left alone");
            world.SetOwner(caster.Handle, world.PlayerAt(3));
            Expect.Same(world.PlayerAt(3), world.OwnerOf(busy), "through the game's change-owner event too, as Charm gives it");
            world.Advance(1.1f);
            caster.CastAt(Storm, 500.0f, 0.0f, Game.Instance);
            Expect.Same(busy, world.Dummies.Fields[2].Unit, "given away, its channel ended, as the game stops a unit it gives (seen 11-03), and it is free a second later");
        }

        /// <summary>The pool removes no dummy, busy or free.</summary>
        public static void NeverRemoved(FakeWorld world)
        {
            Spells(world);
            world.Dummies.MissileSpeed(Bolt, 1000);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 1000.0f, 0.0f);
            for (var i = 0; i < 5; i++)
            {
                caster.CastOn<TestBolt>(target, Game.Instance);
                world.Advance(0.5f);
            }

            world.Advance(10.0f);
            foreach (var field in world.Dummies.Fields)
            {
                Expect.True(!world.RemovedUnits.Contains(field.Unit), "a dummy is never removed");
            }
        }

#if DEBUG
        /// <summary>A level past the spell's last is reported in a development build.</summary>
        public static void LevelReported(FakeWorld world)
        {
            Spells(world);
            world.SetLevelCount(Bolt, 3);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            var before = world.Printed.Count;
            caster.CastOn<TestBolt>(target, Game.Instance, level: 5);
            Expect.Equal(before + 1, world.Printed.Count, "one report");
            Expect.True(world.Printed[before].Contains("level 5"), "it names the level asked for: " + world.Printed[before]);
        }

        /// <summary>A cast on a scope that has ended is reported in a development build.</summary>
        public static void EndedScopeReported(FakeWorld world)
        {
            Spells(world);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            var scope = new TestScope("cast", new List<string>());
            scope.End();
            var before = world.Printed.Count;
            caster.CastOn<TestBolt>(target, scope);
            Expect.Equal(before + 1, world.Printed.Count, "one report");
            Expect.True(world.Printed[before].Contains("has ended"), "it says why: " + world.Printed[before]);
        }

        /// <summary>A refused cast whose player lacks the spell's research is reported, naming the spell and the research (decision 185); one whose player has it is not.</summary>
        public static void ResearchReported(FakeWorld world)
        {
            Spells(world);
            ObjectTypes.Requires(Bolt, Research, 2);
            world.Dummies.Refuse(Bolt);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            var before = world.Printed.Count;
            caster.CastOn<TestBolt>(target, Game.Instance);
            Expect.Equal(before + 1, world.Printed.Count, "one report");
            Expect.True(world.Printed[before].Contains("TestBolt") && world.Printed[before].Contains("Rhst"), "it names the spell and the research: " + world.Printed[before]);
            world.Dummies.Tech(world.PlayerAt(0), Research, 2);
            caster.CastOn<TestBolt>(target, Game.Instance);
            Expect.Equal(before + 1, world.Printed.Count, "no report once the player has it");
        }

        /// <summary>A spell whose targets are only its caster is reported, as it lands on the dummy (the design's call 15).</summary>
        public static void SelfOnlyReported(FakeWorld world)
        {
            Spells(world);
            ObjectTypes.CastOrder(Bolt, "thunderbolt", true);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            var before = world.Printed.Count;
            caster.CastOn<TestBolt>(target, Game.Instance);
            Expect.Equal(before + 1, world.Printed.Count, "one report");
            Expect.True(world.Printed[before].Contains("dummy"), "it says the spell lands on the dummy: " + world.Printed[before]);
        }

        /// <summary>A cast by an id with no registered order is reported, saying which abilities have one.</summary>
        public static void NoOrderReported(FakeWorld world)
        {
            Spells(world);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            var before = world.Printed.Count;
            Expect.True(!caster.CastOn(ObjectTypes.FourCC("AHhb"), target, Game.Instance), "false");
            Expect.Equal(before + 1, world.Printed.Count, "one report");
            Expect.True(world.Printed[before].Contains("AHhb") && world.Printed[before].Contains("World Editor"), "it names the id and the abilities that have an order: " + world.Printed[before]);
        }

        /// <summary>Dummy casts nested deeper than 16 are reported once.</summary>
        public static void NestedReported(FakeWorld world)
        {
            Spells(world);
            var caster = At(world, 0, 0.0f, 0.0f);
            var target = At(world, 1, 500.0f, 0.0f);
            var calls = 0;
            world.Dummies.Spell(Spark, "firebolt", SpellKind.Target, (dummy, aimed, x, y) =>
            {
                if (++calls < 20)
                {
                    caster.CastOn(Spark, target, Game.Instance);
                }
            });
            var before = world.Printed.Count;
            caster.CastOn(Spark, target, Game.Instance);
            Expect.Equal(before + 1, world.Printed.Count, "one report");
            Expect.True(world.Printed[before].Contains("16"), "it names the depth: " + world.Printed[before]);
        }
#endif
    }

    /// <summary>A spell class with code in OnCast, which a dummy's cast skips; it logs "echo cast".</summary>
    public sealed class EchoBolt : Ability
    {
        /// <summary>Logs "echo cast".</summary>
        public override void OnCast(Cast cast)
        {
            TestLog.Lines.Add("echo cast");
        }
    }

    /// <summary>A unit component that logs "unit cast" for its unit's casts.</summary>
    public sealed class CastEars : UnitComponent
    {
        /// <summary>Logs "unit cast".</summary>
        public override void OnCast(Cast cast)
        {
            TestLog.Lines.Add("unit cast");
        }
    }

    /// <summary>A game component that logs what map code hears of any unit, and keeps the last death's killer.</summary>
    public sealed class DummyEars : GameComponent
    {
        /// <summary>Whether a death came.</summary>
        public bool Died;

        /// <summary>The last death's killer.</summary>
        public Unit? Killer;

        /// <summary>Logs "created".</summary>
        public override void OnUnitCreated(Unit createdUnit)
        {
            TestLog.Lines.Add("created");
        }

        /// <summary>Logs "ordered".</summary>
        public override void OnUnitOrdered(Order order)
        {
            TestLog.Lines.Add("ordered");
        }

        /// <summary>Logs "cast".</summary>
        public override void OnUnitCast(Cast cast)
        {
            TestLog.Lines.Add("cast");
        }

        /// <summary>Keeps the killer.</summary>
        public override void OnUnitDied(Unit dyingUnit, Unit? killerUnit)
        {
            Died = true;
            Killer = killerUnit;
        }
    }

    /// <summary>A unit component that keeps the credit its unit sees: the source of each hit taken, the hits dealt, its killer and its kill.</summary>
    public sealed class CreditEars : UnitComponent
    {
        /// <summary>How many hits its unit dealt.</summary>
        public int Dealt;

        /// <summary>How many hits its unit took.</summary>
        public int Taken;

        /// <summary>The last hit's source.</summary>
        public Unit? Source;

        /// <summary>Where each hit's source goes too; null for none.</summary>
        public List<Unit?>? Seen;

        /// <summary>Its unit's killer.</summary>
        public Unit? Killer;

        /// <summary>The unit its unit killed.</summary>
        public Unit? Killed;

        /// <summary>Keeps the source.</summary>
        public override void OnDamaged(Damage damage)
        {
            Taken++;
            Source = damage.Source;
            Seen?.Add(damage.Source);
        }

        /// <summary>Counts the hit dealt.</summary>
        public override void OnDealtDamage(Damage damage)
        {
            Dealt++;
        }

        /// <summary>Keeps the killer.</summary>
        public override void OnDeath(Unit? killerUnit)
        {
            Killer = killerUnit;
        }

        /// <summary>Keeps the kill.</summary>
        public override void OnKill(Unit killedUnit)
        {
            Killed = killedUnit;
        }
    }

    /// <summary>A player component that logs "&lt;name&gt; damaged" for each hit its player's reactions hear.</summary>
    public sealed class PlayerEars : PlayerComponent
    {
        private readonly string name;

        /// <summary>Logs under a name.</summary>
        public PlayerEars(string name)
        {
            this.name = name;
        }

        /// <summary>Logs "&lt;name&gt; damaged".</summary>
        public override void OnUnitDamaged(Damage damage)
        {
            TestLog.Lines.Add(name + " damaged");
        }
    }
}
