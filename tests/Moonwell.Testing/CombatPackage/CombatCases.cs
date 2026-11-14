using System;
using System.Collections.Generic;
using Moonwell.AI;
using Moonwell.AI.Combat;

namespace Moonwell.Testing
{
    /// <summary>Cases for the combat package (K9, decisions 348 to 356): meeting enemies, target choice, casting, pulling back, the leash and the lane walker's settings.</summary>
    public static class CombatCases
    {
        /// <summary>The type id of the cases' units.</summary>
        private static readonly int Footman = ObjectTypes.FourCC("hfoo");

        /// <summary>Storm Bolt, cast in the cases with the fake's "holybolt", a spell on a unit.</summary>
        private static readonly int BoltId = ObjectTypes.FourCC("AHtb");

        /// <summary>Thunder Clap, cast in the cases with the fake's "immolation", a spell with no target.</summary>
        private static readonly int ClapId = ObjectTypes.FourCC("AHtc");

        /// <summary>Holy Light, cast in the cases with the fake's "holybolt".</summary>
        private static readonly int HealId = ObjectTypes.FourCC("AHhb");

        /// <summary>Blizzard, a spell at a place, which the fake does not know, so the unit reads idle at once after it.</summary>
        private static readonly int BlizzardId = ObjectTypes.FourCC("AHbz");

        /// <summary>The game's id of "holybolt" in the fake.</summary>
        private const int HolyBoltId = 852092;

        /// <summary>A unit of a slot at a place, with 100 life of 100.</summary>
        private static Unit At(FakeWorld world, int slot, float x, float y)
        {
            var handle = world.NewUnit(Footman, slot: slot);
            world.PlaceUnit(handle, x, y);
            var unit = Unit.Of(handle)!;
            unit.MaxLife = 100f;
            unit.Life = 100f;
            return unit;
        }

        /// <summary>A combat brain on a unit of player 0 at a place, its fighter meeting enemies within the range; the orders so far forgotten.</summary>
        private static CombatBrain Brain(FakeWorld world, float x, float y, float meetRange)
        {
            var brain = At(world, 0, x, y).Add(new CombatBrain());
            brain.Fighter.MeetRange = meetRange;
            world.Orders.Clear();
            world.OrderPoints.Clear();
            world.OrderTargets.Clear();
            return brain;
        }

        /// <summary>Moves time on a step, so the fighter checks anew, checks, and gives its target.</summary>
        private static Unit? Check(FakeWorld world, Fighter fighter)
        {
            BrainCases.Steps(world, 1);
            fighter.Meets();
            return fighter.Target;
        }

        /// <summary>A route through points given as x, y pairs.</summary>
        private static Route Through(params float[] xy)
        {
            var points = new List<Vector2>();
            for (var i = 0; i < xy.Length; i += 2)
            {
                points.Add(new Vector2(xy[i], xy[i + 1]));
            }

            return new Route(points, true);
        }

        /// <summary>The orders given through the world so far, in one line.</summary>
        private static string Given(FakeWorld world)
        {
            return string.Join(", ", world.Orders.ToArray());
        }

        /// <summary>Checks a status.</summary>
        private static void Is(Status expected, Status actual, string what)
        {
            Expect.Equal((int)expected, (int)actual, what);
        }

