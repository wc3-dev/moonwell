using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>A unit component that keeps each hit its unit takes: the amount, and how many were attacks.</summary>
    public sealed class HitLog : UnitComponent
    {
        /// <summary>The amount of each hit, in order.</summary>
        public List<float> Amounts = new List<float>();

        /// <summary>How many of the hits were attacks.</summary>
        public int Attacks;

        /// <summary>The simulation's time when the unit died, as a case notes it.</summary>
        public float DiedAt;

        /// <summary>Keeps the hit.</summary>
        public override void OnDamaged(Damage damage)
        {
            Amounts.Add(damage.Amount);
            if (damage.IsAttack)
            {
                Attacks++;
            }
        }
    }

    /// <summary>The unit class of the map-start case, registered by hand.</summary>
    public sealed class Marcher : Unit
    {
    }

    /// <summary>The map-start case's game component: counts the game's starts, and makes a Marcher for player one 0.1 s after the start.</summary>
    public sealed class MarchStart : GameComponent
    {
        /// <summary>How many times the game started.</summary>
        public int Starts;

        /// <summary>The Marcher it made; null until then.</summary>
        public Unit? Made;

        /// <summary>Counts the start and makes the Marcher a moment later: the case gives its type numbers after Start returns.</summary>
        public override void OnGameStart()
        {
            Starts++;
            After(0.1f, () =>
            {
                Made = Unit.Create(SimulationCases.MarcherType, Player.One, 0.0f, 0.0f, Game.Instance);
            });
        }
    }

    /// <summary>The map-start case's player component, on each playing slot.</summary>
    public sealed class MarchSeat : PlayerComponent
    {
    }

    /// <summary>The map-start case's map: slots 0 and 2 on Blue, slot 1 an orc computer on Red, a game component and a player component.</summary>
    public sealed class MarchMap : Map
    {
        /// <summary>The team of slots 0 and 2.</summary>
        public static readonly Team Blue = new Team("Blue");

        /// <summary>The team of slot 1.</summary>
        public static readonly Team Red = new Team("Red");

        /// <summary>Lists the slots and the components.</summary>
        public MarchMap()
        {
            Slots.Add(new Slot(0, Blue, Controller.User), new Slot(1, Red, Controller.Computer, Race.Orc), new Slot(2, Blue, Controller.Computer));
            Components.Add(new MarchStart());
            PlayerComponents.Add(() => new MarchSeat());
        }
    }

    /// <summary>
    /// Cases for the fast simulation (decisions 312 to 314): its steps, the hold, walking, fighting, acquiring, seeds and the
    /// read-outs. Each case starts its own simulation, which installs a fresh fake world, and makes its units with
    /// Unit.Create so the play seeds them. Every expected number follows the play's rules: an order is stamped with the next
    /// play step and stands the unit 8 steps from it; a walk of d at speed s takes ceil(d x 50 / s) steps.
    /// </summary>
    public static class SimulationCases
    {
        private static readonly int Walker = ObjectTypes.FourCC("s0wk");
        private static readonly int Unplayed = ObjectTypes.FourCC("s0un");
        private static readonly int Numbered = ObjectTypes.FourCC("s0sd");
        private static readonly int Attacker = ObjectTypes.FourCC("s0at");
        private static readonly int Target = ObjectTypes.FourCC("s0tg");
        private static readonly int OtherTarget = ObjectTypes.FourCC("s0ot");

        /// <summary>The type of the map-start case's Marcher.</summary>
        public static readonly int MarcherType = ObjectTypes.FourCC("s0mr");

        /// <summary>Runs the simulation one 0.02 s step at a time.</summary>
        private static void Steps(Simulation sim, int count)
        {
            for (var i = 0; i < count; i++)
            {
                sim.Run(0.02f);
            }
        }

        /// <summary>Makes a unit of a type for a player at a place, in the game's scope.</summary>
        private static Unit Make(int typeId, Player owner, float x, float y)
        {
            return Unit.Create(typeId, owner, x, y, Game.Instance);
        }

        /// <summary>A walker: 270 a second, no attack.</summary>
        private static UnitNumbers WalkerNumbers()
        {
            return new UnitNumbers { Life = 100.0f, MoveSpeed = 270.0f };
        }

        /// <summary>Run(0.1) runs 5 steps and Run(0) none; RunUntil stops at the step its check holds, or after its steps. Within a step Moonwell's timers run before the play: an order given in a timer at step 8 is stamped 8, one given between runs after step 8 is stamped 9, as when each starts walking shows.</summary>
        public static void StepsInOrder(FakeWorld world)
        {
            var sim = Simulation.Start();
            sim.SetNumbers(Walker, WalkerNumbers());
            var first = Make(Walker, Player.One, 0.0f, 0.0f);
            var second = Make(Walker, Player.One, 0.0f, 1000.0f);
            var ticks = 0;
            Game.Instance.Every(0.02f, () =>
            {
                ticks++;
                if (ticks == 8)
                {
                    first.OrderAt("move", 500.0f, 0.0f);
                }
            });

            sim.Run(0.1f);
            Expect.Near(0.1f, sim.Time, "0.1 s is 5 steps"); // 100 ms / 20 ms
            Expect.Equal(5, ticks, "the timer ran at each of the 5 steps");
            sim.Run(0.0f);
            Expect.Near(0.1f, sim.Time, "Run(0) runs no step");
            Expect.Equal(5, ticks, "and no timer");
            Expect.True(sim.RunUntil(() => true, 1.0f), "a check that holds at once gives true");
            Expect.Near(0.1f, sim.Time, "before any step");

            // Steps 6 to 8: the timer's 8th run, at step 8 before the play, orders first: stamped 8, ready at 16 (8 + 8).
            Steps(sim, 3);
            // Between runs: stamped 9, ready at 17 (9 + 8).
            second.OrderAt("move", 500.0f, 1000.0f);
            Steps(sim, 7); // steps 9 to 15
            Expect.Near(0.0f, first.X, "first stands steps 8 to 15");
            Steps(sim, 1); // step 16; a walk of 500 at 270 is ceil(500 x 50 / 270) = ceil(92.59) = 93 steps
            Expect.Near(500.0f * 1.0f / 93.0f, first.X, "first's first walking step is 16"); // 5.38
            Expect.Near(0.0f, second.X, "second still stands at 16");
            Steps(sim, 1); // step 17
            Expect.Near(500.0f * 2.0f / 93.0f, first.X, "first's second walking step"); // 10.75
            Expect.Near(500.0f * 1.0f / 93.0f, second.X, "second's first walking step is 17");
            Expect.Near(0.16f, sim.StoodOf(first), "first stood 8 steps"); // 8 x 0.02
            Expect.Near(0.16f, sim.StoodOf(second), "second stood 8 steps");

            Expect.True(sim.RunUntil(() => ticks >= 20, 1.0f), "a check that comes to hold gives true");
            Expect.Equal(20, ticks, "at the step it holds");
            Expect.Near(0.4f, sim.Time, "step 20"); // 20 / 50
            Expect.True(!sim.RunUntil(() => false, 0.1f), "a check that never holds gives false");
            Expect.Near(0.5f, sim.Time, "after its 5 steps"); // step 25
        }

        /// <summary>An order stands the unit 8 steps (0.15 s rounded as brain ticks are) and is counted; a repeat restarts the hold and is counted too; an order string the fake does not know (id 0) is neither.</summary>
        public static void Hold(FakeWorld world)
        {
            var sim = Simulation.Start();
            sim.SetNumbers(Walker, WalkerNumbers());
            var first = Make(Walker, Player.One, 0.0f, 0.0f);
            first.OrderAt("move", 500.0f, 0.0f); // stamped 1, ready at 9: stands steps 1 to 8
            Steps(sim, 20);
            Expect.Equal(1, sim.OrdersOf(first), "one order");
            Expect.Near(0.16f, sim.StoodOf(first), "stood 8 steps"); // 8 x 0.02
            Expect.Near(500.0f * 12.0f / 93.0f, first.X, "then walked steps 9 to 20"); // 12 of ceil(500 x 50 / 270) = 93

            var second = Make(Walker, Player.One, 0.0f, 1000.0f);
            second.OrderAt("move", 500.0f, 1000.0f); // after step 20: stamped 21, ready at 29
            Steps(sim, 4); // steps 21 to 24 stood
            second.OrderAt("move", 500.0f, 1000.0f); // the repeat after step 24: stamped 25, ready at 33
            Steps(sim, 20); // steps 25 to 32 stood, 33 to 44 walked
            Expect.Equal(2, sim.OrdersOf(second), "the repeat is counted");
            Expect.Near(0.24f, sim.StoodOf(second), "and restarts the hold"); // 4 + 8 = 12 steps x 0.02
            Expect.Near(500.0f * 12.0f / 93.0f, second.X, "walking from step 33, not 29"); // 12 steps; from 29 it would be 16

            var walked = first.X;
            Expect.Near(500.0f * 36.0f / 93.0f, walked, "first walks on"); // steps 9 to 44
            first.OrderAt("blink", 0.0f, 0.0f); // the fake knows no "blink": id 0, which leaves the unit idle
            Steps(sim, 10);
            Expect.Equal(1, sim.OrdersOf(first), "an order of id 0 is not counted");
            Expect.Near(0.16f, sim.StoodOf(first), "nor held");
            Expect.Equal("", first.CurrentOrder, "the unit is idle");
            Expect.Near(walked, first.X, "and stands");
        }

        /// <summary>A unit of a type with numbers reads them through Moonwell's own members; a type with no numbers is left to the fake: not seeded, counted, held or moved.</summary>
        public static void Seeded(FakeWorld world)
        {
            var sim = Simulation.Start();
            sim.SetNumbers(Numbered, new UnitNumbers { Life = 420.0f, MoveSpeed = 290.0f, Armor = 3.0f, AttackRange = 100.0f, AttackCooldown = 1.5f, Damage = 12, DamageDice = 2, DamageSides = 4 });
            var unit = Make(Numbered, Player.One, 0.0f, 0.0f);
            Expect.Near(420.0f, unit.Life, "life");
            Expect.Near(420.0f, unit.MaxLife, "max life");
            Expect.Near(290.0f, unit.Get(Stat.MoveSpeed), "move speed");
            Expect.Near(3.0f, unit.Get(Stat.Armor), "armor");
            Expect.Near(17.0f, unit.Get(Stat.Damage), "damage, the middle of 12 + 2d4"); // 12 + 2 x (4 + 1) / 2

            var plain = Make(Unplayed, Player.One, 0.0f, 500.0f);
            Expect.Near(0.0f, plain.Life, "a type with no numbers is not seeded: the fake's 0");
            plain.OrderAt("move", 300.0f, 500.0f);
            Steps(sim, 30);
            Expect.Equal(0, sim.OrdersOf(plain), "nor counted");
            Expect.Near(0.0f, sim.StoodOf(plain), "nor held");
            Expect.Near(0.0f, plain.X, "nor moved");
            Expect.Equal("move", plain.CurrentOrder, "its order is the fake's, as before");
        }

        /// <summary>A move walks straight in ceil(d x 50 / speed) steps from its ready step, part way at a middle step, exactly on the point at the last, idle in that same step; a move of 0 arrives at its first ready step.</summary>
        public static void Walk(FakeWorld world)
        {
            var sim = Simulation.Start();
            sim.SetNumbers(Walker, WalkerNumbers());
            var unit = Make(Walker, Player.One, 100.0f, 200.0f);
            // 300 by 400 is 500 away: ceil(500 x 50 / 270) = ceil(92.59) = 93 steps; ready at 9, so it walks steps 9 to 101.
            unit.OrderAt("move", 400.0f, 600.0f);
            Steps(sim, 54); // 54 - 8 = 46 walking steps
            Expect.Near(100.0f + 300.0f * 46.0f / 93.0f, unit.X, "part way: x"); // 248.39
            Expect.Near(200.0f + 400.0f * 46.0f / 93.0f, unit.Y, "part way: y"); // 397.85
            Expect.Equal("move", unit.CurrentOrder, "still moving");
            Steps(sim, 46); // step 100: 92 of 93
            Expect.Near(100.0f + 300.0f * 92.0f / 93.0f, unit.X, "a step short at 100"); // 396.77
            Expect.Equal("move", unit.CurrentOrder, "still moving at 100");
            Steps(sim, 1); // step 101: 93 of 93
            Expect.True(unit.X == 400.0f && unit.Y == 600.0f, "exactly on the point at step 101, not " + unit.X + ", " + unit.Y);
            Expect.Equal("", unit.CurrentOrder, "idle in that same step");
            Expect.Near(2.02f, sim.Time, "at 2.02 s"); // 101 / 50

            var still = Make(Walker, Player.One, -500.0f, 0.0f);
            still.OrderAt("move", -500.0f, 0.0f); // after step 101: stamped 102, ready at 110
            Steps(sim, 8); // steps 102 to 109 stood
            Expect.Equal("move", still.CurrentOrder, "held to step 109");
            Steps(sim, 1); // step 110
            Expect.Equal("", still.CurrentOrder, "a move of 0 arrives at its first ready step");
            Expect.True(still.X == -500.0f && still.Y == 0.0f, "where it stood");
            Expect.True(unit.X == 400.0f && unit.Y == 600.0f, "the first stays on its point");
        }

        /// <summary>An attack chases to its range, swings every cooldown with no randomness and armor 0, kills at a known step with Moonwell's death reactions naming the attacker, and the attacker is idle the step after.</summary>
        public static void Duel(FakeWorld world)
        {
            var sim = Simulation.Start();
            sim.SetNumbers(Attacker, new UnitNumbers { Life = 100.0f, MoveSpeed = 300.0f, AttackRange = 128.0f, AttackCooldown = 1.0f, Damage = 10, DamageDice = 1, DamageSides = 1 });
            sim.SetNumbers(Target, new UnitNumbers { Life = 50.0f, MoveSpeed = 200.0f });
            var attacker = Make(Attacker, Player.One, 0.0f, 0.0f);
            var target = Make(Target, Player.Two, 400.0f, 0.0f);
            var killer = attacker.Add(new CreditEars());
            var dying = target.Add(new CreditEars());
            attacker.OrderOn("attack", target); // stamped 1, ready at 9
            // It chases 6 a step (300 / 50) from step 9: 400 - 6 x 45 = 130 away at step 53, then max(128, 124) = 128 at 54.
            Steps(sim, 54);
            Expect.Near(272.0f, attacker.X, "chased to its range"); // 400 - 128
            Expect.Near(50.0f, target.Life, "no swing while chasing");
            Steps(sim, 1); // step 55: in range, a swing of 10 + 1d1 = 11
            Expect.Near(39.0f, target.Life, "the first swing at step 55"); // 50 - 11
            // Swings every StepsOf(1.0) = 50 steps: 105, 155, 205, 255 leave 28, 17, 6, then -5, which kills at 255.
            Expect.True(sim.RunUntil(() => !target.IsAlive, 10.0f), "the target dies");
            Expect.Near(5.1f, sim.Time, "at step 255"); // 255 / 50
            Expect.Equal(5, dying.Taken, "after five hits");
            Expect.Same(attacker, dying.Killer, "its OnDeath names the attacker");
            Expect.Same(target, killer.Killed, "the attacker's OnKill runs");
            Expect.Equal("attack", attacker.CurrentOrder, "the attacker notices at its next step");
            Steps(sim, 1); // step 256
            Expect.Equal("", attacker.CurrentOrder, "idle at step 256");
            Expect.Near(272.0f, attacker.X, "where it fought");
        }

        /// <summary>One swing of 100 on armor 2 and on armor -2 deals the game's 89.29 and 111.64, as an attack.</summary>
        public static void Armor(FakeWorld world)
        {
            var sim = Simulation.Start();
            sim.SetNumbers(Attacker, new UnitNumbers { Life = 100.0f, MoveSpeed = 300.0f, AttackRange = 200.0f, AttackCooldown = 2.0f, Damage = 100 });
            sim.SetNumbers(Target, new UnitNumbers { Life = 1000.0f, MoveSpeed = 200.0f, Armor = 2.0f });
            sim.SetNumbers(OtherTarget, new UnitNumbers { Life = 1000.0f, MoveSpeed = 200.0f, Armor = -2.0f });
            var first = Make(Attacker, Player.One, 0.0f, 0.0f);
            var second = Make(Attacker, Player.One, 0.0f, 1000.0f);
            var armored = Make(Target, Player.Two, 100.0f, 0.0f);
            var bare = Make(OtherTarget, Player.Two, 100.0f, 1000.0f);
            var armoredHits = armored.Add(new HitLog());
            var bareHits = bare.Add(new HitLog());
            first.OrderOn("attack", armored);
            second.OrderOn("attack", bare);
            Steps(sim, 9); // both ready at 9 and in range (100 of 200), so both swing at 9
            Expect.Equal(1, armoredHits.Amounts.Count, "one swing on armor 2");
            Expect.Near(89.2857f, armoredHits.Amounts[0], "100 on armor 2"); // 100 x (1 - 0.12 / 1.12), the game's 89.29
            Expect.Near(910.7143f, armored.Life, "its life"); // 1000 - 89.2857
            Expect.Equal(1, armoredHits.Attacks, "dealt as an attack");
            Expect.Equal(1, bareHits.Amounts.Count, "one swing on armor -2");
            Expect.Near(111.64f, bareHits.Amounts[0], "100 on armor -2"); // 100 x (2 - 0.94^2) = 100 x 1.1164
            Expect.Near(888.36f, bare.Life, "its life"); // 1000 - 111.64
        }

        /// <summary>A fight with dice gives the same hits and death for the same seed, and others for another seed; each swing is 5 plus two draws of new Dice(seed).Int(1, 6), in swing order.</summary>
        public static void Seed(FakeWorld world)
        {
            var once = Fight(1);
            var again = Fight(1);
            var other = Fight(3);

            // Dice(1) draws 2, 6, 5, 1, 1, 4, 3, 4: swings of 13, 11, 10, 12, then 12, 10, 15, 15, 11, 13, 15, 11, 11, 16, 14
            // make 189 after 15, so the 16th (14) kills the 200.
            Expect.Equal(16, once.Amounts.Count, "seed 1 kills in 16 swings");
            Expect.Near(13.0f, once.Amounts[0], "seed 1's first swing"); // 5 + 2 + 6
            Expect.Near(11.0f, once.Amounts[1], "its second"); // 5 + 5 + 1
            Expect.Near(10.0f, once.Amounts[2], "its third"); // 5 + 1 + 4
            Expect.Near(7.68f, once.DiedAt, "at step 384"); // swings every 25 steps from 9: 9 + 25 x 15 = 384, / 50
            Expect.Equal(once.Amounts.Count, again.Amounts.Count, "the same seed, the same swings");
            for (var i = 0; i < once.Amounts.Count; i++)
            {
                Expect.Near(once.Amounts[i], again.Amounts[i], "the same seed's swing " + i);
            }

            Expect.Near(once.DiedAt, again.DiedAt, "and the same death");

            // Dice(3) draws 4, 6, 4, 1, 1, 2, 6, 1: swings of 15, 10, 8, 12, then 12, 15, 12, 13, 15, 14, 11, 16, 9, 14, 9, 9
            // make 194 after 16, so the 17th (14) kills.
            Expect.Equal(17, other.Amounts.Count, "seed 3 kills in 17 swings");
            Expect.Near(15.0f, other.Amounts[0], "another seed, another first swing"); // 5 + 4 + 6
            Expect.Near(8.18f, other.DiedAt, "at step 409"); // 9 + 25 x 16 = 409, / 50
            DrawnInOrder(once, 1);
            DrawnInOrder(other, 3);
        }

        /// <summary>One attacker of 5 + 2d6 every 0.5 s, in range, against 200 life with armor 0, under a seed, until the death.</summary>
        private static HitLog Fight(int seed)
        {
            var sim = Simulation.Start(seed);
            sim.SetNumbers(Attacker, new UnitNumbers { Life = 100.0f, MoveSpeed = 300.0f, AttackRange = 200.0f, AttackCooldown = 0.5f, Damage = 5, DamageDice = 2, DamageSides = 6 });
            sim.SetNumbers(Target, new UnitNumbers { Life = 200.0f, MoveSpeed = 200.0f });
            var attacker = Make(Attacker, Player.One, 0.0f, 0.0f);
            var target = Make(Target, Player.Two, 100.0f, 0.0f);
            var hits = target.Add(new HitLog());
            attacker.OrderOn("attack", target); // ready at 9, in range: swings at 9, 34, 59, ...
            Expect.True(sim.RunUntil(() => !target.IsAlive, 20.0f), "the target dies with seed " + seed);
            hits.DiedAt = sim.Time;
            return hits;
        }

        /// <summary>Checks each hit against 5 plus two draws of the seed's own dice, in order, as the play draws them.</summary>
        private static void DrawnInOrder(HitLog hits, int seed)
        {
            var dice = new Dice(seed);
            for (var i = 0; i < hits.Amounts.Count; i++)
            {
                var raw = 5 + dice.Int(1, 6) + dice.Int(1, 6);
                Expect.Near(raw, hits.Amounts[i], "seed " + seed + "'s swing " + i);
            }
        }

        /// <summary>An idle unit attacks the nearest enemy within its acquisition range, skipping one beyond it, an ally and Neutral Passive, silently: no order call, no order event, not counted, not held.</summary>
        public static void Acquire(FakeWorld world)
        {
            var sim = Simulation.Start();
            sim.SetNumbers(Attacker, new UnitNumbers { Life = 500.0f, MoveSpeed = 300.0f, AttackRange = 100.0f, AttackCooldown = 1.0f, Damage = 10, AcquisitionRange = 500.0f });
            sim.SetNumbers(Target, new UnitNumbers { Life = 1000.0f, MoveSpeed = 200.0f });
            Player.One.MakeAllies(Player.Three);
            var heard = Game.Instance.Add(new OrderedGame());
            var guard = Make(Attacker, Player.One, 0.0f, 0.0f);
            var near = Make(Target, Player.Two, 400.0f, 0.0f);
            var far = Make(Target, Player.Two, -600.0f, 0.0f);
            var ally = Make(Target, Player.Three, 0.0f, 100.0f);
            var passive = Make(Target, Player.NeutralPassive, 0.0f, -50.0f);
            // Step 1: the nearest enemy within 500 is near (400); the ally (100), Neutral Passive (50) and far (600) are not.
            Steps(sim, 1);
            Expect.Equal("attack", guard.CurrentOrder, "it acquires near");
            Expect.Equal(0, sim.OrdersOf(guard), "not counted");
            Expect.Near(0.0f, sim.StoodOf(guard), "nor held");
            Expect.Equal(0, sim.World.Orders.Count, "no order call");
            Expect.True(heard.Heard == null, "and no order event");
            // From step 2 it chases 6 a step: max(100, 400 - 6 x 50) = 100 away at step 51.
            Steps(sim, 50);
            Expect.Near(300.0f, guard.X, "it chased near to its range"); // 400 - 100
            Expect.Near(1000.0f, near.Life, "no swing yet");
            Steps(sim, 1); // step 52: a swing of 10
            Expect.Near(990.0f, near.Life, "near takes the swing");
            Expect.Near(1000.0f, far.Life, "far is left");
            Expect.Near(1000.0f, ally.Life, "the ally is left");
            Expect.Near(1000.0f, passive.Life, "Neutral Passive is left");
        }

        /// <summary>An attack order to a point fights an enemy met on the way, then walks on and ends on the point, idle; the fight is no order.</summary>
        public static void AttackMove(FakeWorld world)
        {
            var sim = Simulation.Start();
            sim.SetNumbers(Attacker, new UnitNumbers { Life = 500.0f, MoveSpeed = 300.0f, AttackRange = 100.0f, AttackCooldown = 0.5f, Damage = 50, AcquisitionRange = 300.0f });
            sim.SetNumbers(Target, new UnitNumbers { Life = 120.0f, MoveSpeed = 200.0f });
            var raider = Make(Attacker, Player.One, 0.0f, 0.0f);
            var foe = Make(Target, Player.Two, 610.0f, 0.0f);
            // Ready at 9. A walk of 764 is ceil(764 x 50 / 300) = ceil(127.33) = 128 steps of 5.96875 (exact in floats):
            // 304.41 at step 59 (51 steps), 305.59 from the foe; 310.375 at step 60 (52 steps), 299.625 from it, within 300.
            raider.OrderAt("attack", 764.0f, 0.0f);
            // Step 61 finds it and chases 6 a step: 299.625 - 6 x 33 = 101.625 at step 93, then 100 away (at 510) at step 94.
            // Swings of 50 at 95, 120 and 145 (cooldown 25 steps) leave 70, 20, then -30, which kills at 145.
            Expect.True(sim.RunUntil(() => !foe.IsAlive, 10.0f), "the foe met on the way dies");
            Expect.Near(2.9f, sim.Time, "at step 145"); // 145 / 50
            Expect.Near(510.0f, raider.X, "where it fought, short of the point"); // 610 - 100
            Expect.Equal("attack", raider.CurrentOrder, "still on its way");
            // From step 146 it walks on: 254 to go, ceil(254 x 50 / 300) = ceil(42.33) = 43 steps, arriving at 146 + 42 = 188.
            Expect.True(sim.RunUntil(() => raider.CurrentOrder == "", 10.0f), "it arrives");
            Expect.Near(3.76f, sim.Time, "at step 188"); // 188 / 50
            Expect.True(raider.X == 764.0f && raider.Y == 0.0f, "on the point, not " + raider.X + ", " + raider.Y);
            Expect.Equal(1, sim.OrdersOf(raider), "the fight on the way is no order");
        }

        /// <summary>Hold position never moves, though an enemy is within its acquisition range, and swings only at one within its attack range, every cooldown.</summary>
        public static void HoldPosition(FakeWorld world)
        {
            var sim = Simulation.Start();
            sim.SetNumbers(Attacker, new UnitNumbers { Life = 500.0f, MoveSpeed = 300.0f, AttackRange = 150.0f, AttackCooldown = 1.0f, Damage = 20, AcquisitionRange = 1000.0f });
            sim.SetNumbers(Target, new UnitNumbers { Life = 100.0f, MoveSpeed = 200.0f });
            var holder = Make(Attacker, Player.One, 0.0f, 0.0f);
            var away = Make(Target, Player.Two, 400.0f, 0.0f);
            holder.Order("holdposition"); // ready at 9
            Steps(sim, 60);
            Expect.True(holder.X == 0.0f && holder.Y == 0.0f, "it never moves to one 400 away");
            Expect.Near(100.0f, away.Life, "nor swings at one beyond its range of 150");
            var close = Make(Target, Player.Two, 100.0f, 0.0f); // after step 60, 100 away
            Steps(sim, 1); // step 61: a swing of 20 at the nearest within 150
            Expect.Near(80.0f, close.Life, "it swings at one in range"); // 100 - 20
            Steps(sim, 49); // step 110; the next swing is at 61 + StepsOf(1.0) = 111
            Expect.Near(80.0f, close.Life, "not before its cooldown");
            Steps(sim, 1); // step 111
            Expect.Near(60.0f, close.Life, "the second swing at step 111"); // 80 - 20
            Expect.True(holder.X == 0.0f && holder.Y == 0.0f, "still where it stood");
            Expect.Near(100.0f, away.Life, "the one away is untouched");
            Expect.Equal("holdposition", holder.CurrentOrder, "it keeps holding");
            Expect.Equal(1, sim.OrdersOf(holder), "one order");
        }

        /// <summary>A stop leaves the unit idle and standing; it is counted and held like any order, and after the hold the idle unit acquires an enemy near.</summary>
        public static void Stop(FakeWorld world)
        {
            var sim = Simulation.Start();
            sim.SetNumbers(Attacker, new UnitNumbers { Life = 500.0f, MoveSpeed = 300.0f, AttackRange = 100.0f, AttackCooldown = 1.0f, Damage = 10, AcquisitionRange = 500.0f });
            sim.SetNumbers(Target, new UnitNumbers { Life = 100.0f, MoveSpeed = 200.0f });
            var unit = Make(Attacker, Player.One, 0.0f, 0.0f);
            unit.OrderAt("move", 1000.0f, 0.0f); // ready at 9; ceil(1000 x 50 / 300) = ceil(166.67) = 167 steps
            Steps(sim, 20); // 12 walking steps
            var stood = 1000.0f * 12.0f / 167.0f; // 71.86
            Expect.Near(stood, unit.X, "walking");
            unit.Order("stop"); // stamped 21, ready at 29
            Expect.Equal("", unit.CurrentOrder, "idle at once");
            Make(Target, Player.Two, 400.0f, 0.0f); // 328.14 away, within 500
            Steps(sim, 8); // steps 21 to 28, held
            Expect.Equal("", unit.CurrentOrder, "it acquires nothing while held");
            Expect.Near(stood, unit.X, "and stands");
            Steps(sim, 1); // step 29
            Expect.Equal("attack", unit.CurrentOrder, "after the hold it acquires the enemy near");
            Expect.Equal(2, sim.OrdersOf(unit), "the stop is counted");
            Expect.Near(0.32f, sim.StoodOf(unit), "and held"); // 8 + 8 = 16 steps x 0.02
            Expect.Near(stood, unit.X, "it moves only from step 30");
        }

        /// <summary>Start(map) starts the map as the game would: its slots playing on their teams, its game component started once, its player component on each playing slot, and the units its code makes played.</summary>
        public static void MapStart(FakeWorld world)
        {
            var sim = Simulation.Start(new MarchMap());
            ObjectTypes.Unit<Marcher>(MarcherType, () => new Marcher());
            sim.SetNumbers(MarcherType, new UnitNumbers { Life = 100.0f, MoveSpeed = 250.0f });
            Expect.True(Player.One.SlotState == SlotState.Playing && Player.Two.SlotState == SlotState.Playing && Player.Three.SlotState == SlotState.Playing, "the three slots play");
            Expect.True(Player.Four.SlotState != SlotState.Playing, "a slot the map does not list does not");
            Expect.True(Player.Two.Controller == Controller.Computer && Player.Two.Race == Race.Orc, "slot 1 is an orc computer");
            Expect.True(Player.One.IsAlly(Player.Three) && Player.Three.IsAlly(Player.One), "Blue's slots are allies");
            Expect.True(Player.One.IsEnemy(Player.Two) && Player.Two.IsEnemy(Player.Three), "Red is their enemy");
            Expect.Equal(1, Player.One.GetAll<MarchSeat>().Count, "player one's component");
            Expect.Equal(1, Player.Two.GetAll<MarchSeat>().Count, "player two's");
            Expect.Equal(1, Player.Three.GetAll<MarchSeat>().Count, "player three's");
            Expect.Equal(0, Player.Four.ComponentCount, "none for a slot not playing");
            var start = Game.Instance.GetAll<MarchStart>()[0];
            Expect.Equal(1, start.Starts, "the game started");

            Steps(sim, 5); // the Marcher is made at 0.1 s, in step 5's timers
            Expect.True(start.Made is Marcher, "the start made a Marcher");
            var marcher = start.Made!;
            Expect.Near(100.0f, marcher.Life, "seeded from its numbers");
            marcher.OrderAt("move", 250.0f, 0.0f); // stamped 6, ready at 14; ceil(250 x 50 / 250) = 50 steps, 14 to 63
            Steps(sim, 33); // steps 6 to 38: 25 walking steps
            Expect.Near(125.0f, marcher.X, "it walks"); // 250 x 25 / 50
            Expect.True(sim.RunUntil(() => marcher.CurrentOrder == "", 2.0f), "and arrives");
            Expect.Near(1.26f, sim.Time, "at step 63"); // 63 / 50
            Expect.Equal(1, start.Starts, "the game started once");
        }

        /// <summary>Alive counts the units a player owns that the fake made, alive and not removed, before and after a death and a removal.</summary>
        public static void Alive(FakeWorld world)
        {
            var sim = Simulation.Start();
            sim.SetNumbers(Target, new UnitNumbers { Life = 100.0f, MoveSpeed = 200.0f });
            var mine = Make(Target, Player.One, 0.0f, 0.0f);
            Make(Target, Player.One, 100.0f, 0.0f);
            var theirs = Make(Target, Player.Two, 0.0f, 500.0f);
            Make(Unplayed, Player.Two, 100.0f, 500.0f);
            Steps(sim, 1);
            Expect.Equal(2, sim.Alive(Player.One), "player one's two");
            Expect.Equal(2, sim.Alive(Player.Two), "player two's two, the one with no numbers too");
            Expect.Equal(0, sim.Alive(Player.Three), "none for a player with no units");
            mine.Kill();
            Expect.Equal(1, sim.Alive(Player.One), "one after a death");
            theirs.Remove();
            Expect.Equal(1, sim.Alive(Player.Two), "one after a removal");
            Steps(sim, 1);
            Expect.Equal(1, sim.Alive(Player.One), "the dead stay out");
            Expect.Equal(1, sim.Alive(Player.Two), "the removed stay out");
        }
    }
}
