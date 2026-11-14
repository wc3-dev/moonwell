using Moonwell.AI.Combat;

namespace Moonwell.Tests
{
    /// <summary>Lane walkers in the fast simulation, on .NET: units made by id walk their lanes, fight what they meet and pull back, with their repeated orders kept from the game.</summary>
    public class CombatSimulationTests
    {
        private static readonly int Walker = ObjectTypes.FourCC("s0lw");
        private static readonly int Grunt = ObjectTypes.FourCC("s0lg");
        private static readonly int Brute = ObjectTypes.FourCC("s0lb");
        private static readonly int Runner = ObjectTypes.FourCC("s0lr");

        /// <summary>A lane walker that counts its thinks.</summary>
        private sealed class CountingWalker : LaneWalker
        {
            public int Thinks;

            public CountingWalker(Route route)
                : base(route)
            {
            }

            protected override void Think()
            {
                Thinks++;
                base.Think();
            }
        }

        /// <summary>A straight lane from one x to another at a y.</summary>
        private static Route Lane(float fromX, float toX, float y)
        {
            return new Route(new List<Vector2> { new Vector2(fromX, y), new Vector2(toX, y) }, true);
        }

        /// <summary>A simulation whose walkers take 4 swings to kill a grunt, which hits back for 5.</summary>
        private static Simulation Start()
        {
            var sim = Simulation.Start();
            sim.SetNumbers(Walker, new UnitNumbers { Life = 400.0f, MoveSpeed = 300.0f, AttackRange = 100.0f, AttackCooldown = 1.0f, Damage = 25, AcquisitionRange = 500.0f });
            sim.SetNumbers(Grunt, new UnitNumbers { Life = 100.0f, MoveSpeed = 200.0f, AttackRange = 100.0f, AttackCooldown = 1.0f, Damage = 5, AcquisitionRange = 300.0f });
            sim.SetNumbers(Brute, new UnitNumbers { Life = 2000.0f, AttackRange = 150.0f, AttackCooldown = 0.5f, Damage = 20, AcquisitionRange = 500.0f });
            sim.SetNumbers(Runner, new UnitNumbers { Life = 5000.0f, MoveSpeed = 200.0f });
            return sim;
        }

        /// <summary>A walker meets a grunt beside its lane, kills it and walks on to the lane's end, and the game gets one order for each thing it did, not one for each think.</summary>
        [Fact]
        public void AWalkerFightsWhatItMeetsAndGoesOn()
        {
            var sim = Start();
            var walker = Unit.Create(Walker, Player.One, 0.0f, 0.0f, Game.Instance);
            var brain = walker.Add(new CountingWalker(Lane(0.0f, 3000.0f, 0.0f)));
            var grunt = Unit.Create(Grunt, Player.Two, 1500.0f, 200.0f, Game.Instance);

            Assert.True(sim.RunUntil(() => walker.X == 3000.0f, 60.0f), "the walker reaches its lane's end, at " + walker.X);
            Assert.False(grunt.IsAlive, "it killed the grunt it met");
            Assert.True(walker.IsAlive, "and lives");
            Assert.True(walker.Life < 400.0f, "hit back on the way: " + walker.Life);
            // A move to the end, the attack on the grunt, and the move on after the kill.
            Assert.Equal(3, sim.OrdersOf(walker));
            Assert.True(brain.Thinks > 30, "it thought " + brain.Thinks + " times");
        }