        /// <summary>Nearest picks the nearest enemy met, a distance below 16 counted as 16, and with no rules too; the target stays until another scores more than its score times 1.1; a dead target is dropped; Weakest picks the least life left, as an amount.</summary>
        public static void Targets(FakeWorld world)
        {
            var fighter = Brain(world, 0f, 0f, 1000f).Fighter;
            var near = At(world, 1, 300f, 0f);
            var far = At(world, 1, 600f, 0f);
            Expect.Same(near, Check(world, fighter), "the nearest, with no rules");
            Expect.Equal(2, fighter.EnemiesNear, "both met");
            world.PlaceUnit(far.Handle, 280f, 0f);
            Expect.Same(near, Check(world, fighter), "the other scores 1 against 280 / 300 = 0.93, not more than 0.93 x 1.1 = 1.03: the target stays");
            world.PlaceUnit(far.Handle, 250f, 0f);
            Expect.Same(far, Check(world, fighter), "1 against 250 / 300 = 0.83, more than 0.83 x 1.1 = 0.92: the other takes over");
            far.Kill();
            Expect.Same(near, Check(world, fighter), "a dead target is dropped");
            near.Kill();
            Expect.True(Check(world, fighter) == null, "none left");
            Expect.True(!fighter.Meets(), "it meets none");

            var close = At(world, 1, 10f, 0f);
            At(world, 1, 0f, 5f);
            Expect.Same(close, Check(world, fighter), "10 and 5 both count as 16, so they score alike: the first met");

            var weakest = Brain(world, 10000f, 0f, 1000f).Fighter;
            weakest.Targets.Add(Target.Weakest);
            var big = At(world, 1, 10100f, 0f);
            big.MaxLife = 500f;
            big.Life = 100f;
            var small = At(world, 1, 10500f, 0f);
            small.Life = 80f;
            Expect.Same(small, Check(world, weakest), "80 life of 100 before 100 of 500: the least life left, not the least share");
        }

        /// <summary>A rule scoring 0 rules an enemy out, a weight of 0 too; a map's score is clamped to 0..1; with every enemy ruled out there is no target, though the enemies are counted; By(null) throws.</summary>
        public static void RuledOut(FakeWorld world)
        {
            var fighter = Brain(world, 0f, 0f, 1000f).Fighter;
            var near = At(world, 1, 300f, 0f);
            var far = At(world, 1, 600f, 0f);
            fighter.Targets.Add(Target.Nearest);
            fighter.Targets.Add(Target.By(u => u == near ? 0f : 5f));
            Expect.Same(far, Check(world, fighter), "the nearest ruled out by its 0, and 5 clamped to 1");
            fighter.Targets.Add(Target.By(u => -3f));
            Expect.True(Check(world, fighter) == null, "-3 is clamped to 0: every enemy ruled out");
            Expect.True(!fighter.Meets(), "so it meets none to fight");
            Expect.Equal(2, fighter.EnemiesNear, "though both are counted near");
            fighter.Targets.Clear();
            fighter.Targets.Add(Target.By(u => u == near ? 0f : 1f).Weight(0f));
            Expect.Same(far, Check(world, fighter), "a rule of weight 0 still rules an enemy out");
            Expect.Throws<ArgumentNullException>(() => Target.By(null!), "By(null)");
        }

        /// <summary>Weights are powers: the score is the rules' scores, each to its weight, multiplied, to 1 over the weights' sum; Weight gives a new rule and leaves the old; below 0 throws; MostDangerous reads damage over cooldown, below 1 a second counted as 1.</summary>
        public static void Weights(FakeWorld world)
        {
            var fighter = Brain(world, 0f, 0f, 1000f).Fighter;
            var strong = At(world, 1, 200f, 0f);
            var weak = At(world, 1, 400f, 0f);
            weak.Life = 50f;
            fighter.Targets.Add(Target.Nearest);
            fighter.Targets.Add(Target.Weakest);
            // The strong one: nearest 1, weakest 50 / 100 = 0.5; the weak one: nearest 200 / 400 = 0.5, weakest 1.
            Expect.Same(strong, Check(world, fighter), "both (1 x 0.5)^(1/2) = 0.71: the first met");
            var weakest = Target.Weakest;
            fighter.Targets[1] = weakest.Weight(2f);
            Expect.Same(weak, Check(world, fighter), "Weakest weighs 2: (0.5 x 1^2)^(1/3) = 0.79 against (1 x 0.5^2)^(1/3) = 0.63, more than 0.63 x 1.1");
            Expect.Near(1f, weakest.weight, "Weight left the rule it was called on as it was");
            Expect.Throws<ArgumentException>(() => Target.Nearest.Weight(-1f), "a weight below 0");

            var danger = Brain(world, 10000f, 0f, 1000f).Fighter;
            danger.Targets.Add(Target.MostDangerous);
            var slow = At(world, 1, 10100f, 0f);
            world.SetAverageDamage(slow.Handle, 30f);
            world.SetAttackCooldown(slow.Handle, 3f);
            var quick = At(world, 1, 10500f, 0f);
            world.SetAverageDamage(quick.Handle, 15f);
            world.SetAttackCooldown(quick.Handle, 1f);
            var harmless = At(world, 1, 10050f, 0f);
            Expect.Same(quick, Check(world, danger), "15 a second before 30 every 3 s, 10 a second");
            quick.Kill();
            slow.Kill();
            Expect.Same(harmless, Check(world, danger), "no attack counts as 1 a second: scored, never ruled out");
        }

