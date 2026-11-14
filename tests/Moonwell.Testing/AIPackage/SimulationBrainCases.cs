using System.Collections.Generic;
using Moonwell.AI;

namespace Moonwell.Testing
{
    /// <summary>A unit brain that orders an attack on the nearest living foe of its list at each think, and counts its thinks and how often that foe changed.</summary>
    public sealed class ChargeBrain : UnitBrain
    {
        /// <summary>The units it may attack.</summary>
        public List<Unit> Foes = new List<Unit>();

        /// <summary>How many times it thought.</summary>
        public int Thinks;

        /// <summary>How many times the nearest foe was another than at the think before.</summary>
        public int Picks;

        private Unit? lastPick;

        protected override void Think()
        {
            Thinks++;
            Unit? nearest = null;
            var best = 0.0f;
            for (var i = 0; i < Foes.Count; i++)
            {
                var foe = Foes[i];
                if (!foe.IsAlive)
                {
                    continue;
                }

                var dx = foe.X - Unit.X;
                var dy = foe.Y - Unit.Y;
                var squared = dx * dx + dy * dy;
                if (nearest == null || squared < best)
                {
                    nearest = foe;
                    best = squared;
                }
            }

            if (nearest == null)
            {
                return;
            }

            if (nearest != lastPick)
            {
                Picks++;
                lastPick = nearest;
            }

            OrderOn("attack", nearest);
        }
    }

    /// <summary>Unit brains in the fast simulation (decisions 300 and 312): brains fight a fight out to a winner, and their repeated orders never reach the game.</summary>
    public static class SimulationBrainCases
    {
        private static readonly int Knight = ObjectTypes.FourCC("s0kn");
        private static readonly int Grunt = ObjectTypes.FourCC("s0gr");

        /// <summary>Three knights whose brains think every 0.25 s and order an attack on the nearest grunt beat three grunts with no brain; each knight gets one order for each grunt it picks, not one for each think.</summary>
        public static void BrainsWin(FakeWorld world)
        {
            var sim = Simulation.Start();
            // A knight swings 40 every 0.5 s and kills a grunt's 100 in 3 swings (60, 20, then -20) over 1 s; a grunt swings
            // 5 every 1 s at 400 life. Knights acquire nothing themselves, so only their brains send them.
            sim.SetNumbers(Knight, new UnitNumbers { Life = 400.0f, MoveSpeed = 300.0f, AttackRange = 100.0f, AttackCooldown = 0.5f, Damage = 40 });
            sim.SetNumbers(Grunt, new UnitNumbers { Life = 100.0f, MoveSpeed = 200.0f, AttackRange = 100.0f, AttackCooldown = 1.0f, Damage = 5, AcquisitionRange = 400.0f });
            var knights = new List<Unit>();
            var grunts = new List<Unit>();
            for (var i = 0; i < 3; i++)
            {
                // Rows 300 apart: a knight's own grunt is 250 away, the next row's sqrt(250^2 + 300^2) = 390.5.
                knights.Add(Unit.Create(Knight, Player.One, 0.0f, i * 300.0f, Game.Instance));
                grunts.Add(Unit.Create(Grunt, Player.Two, 250.0f, i * 300.0f, Game.Instance));
            }

            var brains = new List<ChargeBrain>();
            for (var i = 0; i < 3; i++)
            {
                brains.Add(knights[i].Add(new ChargeBrain { ThinkEvery = 0.25f, Foes = grunts }));
            }

            Expect.True(sim.RunUntil(() => sim.Alive(Player.One) == 0 || sim.Alive(Player.Two) == 0, 30.0f), "one side falls within 30 s");
            Expect.Equal(3, sim.Alive(Player.One), "every knight lives");
            Expect.Equal(0, sim.Alive(Player.Two), "every grunt fell");
            for (var i = 0; i < 3; i++)
            {
                var brain = brains[i];
                Expect.Equal(brain.Picks, sim.OrdersOf(knights[i]), "knight " + i + " got one order for each pick, its repeats dropped");
                Expect.True(brain.Picks >= 1 && brain.Picks <= 3, "knight " + i + " picked at most the 3 grunts, not " + brain.Picks);
                Expect.True(brain.Thinks > brain.Picks, "knight " + i + " thought more often than it was ordered: " + brain.Thinks + " thinks");
            }
        }
    }
}
