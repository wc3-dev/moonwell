using System;
using System.Text;
using Moonwell.Procgen;

namespace Moonwell.Testing
{
    /// <summary>Cases for the procgen package's terrain generator (decision 269): a plan from a seed, its rules, and the ground it makes in the fake game.</summary>
    public static class TerrainGeneratorCases
    {
        /// <summary>The corners across and up the cases' area.</summary>
        private const int Side = 21;

        /// <summary>A game component that holds what a case makes, ended by the case.</summary>
        private sealed class Holder : GameComponent
        {
        }

        /// <summary>An area of 21 by 21 corners from (0, 0).</summary>
        private static Bounds Area()
        {
            return Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(2560f, 2560f));
        }

        /// <summary>Steep hills with no fade, so every rule has corners to fit.</summary>
        private static TerrainGenerator Steep(int seed)
        {
            return new TerrainGenerator(seed) { Lowest = 0f, Highest = 600f, FeatureSize = 1024f, EdgeFade = 0f };
        }

        private static FakeTerrainWorld Flat()
        {
            var fake = new FakeTerrainWorld();
            var text = new StringBuilder();
            for (var k = 0; k < 24 * 24; k++)
            {
                text.Append(GridText.Corner(0f, 2));
            }

            Terrain.Install(fake, 24, 24, 0f, 0f, text.ToString());
            return fake;
        }

        /// <summary>The same seed, settings and area give the same plan; the area elsewhere, or another seed, gives another.</summary>
        public static void SameSeed(FakeWorld world)
        {
            var first = Hills(11).Plan(Area());
            var again = Hills(11).Plan(Area());
            var elsewhere = Hills(11).Plan(Bounds.FromCorners(new Vector2(-5000f, 300f), new Vector2(-2440f, 2860f)));
            var other = Hills(12).Plan(Area());
            Expect.Equal(Side, first.Columns, "a corner every 128 across");
            Expect.Equal(Side, first.Rows, "and up");
            Expect.Near(-5000f, elsewhere.X, "the plan starts at the area's lower left");
            Expect.Near(300f, elsewhere.Y, "its y");
            var same = true;
            var moved = false;
            var differs = false;
            for (var k = 0; k < Side * Side; k++)
            {
                same = same && first.Heights.Cells[k] == again.Heights.Cells[k] && first.Tiles[k] == again.Tiles[k] && first.Blocked[k] == again.Blocked[k];
                moved = moved || first.Heights.Cells[k] != elsewhere.Heights.Cells[k];
                differs = differs || first.Heights.Cells[k] != other.Heights.Cells[k];
            }

            Expect.True(same, "the same seed and area give the same plan");
            Expect.True(moved, "the area elsewhere gives other ground");
            Expect.True(differs, "another seed gives another");
        }

        /// <summary>Two neighbouring plans with no fade give the same height, tile and blocked corner on their shared edge, a level place across it too; an area moved by a corner is the same ground moved.</summary>
        public static void Seams(FakeWorld world)
        {
            var generator = Hills(21);
            generator.EdgeFade = 0f;
            generator.Level(-2560f, 1280f, 300f);
            var left = generator.Plan(Bounds.FromCorners(new Vector2(-5120f, 0f), new Vector2(-2560f, 2560f)));
            var right = generator.Plan(Bounds.FromCorners(new Vector2(-2560f, 0f), new Vector2(0f, 2560f)));
            var below = generator.Plan(Bounds.FromCorners(new Vector2(-2560f, -2560f), new Vector2(0f, 0f)));
            var mismatches = 0;
            for (var k = 0; k < Side; k++)
            {
                mismatches += Same(left, Side - 1, k, right, 0, k) ? 0 : 1;
                mismatches += Same(below, k, Side - 1, right, k, 0) ? 0 : 1;
            }

            Expect.Equal(0, mismatches, "the shared edges match");
            Expect.Near(right.Heights[0, 10], right.Heights[2, 10], "the level place reaches the right plan");
            Expect.Near(right.Heights[0, 10], left.Heights[Side - 3, 10], "and the left, at the same height");
            Expect.True(right.Blocked[10 * Side] == Pathing.None && left.Blocked[10 * Side + Side - 1] == Pathing.None, "and is not blocked on either side");

            var shifted = generator.Plan(Bounds.FromCorners(new Vector2(-2432f, 0f), new Vector2(128f, 2560f)));
            var along = 0;
            for (var row = 0; row < Side; row++)
            {
                for (var column = 0; column < Side - 1; column++)
                {
                    along += Same(right, column + 1, row, shifted, column, row) ? 0 : 1;
                }
            }

            Expect.Equal(0, along, "an area a corner to the right is the same ground a corner along");
        }

