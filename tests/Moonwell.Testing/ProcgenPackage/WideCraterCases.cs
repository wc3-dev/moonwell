using System;
using System.Text;
using Moonwell.Procgen;

namespace Moonwell.Testing
{
    /// <summary>
    /// Cases for areas past 20,000 corners, raised by wide craters fitted to the plan (decision 343), and for a making's
    /// pace and its report of a slow step (decision 344).
    /// </summary>
    public static class WideCraterCases
    {
        /// <summary>The corners across and up the big area: 22,500 in all.</summary>
        private const int Side = 150;

        /// <summary>A game component that holds what a case makes, ended by the case.</summary>
        private sealed class Holder : GameComponent
        {
        }

        /// <summary>A flat map of 160 by 160 corners from (0, 0).</summary>
        private static FakeTerrainWorld Big()
        {
            var fake = new FakeTerrainWorld();
            var text = new StringBuilder();
            for (var k = 0; k < 160 * 160; k++)
            {
                text.Append(GridText.Corner(0f, 2));
            }

            Terrain.Install(fake, 160, 160, 0f, 0f, text.ToString());
            return fake;
        }

        private static Bounds Area()
        {
            return Bounds.FromCorners(new Vector2(0f, 0f), new Vector2((Side - 1) * 128f, (Side - 1) * 128f));
        }

        /// <summary>SnowyForest's hills, with a level place in the middle.</summary>
        private static TerrainGenerator Land()
        {
            var land = new TerrainGenerator(1234) { Lowest = -100f, Highest = 250f };
            land.Paint(Tile.LordaeronSummerGrass);
            land.Paint(Tile.LordaeronSummerRock, fromSlope: 22f);
            land.Level(75 * 128f, 75 * 128f, 512f);
            return land;
        }

        /// <summary>
        /// Past 20,000 corners a plan takes wide craters: its heights are what they make, close to the hills' own (planned
        /// in two halves, as pieces of the whole, which never take them), but for the level place, exact, and the area's edge,
        /// where no crater reaches.
        /// </summary>
        public static void Plan(FakeWorld world)
        {
            Big();
            var land = Land();
            var plan = land.Plan(Area());
            Expect.True(plan.WideDepths != null, "past 20,000 corners the plan takes wide craters");
            var left = land.Plan(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(74 * 128f, (Side - 1) * 128f)), Area());
            var right = land.Plan(Bounds.FromCorners(new Vector2(75 * 128f, 0f), new Vector2((Side - 1) * 128f, (Side - 1) * 128f)), Area());
            Expect.True(left.WideDepths == null && right.WideDepths == null, "a piece of a whole never takes them");
            var squares = 0.0;
            var worst = 0f;
            var differ = 0;
            for (var row = 0; row < Side; row++)
            {
                for (var column = 0; column < Side; column++)
                {
                    var exact = column < 75 ? left.Heights[column, row] : right.Heights[column - 75, row];
                    var off = Math.Abs(plan.Heights[column, row] - exact);
                    squares += off * off;
                    worst = Math.Max(worst, off);
                    differ += off > 0.01f ? 1 : 0;
                }
            }

            var typical = (float)Math.Sqrt(squares / (Side * Side));
            Expect.True(typical < 25f, "the heights close to the hills': typically " + typical + " off, worst " + worst);
            Expect.True(differ > Side * Side / 2, "though not exactly: " + differ + " corners differ");
            Expect.Near(0f, plan.Heights[75, 75], "the level place is level at the ground as it was");
            Expect.Near(0f, plan.Heights[75 + 3, 75], "across it");
            Expect.Near(0f, plan.Heights[0, 60], "the area's edge is the ground as it was");
            Expect.Near(0f, plan.Heights[Side - 1, 90], "every edge");
        }