        /// <summary>The fight attacks its target, casts the first spell whose aim has someone, whose condition holds and that is ready, watches the cast while the unit carries it, then goes back to the attack; a spell the unit lacks, on cooldown or short of mana is skipped; each aim; and the class overloads read the class's id.</summary>
        public static void Spells(FakeWorld world)
        {
            var brain = Brain(world, 0f, 0f, 800f);
            var unit = brain.Unit;
            var fighter = brain.Fighter;
            var foe = At(world, 1, 300f, 0f);
            var tree = new Tree(brain, CombatNodes.Fight(fighter));
            Is(Status.Failed, tree.Run(), "no target before a check");
            Check(world, fighter);
            Is(Status.Running, tree.Run(), "it attacks its target");
            Expect.Equal("target attack", Given(world), "the attack");

            fighter.Spells.Add(Spell.On(BoltId, "holybolt", when: t => t.Life > 50f));
            tree.Run();
            Expect.Equal(1, world.Orders.Count, "a spell the unit lacks is skipped, and the attack's repeat dropped");
            unit.AddAbility(BoltId);
            Is(Status.Running, tree.Run(), "ready: cast");
            Expect.Equal("target attack, target holybolt", Given(world), "on its target");
            Expect.Same(foe.Handle, world.OrderTargets[1], "the target");
            Expect.Equal(HolyBoltId, world.CurrentOrderOf(unit.Handle), "the unit carries the cast");
            Is(Status.Running, tree.Run(), "watched while carried");
            Expect.Equal(2, world.Orders.Count, "not ordered again");

            var bolt = unit.AbilityOf(BoltId);
            bolt.StartCooldown(5f);
            world.SetCurrentOrder(unit.Handle, 0);
            Is(Status.Running, tree.Run(), "the cast is over");
            Expect.Equal("target attack", world.Orders[2], "back to the attack, the spell on its cooldown");
            bolt.ResetCooldown();
            unit.MaxMana = 100f;
            unit.Mana = 20f;
            bolt.LiveManaCost = 50;
            tree.Run();
            Expect.Equal(3, world.Orders.Count, "20 mana for a cost of 50: not ready");
            foe.Life = 40f;
            unit.Mana = 50f;
            tree.Run();
            Expect.Equal(3, world.Orders.Count, "mana enough, but its condition does not hold for a target at 40 life");
            foe.Life = 100f;
            tree.Run();
            Expect.Equal("target holybolt", world.Orders[3], "mana at the cost and the condition holding: cast");
            world.SetCurrentOrder(unit.Handle, 0);
            bolt.StartCooldown(5f);

            fighter.Spells.Clear();
            unit.AddAbility(ClapId);
            unit.AddAbility(BlizzardId);
            fighter.Spells.Add(Spell.Self(ClapId, "immolation", when: f => f.EnemiesNear >= 2));
            fighter.Spells.Add(Spell.At(BlizzardId, "blizzard"));
            world.Orders.Clear();
            world.OrderPoints.Clear();
            Is(Status.Running, tree.Run(), "the first spell's condition does not hold with one enemy near: the second");
            Expect.Equal("point blizzard, target attack", Given(world), "at the target's place, and the unit, idle at once after it, attacks in the same pass");
            Expect.Near(300f, world.OrderPoints[0], "x");
            Expect.Near(0f, world.OrderPoints[1], "y");
            At(world, 1, 0f, 300f);
            Check(world, fighter);
            tree.Run();
            Expect.Equal("immediate immolation", world.Orders[2], "two enemies near: the first spell, though the second is ready too");

            world.SetCurrentOrder(unit.Handle, 0);
            fighter.Spells.Clear();
            unit.AddAbility(HealId);
            var friend = At(world, 0, 100f, 0f);
            friend.Life = 80f;
            var wounded = At(world, 0, 0f, 100f);
            wounded.Life = 30f;
            unit.Life = 50f;
            var asked = new List<Unit>();
            fighter.Spells.Add(Spell.Ally(HealId, "holybolt", when: a =>
            {
                asked.Add(a);
                return a == unit;
            }));
            Check(world, fighter);
            world.Orders.Clear();
            world.OrderTargets.Clear();
            tree.Run();
            Expect.Equal(2, asked.Count, "the condition is checked from the most hurt, until it holds");
            Expect.Same(wounded, asked[0], "first the ally at 30%");
            Expect.Same(unit, asked[1], "then its own unit at 50%, which is counted");
            Expect.Equal("target holybolt", Given(world), "cast on the first for whom it holds");
            Expect.Same(unit.Handle, world.OrderTargets[0], "its own unit");
            world.SetCurrentOrder(unit.Handle, 0);
            fighter.Spells.Clear();
            fighter.Spells.Add(Spell.Ally(HealId, "holybolt"));
            tree.Run();
            Expect.Same(wounded.Handle, world.OrderTargets[1], "with no condition, the most hurt below its most life");
            world.SetCurrentOrder(unit.Handle, 0);
            friend.Life = 100f;
            wounded.Life = 100f;
            unit.Life = 100f;
            Check(world, fighter);
            world.Orders.Clear();
            tree.Run();
            Expect.Equal("target attack", Given(world), "nobody hurt: no ally to aim at, so it attacks");

            ObjectTypes.Id<TestBolt>(ObjectTypes.FourCC("A0cb"));
            var typed = Spell.On<TestBolt>("thunderbolt");
            Expect.Equal(ObjectTypes.FourCC("A0cb"), typed.AbilityId, "On with a class reads its id");
            Expect.Equal("thunderbolt", typed.Order, "and keeps the order string");
            Expect.Equal(ObjectTypes.FourCC("A0cb"), Spell.Self<TestBolt>("thunderbolt").AbilityId, "Self with a class");
            Expect.Equal(ObjectTypes.FourCC("A0cb"), Spell.Ally<TestBolt>("thunderbolt").AbilityId, "Ally with a class");
            Expect.Equal(ObjectTypes.FourCC("A0cb"), Spell.At<TestBolt>("thunderbolt").AbilityId, "At with a class");
        }