        /// <summary>One area planned whole and as four quarters gives the same corners.</summary>
        public static void Quarters(FakeWorld world)
        {
            var generator = Hills(4);
            generator.EdgeFade = 0f;
            generator.Level(1300f, 1250f, 400f);
            var whole = generator.Plan(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(5120f, 5120f)));
            var differ = 0;
            for (var quarter = 0; quarter < 4; quarter++)
            {
                var offsetColumn = quarter % 2 * (Side - 1);
                var offsetRow = quarter / 2 * (Side - 1);
                var part = generator.Plan(Bounds.FromCorners(new Vector2(offsetColumn * 128f, offsetRow * 128f), new Vector2(offsetColumn * 128f + 2560f, offsetRow * 128f + 2560f)));
                for (var row = 0; row < Side; row++)
                {
                    for (var column = 0; column < Side; column++)
                    {
                        differ += Same(whole, offsetColumn + column, offsetRow + row, part, column, row) ? 0 : 1;
                    }
                }
            }

            Expect.Equal(41, whole.Columns, "the whole is 41 corners across");
            Expect.Equal(0, differ, "every quarter's corners are the whole's");
        }

        /// <summary>Whether a corner of one plan has the same height, tile and blocked pathing as a corner of another.</summary>
        private static bool Same(TerrainPlan first, int firstColumn, int firstRow, TerrainPlan second, int secondColumn, int secondRow)
        {
            var a = firstRow * first.Columns + firstColumn;
            var b = secondRow * second.Columns + secondColumn;
            return first.Heights.Cells[a] == second.Heights.Cells[b] && first.Tiles[a] == second.Tiles[b] && first.Blocked[a] == second.Blocked[b];
        }

        /// <summary>Heights stay from Lowest to Highest and fill most of it, with a fade or not; with a fade the edge is the ground as it was and the middle is as without it.</summary>
        public static void RangeAndFade(FakeWorld world)
        {
            var generator = new TerrainGenerator(3) { Lowest = -100f, Highest = 400f, EdgeFade = 0f };
            var plan = generator.Plan(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(6400f, 6400f)));
            Expect.True(plan.Heights.Lowest() >= -100f && plan.Heights.Highest() <= 400f, "inside the range: " + plan.Heights.Lowest() + " to " + plan.Heights.Highest());
            Expect.True(plan.Heights.Highest() - plan.Heights.Lowest() > 250f, "over half of it in a large area: " + plan.Heights.Lowest() + " to " + plan.Heights.Highest());
            plan = generator.Plan(Area());
            generator.EdgeFade = 512f;
            var faded = generator.Plan(Area());
            var edge = 0f;
            for (var k = 0; k < Side; k++)
            {
                edge = Math.Max(edge, Math.Abs(faded.Heights[k, 0]));
                edge = Math.Max(edge, Math.Abs(faded.Heights[k, Side - 1]));
                edge = Math.Max(edge, Math.Abs(faded.Heights[0, k]));
                edge = Math.Max(edge, Math.Abs(faded.Heights[Side - 1, k]));
            }

            Expect.Near(0f, edge, "the edge is the ground as it was");
            Expect.Near(plan.Heights[10, 10], faded.Heights[10, 10], "the middle is as without the fade");
            Expect.True(Math.Abs(faded.Heights[1, 10]) <= Math.Abs(plan.Heights[1, 10]), "a corner in the fade is nearer the ground as it was");
        }

        /// <summary>A tile rule paints the corners in its height and slope ranges, a later rule paints over an earlier one, and a cover below 1 paints some of them.</summary>
        public static void Paint(FakeWorld world)
        {
            var generator = Steep(8);
            generator.Level(1280f, 1280f, 300f);
            generator.Paint(Tile.LordaeronSummerGrass);
            generator.Paint(Tile.LordaeronSummerDirt, fromHeight: 0.5f);
            generator.Paint(Tile.LordaeronSummerRock, fromSlope: 20f);
            var plan = generator.Plan(Area());
            var grass = 0;
            var dirt = 0;
            var rock = 0;
            var wrong = 0;
            for (var k = 0; k < Side * Side; k++)
            {
                var tile = plan.Tiles[k];
                var high = plan.Heights.Cells[k] / 600f >= 0.5f;
                grass += tile == Tile.LordaeronSummerGrass ? 1 : 0;
                dirt += tile == Tile.LordaeronSummerDirt ? 1 : 0;
                rock += tile == Tile.LordaeronSummerRock ? 1 : 0;
                if (tile != Tile.LordaeronSummerRock && tile != (high ? Tile.LordaeronSummerDirt : Tile.LordaeronSummerGrass))
                {
                    wrong++;
                }
            }

            Expect.Equal(Side * Side, grass + dirt + rock, "every corner has a tile");
            Expect.Equal(0, wrong, "dirt on the upper half and grass on the lower, where it is not rock");
            Expect.True(grass > 0 && dirt > 0 && rock > 0, "all three are there: " + grass + ", " + dirt + ", " + rock);
            Expect.True(plan.Tiles[10 * Side + 10] != Tile.LordaeronSummerRock && plan.Tiles[10 * Side + 11] != Tile.LordaeronSummerRock, "a level place is not steep");

            var patchy = Steep(8);
            patchy.Paint(Tile.LordaeronSummerGrass);
            patchy.Paint(Tile.LordaeronSummerDirt, cover: 0.5f);
            patchy.Paint(Tile.LordaeronSummerRock, cover: 0f);
            var patches = patchy.Plan(Area());
            var covered = 0;
            var none = 0;
            for (var k = 0; k < Side * Side; k++)
            {
                covered += patches.Tiles[k] == Tile.LordaeronSummerDirt ? 1 : 0;
                none += patches.Tiles[k] == Tile.LordaeronSummerRock ? 1 : 0;
            }

            Expect.True(covered > Side * Side / 5 && covered < Side * Side * 4 / 5, "a cover of a half paints some corners and leaves some: " + covered);
            Expect.Equal(0, none, "a cover of 0 paints none");
        }

        /// <summary>Block rules take pathing from the corners in their ranges and add up; a level place is level at its centre's height, blends back past its radius, and is never blocked.</summary>
        public static void BlockAndLevel(FakeWorld world)
        {
            var generator = Steep(8);
            generator.Block(Pathing.Walk | Pathing.Build, fromSlope: 20f);
            generator.Block(Pathing.Fly, fromHeight: 0.7f);
            var open = generator.Plan(Area());
            var walls = 0;
            var free = 0;
            var high = 0;
            var wrong = 0;
            for (var k = 0; k < Side * Side; k++)
            {
                walls += (open.Blocked[k] & Pathing.Walk) != 0 ? 1 : 0;
                free += open.Blocked[k] == Pathing.None ? 1 : 0;
                var fits = open.Heights.Cells[k] / 600f >= 0.7f;
                high += fits ? 1 : 0;
                wrong += fits != ((open.Blocked[k] & Pathing.Fly) != 0) ? 1 : 0;
            }

            Expect.True(walls > 0 && free > 0, "steep corners are blocked and gentle ones are not: " + walls + ", " + free);
            Expect.True(high > 0 && wrong == 0, "the high corners, and only they, fit the height rule: " + high + ", " + wrong);

            generator.Level(1280f, 1280f, 300f);
            var plan = generator.Plan(Area());
            var centre = open.Heights[10, 10];
            Expect.Near(centre, plan.Heights[10, 10], "level at its centre's height");
            Expect.Near(centre, plan.Heights[12, 10], "out to the radius");
            Expect.Near(centre, plan.Heights[10, 8], "on every side");
            Expect.Equal(true, plan.Blocked[10 * Side + 10] == Pathing.None && plan.Blocked[10 * Side + 12] == Pathing.None && plan.Blocked[8 * Side + 10] == Pathing.None, "never blocked");
            Expect.True(Math.Abs(plan.Heights[13, 10] - centre) <= Math.Abs(open.Heights[13, 10] - centre) + 0.001f, "the rim is nearer the level than the hills were");
            Expect.Near(open.Heights[0, 0], plan.Heights[0, 0], "a far corner is as it was");
        }

        /// <summary>Apply makes a plan's heights, tiles and blocked corners in the game, in the scope given; its end takes them back; an empty plan makes nothing; a generated plan's heights are the ground's.</summary>
        public static void Apply(FakeWorld world)
        {
            var fake = Flat();
            var empty = new TerrainPlan(0f, 0f, 3, 3);
            empty.Apply(Game.Instance);
            Expect.Equal(0, fake.Craters.Count + fake.Paints + fake.PathSets, "an empty plan asks the game nothing");

            var plan = new TerrainPlan(1280f, 1280f, 2, 2);
            plan.Heights[0, 0] = 50f;
            plan.Heights[1, 1] = -20f;
            plan.Tiles[1] = Tile.LordaeronSummerGrass;
            plan.Blocked[2] = Pathing.Walk;
            var holder = Game.Instance.Add(new Holder());
            plan.Apply(holder);
            Expect.Near(50f, Terrain.Height(1280f, 1280f), "the first corner raised");
            Expect.Near(-20f, Terrain.Height(1408f, 1408f), "the last lowered");
            Expect.Equal(2, fake.Craters.Count, "a crater a corner moved");
            Expect.Equal(Tile.LordaeronSummerGrass, Terrain.Tile(1408f, 1280f), "the second corner painted");
            Expect.Equal(1, fake.Paints, "and no other");
            Expect.True(!Terrain.IsWalkable(1296f, 1424f), "walking taken around the third corner");
            Expect.True(Terrain.IsWalkable(1296f, 1296f), "and left around the first");
            holder.End();
            Expect.Near(0f, Terrain.Height(1280f, 1280f), "flat again");
            Expect.Equal(Tile.LordaeronSummerDirt, Terrain.Tile(1408f, 1280f), "the tile before");
            Expect.True(Terrain.IsWalkable(1296f, 1424f), "walkable again");

            var hills = new TerrainGenerator(5).Plan(Bounds.FromCorners(new Vector2(256f, 256f), new Vector2(2304f, 2304f)));
            hills.Apply(Game.Instance);
            Expect.Near(hills.Heights[8, 8], Terrain.Height(256f + 8 * 128f, 256f + 8 * 128f), "a generated plan's height is the ground's");
            Expect.Near(0f, Terrain.Height(256f, 1280f), "its faded edge meets the ground as it was");
        }

        /// <summary>A feature size of 0, no layers, a tile of 0, a level radius of 0 and a plan below 1 by 1 each throw.</summary>
        public static void Refused(FakeWorld world)
        {
            Expect.Throws<ArgumentException>(() => new TerrainGenerator(1) { FeatureSize = 0f }.Plan(Area()), "a feature size of 0");
            Expect.Throws<ArgumentException>(() => new TerrainGenerator(1) { Layers = 0 }.Plan(Area()), "no layers");
            Expect.Throws<ArgumentException>(() => new TerrainGenerator(1).Paint(0), "a tile of 0");
            Expect.Throws<ArgumentException>(() => new TerrainGenerator(1).Level(0f, 0f, 0f), "a level radius of 0");
            Expect.Throws<ArgumentException>(() => new TerrainPlan(0f, 0f, 0, 1), "a plan with no columns");
            var point = new TerrainGenerator(1).Plan(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(10f, 10f)));
            Expect.Equal(1, point.Columns * point.Rows, "an area smaller than a tile is one corner");
            Expect.Near(0f, point.Heights[0, 0], "which the edge fade leaves as it was");
        }

        /// <summary>Hills with a rule of each kind, for the cases that compare plans.</summary>
        private static TerrainGenerator Hills(int seed)
        {
            var generator = new TerrainGenerator(seed) { Lowest = 0f, Highest = 500f, FeatureSize = 1024f };
            generator.Paint(Tile.LordaeronSummerGrass);
            generator.Paint(Tile.LordaeronSummerDirt, fromHeight: 0.4f, cover: 0.6f);
            generator.Paint(Tile.LordaeronSummerRock, fromSlope: 25f);
            generator.Block(Pathing.Walk | Pathing.Build, fromSlope: 25f);
            return generator;
        }
    }
}