        /// <summary>A walker chases an enemy fleeing from its lane, as an attack from code has no chase limit of the game's (game facts, 11-14), until the enemy is beyond its leash of 2,100 from where it left the lane; then it drops it and walks on to the lane's end.</summary>
        [Fact]
        public void AWalkerDropsAFleeingEnemyAtItsLeash()
        {
            var sim = Start();
            var walker = Unit.Create(Walker, Player.One, 0.0f, 0.0f, Game.Instance);
            var brain = walker.Add(new CountingWalker(Lane(0.0f, 3000.0f, 0.0f)));
            var runner = Unit.Create(Runner, Player.Two, 600.0f, 0.0f, Game.Instance);
            runner.OrderAt(Orders.Move, 600.0f, 10000.0f);

            Assert.True(sim.RunUntil(() => brain.Fighter.Target == runner, 10.0f), "it meets the runner");
            var fighter = brain.Fighter;
            var farthest = 0.0f;
            Assert.True(sim.RunUntil(() => { farthest = Math.Max(farthest, walker.Y); return fighter.Target == null; }, 30.0f), "it drops the runner");
            Assert.True(runner.IsAlive, "which got away");
            var dx = runner.X - fighter.spotX;
            var dy = runner.Y - fighter.spotY;
            Assert.True(dx * dx + dy * dy > 2100.0f * 2100.0f, "beyond the leash from where it left its lane");
            Assert.True(farthest < 2200.0f, "it went no farther than its leash and its reach: " + farthest);
            Assert.True(sim.RunUntil(() => walker.X == 3000.0f, 60.0f), "then it walks on to its lane's end, at " + walker.X);
        }

        /// <summary>A walker fighting a brute it cannot beat pulls back below 30% of its life, walks back to its lane's start with moves, and stands there, still pulled back below 70%.</summary>
        [Fact]
        public void AWalkerPullsBackWhenHurt()
        {
            var sim = Start();
            var walker = Unit.Create(Walker, Player.One, 0.0f, 0.0f, Game.Instance);
            var brain = walker.Add(new CountingWalker(Lane(0.0f, 3000.0f, 0.0f)));
            Unit.Create(Brute, Player.Two, 1500.0f, 0.0f, Game.Instance);

            Assert.True(sim.RunUntil(() => brain.Fighter.PullingBack, 60.0f), "it pulls back");
            Assert.True(walker.Life < 120.0f, "below 30% of 400: " + walker.Life);
            Assert.True(sim.RunUntil(() => walker.X <= 64.0f, 30.0f), "it walks back to its lane's start, at " + walker.X);
            sim.Run(5.0f);
            Assert.True(walker.IsAlive, "it lives");
            Assert.True(walker.X <= 64.0f, "and stands there: " + walker.X);
            Assert.True(brain.Fighter.PullingBack, "still back, below 70%");
        }

        /// <summary>Four walkers of each side walk lanes toward each other and fight, never pulling back, until one side falls; the same seed gives the same fight.</summary>
        [Fact]
        public void TwoLanesFightToAWinner()
        {
            var first = Lanes(5);
            var again = Lanes(5);
            Assert.Equal(first, again);
        }

        /// <summary>Plays four walkers of player 1 east and four of player 2 west on lanes 150 apart, until one side is gone: the winner's slot, its walkers left and the time.</summary>
        private static (int Winner, int Left, float Time) Lanes(int seed)
        {
            var sim = Simulation.Start(seed);
            sim.SetNumbers(Walker, new UnitNumbers { Life = 300.0f, MoveSpeed = 270.0f, AttackRange = 100.0f, AttackCooldown = 1.2f, Damage = 10, DamageDice = 2, DamageSides = 6, AcquisitionRange = 500.0f });
            for (var i = 0; i < 4; i++)
            {
                var y = i * 150.0f;
                // With no regeneration in the simulation, walkers that pull back never come back, so they fight to the end.
                Unit.Create(Walker, Player.One, 0.0f, y, Game.Instance).Add(new LaneWalker(Lane(0.0f, 3000.0f, y)) { PullBack = { Below = 0.0f, Outnumbered = 0.0f } });
                Unit.Create(Walker, Player.Two, 3000.0f, y, Game.Instance).Add(new LaneWalker(Lane(3000.0f, 0.0f, y)) { PullBack = { Below = 0.0f, Outnumbered = 0.0f } });
            }

            Assert.True(sim.RunUntil(() => sim.Alive(Player.One) == 0 || sim.Alive(Player.Two) == 0, 300.0f), "one side falls within 300 s: " + sim.Alive(Player.One) + " to " + sim.Alive(Player.Two));
            var west = sim.Alive(Player.One);
            var east = sim.Alive(Player.Two);
            return (west > 0 ? 1 : 2, west + east, sim.Time);
        }
    }
}