        /// <summary>When the unit's order id leaves the cast's, the cast over, the brain is woken and attacks again within two steps, not at its next tick: the game leaves the unit idle after a cast, and full control holds the target it picks then without a wake.</summary>
        public static void CastEnds(FakeWorld world)
        {
            var unit = At(world, 0, 0f, 0f);
            unit.AddAbility(BoltId);
            var walker = unit.Add(new LaneWalker { MeetRange = 600f, Spells = { Spell.On(BoltId, "holybolt") } });
            walker.ThinkEvery = 0f;
            At(world, 1, 300f, 0f);
            world.Orders.Clear();
            walker.Wake();
            BrainCases.Steps(world, 1);
            Expect.Equal("target holybolt", Given(world), "woken, it casts on its target");
            unit.AbilityOf(BoltId).StartCooldown(10f);
            BrainCases.Steps(world, 5);
            Expect.Equal(1, world.Orders.Count, "nothing while the unit carries the cast out");
            world.SetCurrentOrder(unit.Handle, 0);
            BrainCases.Steps(world, 2);
            Expect.Equal("target holybolt, target attack", Given(world), "the cast over, it is woken and attacks again");
            BrainCases.Steps(world, 5);
            Expect.Equal(2, world.Orders.Count, "one wake: the watch has ended");
        }