        /// <summary>
        /// Made in steps, a big area asks far fewer craters than corners, and Moonwell's ground is the plan's at every corner,
        /// nothing past the area's edge; ending the scope stops every crater and leaves the ground exactly as before.
        /// </summary>
        public static void Make(FakeWorld world)
        {
            var fake = Big();
            var land = Land();
            var plan = land.Plan(Area());
            var holder = Game.Instance.Add(new Holder());
            var making = land.Make(Area(), holder, msPerStep: 50f);
            var ticks = 0;
            while (!making.IsDone && ticks < 5000)
            {
                world.Advance(0.02f);
                ticks++;
            }

            Expect.True(making.IsDone, "the ground is made");
            var wide = 0;
            foreach (var crater in fake.Craters)
            {
                wide += crater[2] > 128f ? 1 : 0;
            }

            Expect.True(fake.Craters.Count < Side * Side / 5, "far fewer craters than the 22,500 corners: " + fake.Craters.Count + ", " + wide + " of them wide");
            Expect.True(wide > 1000, "most of them wide: " + wide);
            var worst = 0f;
            for (var row = 0; row < Side; row++)
            {
                for (var column = 0; column < Side; column++)
                {
                    worst = Math.Max(worst, Math.Abs(Terrain.Height(column * 128f, row * 128f) - plan.Heights[column, row]));
                }
            }

            Expect.True(worst < 0.05f, "the ground is the plan's at every corner, worst " + worst + " off");
            Expect.Near(0f, Terrain.Height(Side * 128f, 75 * 128f), "nothing past the area's edge");
            holder.End();
            Expect.Equal(fake.Craters.Count, fake.Stopped.Count, "the end stops every crater");
            var moved = 0;
            for (var row = 0; row < 160; row++)
            {
                for (var column = 0; column < 160; column++)
                {
                    moved += Math.Abs(Terrain.Height(column * 128f, row * 128f)) < 0.001f ? 0 : 1;
                }
            }

            Expect.Equal(0, moved, "and the ground is as before, within a thousandth (raises that overlap add up in another order than they come off)");
        }

        /// <summary>Applying a big plan makes its ground at once, a change made to its heights before too.</summary>
        public static void Apply(FakeWorld world)
        {
            Big();
            var plan = Land().Plan(Area());
            plan.Heights[40, 40] += 100f;
            var holder = Game.Instance.Add(new Holder());
            plan.Apply(holder);
            var worst = 0f;
            for (var row = 0; row < Side; row++)
            {
                for (var column = 0; column < Side; column++)
                {
                    worst = Math.Max(worst, Math.Abs(Terrain.Height(column * 128f, row * 128f) - plan.Heights[column, row]));
                }
            }

            Expect.True(worst < 0.05f, "the ground is the plan's at every corner, the changed one too, worst " + worst + " off");
            holder.End();
            Expect.Near(0f, Terrain.Height(40 * 128f, 40 * 128f), "ending the scope takes it back");
        }

        /// <summary>A step makes fewer corners' craters the more craters are held by then, so its time stays near its budget (decision 344).</summary>
        public static void Paced(FakeWorld world)
        {
            var fake = Big();
            var land = new TerrainGenerator(7) { Lowest = 50f, Highest = 200f, EdgeFade = 0f };
            var area = Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(119 * 128f, 119 * 128f));
            var making = land.Make(area, Game.Instance, msPerStep: 1f);
            var counts = new System.Collections.Generic.List<int>();
            var before = 0;
            var ticks = 0;
            while (!making.IsDone && ticks < 20000)
            {
                world.Advance(0.02f);
                ticks++;
                if (fake.Craters.Count > before)
                {
                    counts.Add(fake.Craters.Count - before);
                    before = fake.Craters.Count;
                }
            }

            Expect.Equal(120 * 120, fake.Craters.Count, "a crater a corner below 20,000 corners");
            Expect.True(counts.Count > 10, "over many steps: " + counts.Count);
            var early = counts[1];
            var late = counts[counts.Count - 2];
            Expect.True(late * 3 < early, "a late step makes fewer: " + early + " early, " + late + " late");
        }

        /// <summary>A development build reports, once, a step of a making that took over a second of real time, with its step and the craters made.</summary>
        public static void SlowReported(FakeWorld world)
        {
            var fake = Big();
#if DEBUG
            var debug = FakeDebugWorld.Install(world);
            fake.Asked = () => debug.ClockSeconds += 0.01f;
#endif
            var land = new TerrainGenerator(7) { Lowest = 50f, Highest = 200f, EdgeFade = 0f };
            var area = Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(40 * 128f, 40 * 128f));
            var before = world.Printed.Count;
            var making = land.Make(area, Game.Instance, msPerStep: 4f);
            var ticks = 0;
            while (!making.IsDone && ticks < 5000)
            {
                world.Advance(0.02f);
                ticks++;
            }

            Expect.True(making.IsDone, "the making goes on");
#if DEBUG
            Expect.Equal(before + 1, world.Printed.Count, "one report, for the first slow step");
            var report = world.Printed[before];
            Expect.True(report.Contains("\"Heights\"") && report.Contains("craters made") && report.Contains("Grow"), "it names the step, the craters and the way out: " + report);
#else
            Expect.Equal(before, world.Printed.Count, "a release build reports nothing");
#endif
        }
    }
}