        /// <summary>A cast the game refuses falls through to the attack in the same pass; refused again, the attack the unit carries out is left alone, though the brain forgot its last order; a refused spell gives way to the next.</summary>
        public static void Refused(FakeWorld world)
        {
            var brain = Brain(world, 0f, 0f, 800f);
            var unit = brain.Unit;
            var fighter = brain.Fighter;
            At(world, 1, 300f, 0f);
            unit.AddAbility(BoltId);
            world.Dummies.Spell(BoltId, "thunderbolt", SpellKind.Target, (caster, target, x, y) => { });
            world.Dummies.Refuse(BoltId);
            fighter.Spells.Add(Spell.On(BoltId, "thunderbolt"));
            var tree = new Tree(brain, CombatNodes.Fight(fighter));
            Check(world, fighter);
            Is(Status.Running, tree.Run(), "it fights");
            Expect.Equal("target thunderbolt, target attack", Given(world), "the refused cast falls through to the attack in the same pass");
            tree.Run();
            Expect.Equal("target thunderbolt, target attack, target thunderbolt", Given(world), "refused again: the attack it carries out is not sent again");
            Expect.Equal("attack", unit.CurrentOrder, "and goes on");
            unit.AddAbility(ClapId);
            fighter.Spells.Add(Spell.Self(ClapId, "immolation"));
            tree.Run();
            Expect.Equal("immediate immolation", world.Orders[world.Orders.Count - 1], "a refused spell gives way to the next");
        }

        /// <summary>It pulls back below 30% of its life or outnumbered two to one, its unit counted and structures on neither side, and comes back at 70% and not outnumbered; a Below and an Outnumbered of 0 turn them off; the node walks back along the route from the point before the nearest to the start and stands there on hold, or goes to its To.</summary>
        public static void PullingBack(FakeWorld world)
        {
            var brain = Brain(world, 0f, 0f, 600f);
            var unit = brain.Unit;
            var fighter = brain.Fighter;
            var foe = At(world, 1, 300f, 0f);
            BrainCases.Steps(world, 1);
            Expect.True(!fighter.ShouldPullBack(), "whole, one to one");
            unit.Life = 29f;
            BrainCases.Steps(world, 1);
            Expect.True(fighter.ShouldPullBack(), "below 30% of its life");
            Expect.True(fighter.PullingBack, "it pulls back");
            unit.Life = 69f;
            BrainCases.Steps(world, 1);
            Expect.True(fighter.ShouldPullBack(), "below 70%, it stays back");
            unit.Life = 70f;
            BrainCases.Steps(world, 1);
            Expect.True(!fighter.ShouldPullBack(), "at 70%, not outnumbered: it comes back");
            Expect.True(!fighter.PullingBack, "no longer pulling back");
            unit.Life = 31f;
            BrainCases.Steps(world, 1);
            Expect.True(!fighter.ShouldPullBack(), "at 31%, coming from the fight, it fights on");

            unit.Life = 100f;
            var second = At(world, 1, 0f, 300f);
            BrainCases.Steps(world, 1);
            Expect.True(fighter.ShouldPullBack(), "two enemies to its one: outnumbered");
            Expect.Equal(2, fighter.EnemiesNear, "two enemies");
            Expect.Equal(1, fighter.AlliesNear, "its own unit");
            var friend = At(world, 0, -100f, 0f);
            var tower = At(world, 1, 0f, -300f);
            world.MakeStructure(tower.Handle);
            var wall = At(world, 0, 0f, 100f);
            world.MakeStructure(wall.Handle);
            BrainCases.Steps(world, 1);
            Expect.True(!fighter.ShouldPullBack(), "an ally comes: two to two, structures counted on neither side");
            Expect.Equal(2, fighter.EnemiesNear, "the enemy structure not counted");
            Expect.Equal(2, fighter.AlliesNear, "nor its own player's");
            foe.Kill();
            second.Kill();
            Expect.Same(tower, Check(world, fighter), "an enemy structure is fought");
            friend.Kill();
            fighter.PullBack.Outnumbered = 0f;
            At(world, 1, 200f, 200f);
            At(world, 1, 200f, -200f);
            BrainCases.Steps(world, 1);
            Expect.True(!fighter.ShouldPullBack(), "Outnumbered 0: never for numbers, at two to one");
            fighter.PullBack.Below = 0f;
            unit.Life = 10f;
            BrainCases.Steps(world, 1);
            Expect.True(!fighter.ShouldPullBack(), "Below and Outnumbered 0: never");

            var back = Brain(world, 1100f, 5000f, 600f);
            back.Fighter.Route = Through(0f, 5000f, 500f, 5000f, 1000f, 5000f, 1500f, 5000f);
            var tree = new Tree(back, CombatNodes.PullBack(back.Fighter));
            Is(Status.Running, tree.Run(), "it walks back");
            Expect.Equal("point move", Given(world), "with a move");
            Expect.Near(500f, world.OrderPoints[0], "to the point before the one nearest it, 1000");
            tree.Run();
            Expect.Equal(1, world.Orders.Count, "the repeat dropped while it walks");
            world.PlaceUnit(back.Unit.Handle, 520f, 5000f);
            tree.Run();
            Expect.Near(0f, world.OrderPoints[2], "within 64 of it: on to the start");
            world.PlaceUnit(back.Unit.Handle, 10f, 5000f);
            Is(Status.Running, tree.Run(), "at the start");
            Expect.Equal("immediate holdposition", world.Orders[2], "it stands on hold position");
            tree.Run();
            Expect.Equal(3, world.Orders.Count, "the hold's repeat dropped");
            back.Fighter.PullBack.To = new Vector2(-500f, 5000f);
            tree.Run();
            Expect.Equal("point move", world.Orders[3], "To set: it goes there");
            Expect.Near(-500f, world.OrderPoints[4], "x");
            back.Fighter.PullBack.To = null;
            back.Fighter.Route = null;
            tree.Run();
            Expect.Equal("immediate holdposition", world.Orders[4], "no route, no To and no fight: it stands where it is");
        }

        /// <summary>The spot where the walker leaves its route is kept, and an enemy farther than the leash from it is dropped, while one within it is fought; the spot is forgotten once the unit is back within 64 of a route point with none met; with no route it walks back to the spot and forgets it there.</summary>
        public static void Leash(FakeWorld world)
        {
            var brain = Brain(world, 500f, 0f, 600f);
            var unit = brain.Unit;
            var fighter = brain.Fighter;
            fighter.Route = Through(0f, 0f, 500f, 0f, 1000f, 0f);
            fighter.Leash = 1000f;
            var runner = At(world, 1, 500f, 400f);
            Expect.Same(runner, Check(world, fighter), "met");
            Expect.True(fighter.hasSpot, "the spot where it left its route is kept");
            Expect.Near(500f, fighter.spotX, "x");
            Expect.Near(0f, fighter.spotY, "y");
            world.PlaceUnit(unit.Handle, 500f, 800f);
            world.PlaceUnit(runner.Handle, 500f, 1100f);
            Expect.True(Check(world, fighter) == null, "1,100 from the spot, beyond its leash of 1,000, though 300 from the unit: dropped");
            Expect.True(fighter.hasSpot, "the spot is kept away from the route");
            var other = At(world, 1, 900f, 800f);
            Expect.Same(other, Check(world, fighter), "894 from the spot, within the leash: fought");
            other.Kill();
            world.PlaceUnit(unit.Handle, 1000f, 40f);
            Expect.True(Check(world, fighter) == null, "none met");
            Expect.True(!fighter.hasSpot, "back within 64 of a point of its route with none met: the spot is forgotten");
            world.PlaceUnit(runner.Handle, 1000f, 600f);
            Expect.Same(runner, Check(world, fighter), "the runner comes into its meet range: met again");
            Expect.Near(40f, fighter.spotY, "the leash now from where it stands");

            var guard = Brain(world, 5000f, 0f, 600f);
            var walk = new Tree(guard, CombatNodes.WalkLane(guard.Fighter));
            Is(Status.Failed, walk.Run(), "no route and no fight: nothing to walk");
            var raider = At(world, 1, 5000f, 400f);
            Check(world, guard.Fighter);
            world.PlaceUnit(guard.Unit.Handle, 5000f, 300f);
            raider.Kill();
            Expect.True(Check(world, guard.Fighter) == null, "the fight is over");
            Expect.True(guard.Fighter.hasSpot, "the spot it stood on is kept");
            world.Orders.Clear();
            world.OrderPoints.Clear();
            Is(Status.Running, walk.Run(), "with no route it walks back");
            Expect.Equal("point move", Given(world), "with a move");
            Expect.Near(0f, world.OrderPoints[1], "to where it stood");
            world.PlaceUnit(guard.Unit.Handle, 5000f, 30f);
            Check(world, guard.Fighter);
            Expect.True(!guard.Fighter.hasSpot, "within 64 of the spot: forgotten");
            Is(Status.Failed, walk.Run(), "and nothing is left to walk");
        }

        /// <summary>An enemy that damages the unit is met however far, for 3 s after its last hit, but not counted near; a new attacker wakes the brain once; an ally's hit is not noted; Attacker scores 1 for one that hit, 0.5 for another.</summary>
        public static void Attacker(FakeWorld world)
        {
            var brain = Brain(world, 0f, 0f, 500f);
            var unit = brain.Unit;
            var fighter = brain.Fighter;
            var mortar = At(world, 1, 900f, 0f);
            Expect.True(Check(world, fighter) == null, "900 away, beyond its meet range of 500");
            mortar.Damage(unit, 1f);
            Expect.Equal(0, brain.Thinks, "not at once");
            BrainCases.Steps(world, 1);
            Expect.Equal(1, brain.Thinks, "a new attacker wakes the brain at the next step");
            Expect.Same(mortar, Check(world, fighter), "the attacker is met however far");
            Expect.Equal(0, fighter.EnemiesNear, "but not counted near");
            mortar.Damage(unit, 1f);
            BrainCases.Steps(world, 1);
            Expect.Equal(1, brain.Thinks, "the same attacker's next hit wakes nothing");
            BrainCases.Steps(world, 140);
            Expect.Same(mortar, Check(world, fighter), "still met 2.84 s after its last hit");
            BrainCases.Steps(world, 10);
            Expect.True(Check(world, fighter) == null, "forgotten 3 s after it");
            mortar.Damage(unit, 1f);
            BrainCases.Steps(world, 1);
            Expect.Equal(2, brain.Thinks, "a forgotten attacker's hit is new again");

            var calm = Brain(world, 5000f, 0f, 500f);
            var friend = At(world, 0, 5900f, 0f);
            friend.Damage(calm.Unit, 1f);
            BrainCases.Steps(world, 1);
            Expect.Equal(0, calm.Thinks, "an ally's hit wakes nothing");
            Expect.True(Check(world, calm.Fighter) == null, "and is not met");

            var picky = Brain(world, 10000f, 0f, 1000f);
            picky.Fighter.Targets.Add(Target.Attacker);
            var idle = At(world, 1, 10200f, 0f);
            var hitter = At(world, 1, 10400f, 0f);
            Expect.Same(idle, Check(world, picky.Fighter), "both score 0.5: the first met");
            hitter.Damage(picky.Unit, 1f);
            Expect.Same(hitter, Check(world, picky.Fighter), "1 for the one that hit it against 0.5");
        }

        /// <summary>An enemy its owner does not see is not met, nor counted, nor kept as the target, an attacker too.</summary>
        public static void Unseen(FakeWorld world)
        {
            var brain = Brain(world, 0f, 0f, 600f);
            var fighter = brain.Fighter;
            var hidden = At(world, 1, 200f, 0f);
            var seen = At(world, 1, 400f, 0f);
            world.Dummies.Hide(world.PlayerAt(0), hidden.Handle);
            Expect.Same(seen, Check(world, fighter), "the nearer one, unseen, is skipped");
            Expect.Equal(1, fighter.EnemiesNear, "and not counted");
            world.Dummies.Invisible(seen.Handle);
            Expect.True(Check(world, fighter) == null, "a target its owner no longer sees is dropped");
            var sneak = At(world, 1, 900f, 0f);
            world.Dummies.Invisible(sneak.Handle);
            sneak.Damage(brain.Unit, 1f);
            Expect.True(Check(world, fighter) == null, "an unseen attacker is skipped too");
        }

        /// <summary>A meet range of 0 takes the unit's acquisition range, read at each check.</summary>
        public static void MeetRange(FakeWorld world)
        {
            var brain = Brain(world, 0f, 0f, 0f);
            var fighter = brain.Fighter;
            world.SetAcquireRange(brain.Unit.Handle, 400f);
            var inside = At(world, 1, 300f, 0f);
            var outside = At(world, 1, 500f, 0f);
            Expect.Same(inside, Check(world, fighter), "within its acquisition range of 400");
            inside.Kill();
            Expect.True(Check(world, fighter) == null, "500 away is beyond it");
            world.SetAcquireRange(brain.Unit.Handle, 600f);
            Expect.Same(outside, Check(world, fighter), "read at each check");
        }

        /// <summary>A lane walker's settings are its fighter's, set in braces, with the defaults kept; it thinks every 0.25 s with full control; at its first think it walks its route; a map's own Root is its tree.</summary>
        public static void LaneWalkerSettings(FakeWorld world)
        {
            var route = Through(0f, 0f, 500f, 0f, 1000f, 0f);
            var unit = At(world, 0, 0f, 0f);
            world.Orders.Clear();
            world.OrderPoints.Clear();
            var walker = unit.Add(new LaneWalker(route)
            {
                MeetRange = 600f,
                Leash = 1500f,
                Targets = { Target.Weakest, Target.Nearest.Weight(2f) },
                Spells = { Spell.Self(ClapId, "thunderclap") },
                PullBack = { Below = 0.25f, To = new Vector2(-100f, 0f) },
            });
            var fighter = walker.Fighter;
            Expect.Same(route, fighter.Route, "the route");
            Expect.Near(600f, fighter.MeetRange, "the meet range");
            Expect.Near(1500f, fighter.Leash, "the leash");
            Expect.Equal(2, fighter.Targets.Count, "the target rules");
            Expect.Equal(1, fighter.Spells.Count, "the spells");
            Expect.Near(0.25f, fighter.PullBack.Below, "pulling back below a quarter");
            Expect.Near(0.7f, fighter.PullBack.ComeBackAt, "coming back at the default");
            Expect.Near(-100f, fighter.PullBack.To!.Value.X, "to a place");
            Expect.True(walker.FullControl, "full control");
            Expect.True(walker.ThinkEvery > 0.24f && walker.ThinkEvery < 0.27f, "a think every 0.25 s, not " + walker.ThinkEvery);

            var plain = new LaneWalker();
            Expect.True(plain.Route == null, "no route by default");
            Expect.Near(0f, plain.MeetRange, "the acquisition range by default");
            Expect.Near(2100f, plain.Leash, "a leash of 2,100");
            Expect.Equal(0, plain.Targets.Count, "no rules, so the nearest");
            Expect.Near(0.3f, plain.PullBack.Below, "below 30%");
            Expect.Near(2f, plain.PullBack.Outnumbered, "two to one");
            Expect.Near(0.7f, plain.PullBack.ComeBackAt, "back at 70%");
            Expect.True(plain.PullBack.To == null, "back along its route");

            BrainCases.Steps(world, 14);
            Expect.Equal("point move", Given(world), "its first think walks the route");
            Expect.Near(500f, world.OrderPoints[0], "to the point after the nearest");

            var own = At(world, 0, 3000f, 0f).Add(new OwnTreeWalker());
            BrainCases.Steps(world, 14);
            Expect.True(own.Passes > 0, "a map's own Root is the tree it runs");
        }
    }
}
