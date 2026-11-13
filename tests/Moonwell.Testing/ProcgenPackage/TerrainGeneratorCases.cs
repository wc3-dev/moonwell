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

        internal static FakeTerrainWorld Flat()
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

        /// <summary>Block rules take pathing from the corners in their ranges and add up; a level place is level at the ground as it was, blends back past its radius, and is never blocked.</summary>
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
            var centre = 0f;
            Expect.Near(centre, plan.Heights[10, 10], "level at the ground as it was");
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

        /// <summary>
        /// Made in steps, the ground is the one-go plan's, corner for corner: heights, tiles and blocked pathing. The share
        /// never goes back and is 1 only once made, Done runs once, and the steps come in order.
        /// </summary>
        public static void MakeInSteps(FakeWorld world)
        {
            Flat();
            var generator = Hills(8);
            generator.Level(1200f, 1300f, 300f);
            var holder = Game.Instance.Add(new Holder());
            var making = generator.Make(Area(), holder, msPerStep: 0.2f);
            var dones = 0;
            making.Done += () => dones++;
            Expect.Near(0f, making.Progress, "nothing made before the first step");
            Expect.Near(0f, Terrain.Height(1280f, 1280f), "nor any ground");
            var steps = making.Step;
            var step = making.Step;
            var last = 0f;
            var backwards = 0;
            var early = 0;
            var ticks = 0;
            while (!making.IsDone && ticks < 20000)
            {
                world.Advance(0.02f);
                ticks++;
                backwards += making.Progress < last ? 1 : 0;
                early += making.Progress >= 1f && !making.IsDone ? 1 : 0;
                last = making.Progress;
                if (making.Step != step)
                {
                    step = making.Step;
                    steps += "," + step;
                }
            }

            Expect.True(making.IsDone, "the ground is made");
            Expect.True(ticks > 50, "over many steps");
            Expect.Equal(0, backwards, "the share never goes back");
            Expect.Equal(0, early, "and is 1 only once made");
            Expect.Near(1f, making.Progress, "1 once made");
            Expect.Equal(1, dones, "Done runs");
            Expect.Equal("Heights,Tiles,Blocked", steps, "the steps in order");
            world.Advance(1f);
            Expect.Equal(1, dones, "once");

            var stepped = Ground();
            holder.End();
            Expect.Near(0f, Terrain.Height(1280f, 1280f), "ending the scope takes the ground back");
            generator.Plan(Area()).Apply(Game.Instance);
            var whole = Ground();
            var differ = 0;
            var moved = 0;
            for (var k = 0; k < whole.Length; k++)
            {
                differ += whole[k] == stepped[k] ? 0 : 1;
                moved += k % 4 == 0 && whole[k] != 0f ? 1 : 0;
            }

            Expect.Equal(0, differ, "the ground made in steps is the one-go plan's, corner for corner");
            Expect.True(moved > Side * Side / 2, "ground that has hills");
        }

        /// <summary>Ending the scope stops a making: no more steps, no Done, and the ground made so far taken back.</summary>
        public static void MakeStopped(FakeWorld world)
        {
            Flat();
            var holder = Game.Instance.Add(new Holder());
            var making = Hills(8).Make(Area(), holder, msPerStep: 0.5f);
            var dones = 0;
            making.Done += () => dones++;
            for (var k = 0; k < 1000 && making.Step != "Tiles"; k++)
            {
                world.Advance(0.02f);
            }

            var share = making.Progress;
            Expect.True(share > 0f && share < 1f, "stopped part made, in the tiles");
            Expect.True(Moved() > 0, "with hills made");
            holder.End();
            world.Advance(5f);
            Expect.True(!making.IsDone && dones == 0, "stopped, with no Done");
            Expect.Near(share, making.Progress, "where it stopped");
            Expect.Equal(0, Moved(), "the ground made so far taken back");
        }

        /// <summary>The corners of the cases' area the ground is moved at.</summary>
        private static int Moved()
        {
            var moved = 0;
            var ground = Ground();
            for (var k = 0; k < ground.Length; k += 4)
            {
                moved += ground[k] != 0f ? 1 : 0;
            }

            return moved;
        }

        /// <summary>The ground at each corner of the cases' area: its height, its tile, and whether units can walk and build by it.</summary>
        internal static float[] Ground()
        {
            var ground = new float[Side * Side * 4];
            for (var k = 0; k < Side * Side; k++)
            {
                var x = k % Side * 128f;
                var y = k / Side * 128f;
                ground[k * 4] = Terrain.Height(x, y);
                ground[k * 4 + 1] = Terrain.Tile(x, y);
                ground[k * 4 + 2] = Terrain.IsWalkable(x + 16f, y + 16f) ? 1f : 0f;
                ground[k * 4 + 3] = Terrain.IsBuildable(x - 48f, y - 48f) ? 1f : 0f;
            }

            return ground;
        }

        /// <summary>A feature size of 0, no layers, a tile of 0, a level radius of 0 a plan below 1 by 1 and a making of 0 ms a step each throw.</summary>
        public static void Refused(FakeWorld world)
        {
            Expect.Throws<ArgumentException>(() => new TerrainGenerator(1) { FeatureSize = 0f }.Plan(Area()), "a feature size of 0");
            Expect.Throws<ArgumentException>(() => new TerrainGenerator(1) { Layers = 0 }.Plan(Area()), "no layers");
            Expect.Throws<ArgumentException>(() => new TerrainGenerator(1).Make(Area(), Game.Instance, msPerStep: 0f), "a making of 0 ms a step");
            Expect.Throws<ArgumentException>(() => new TerrainGenerator(1) { Layers = 0 }.Make(Area(), Game.Instance), "a making with no layers");
            Expect.Throws<ArgumentException>(() => new TerrainGenerator(1).Paint(0), "a tile of 0");
            Expect.Throws<ArgumentException>(() => new TerrainGenerator(1).Level(0f, 0f, 0f), "a level radius of 0");
            Expect.Throws<ArgumentException>(() => new TerrainGenerator(1).Forest(0), "a tree type of 0");
            Expect.Throws<ArgumentException>(() => new TerrainGenerator(1).Scatter(Tree, variations: 0), "no variations");
            Expect.Throws<ArgumentException>(() => new TerrainGenerator(1).Wall(Tree, spacing: 0f), "a wall spacing of 0");
            Expect.Throws<ArgumentException>(() => new TerrainGenerator(1).Wall(Tree, width: 0f), "a wall width of 0");
            Expect.Throws<ArgumentException>(() => new TerrainPlan(0f, 0f, 0, 1), "a plan with no columns");
            var point = new TerrainGenerator(1).Plan(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(10f, 10f)));
            Expect.Equal(1, point.Columns * point.Rows, "an area smaller than a tile is one corner");
            Expect.Near(0f, point.Heights[0, 0], "which the edge fade leaves as it was");
        }

        /// <summary>A tree type for the cases: Summer Tree Wall.</summary>
        private const int Tree = 0x4C546C74;

        /// <summary>Another, Ashenvale Tree Wall.</summary>
        private const int OtherTree = 0x41547472;

        /// <summary>The trees of a plan.</summary>
        private static int TreesIn(TerrainPlan plan)
        {
            var count = 0;
            foreach (var type in plan.Trees)
            {
                count += type != 0 ? 1 : 0;
            }

            return count;
        }

        /// <summary>
        /// Scatter stands trees on its share of the corners that fit its height, slope and tile, and none on a level place;
        /// a later rule takes a corner; Forest's patches hold together and a thinner one is a part of the thick one; Wall's
        /// lines are thin, and gaps leave part of them open; variations stay below the count.
        /// </summary>
        public static void TreeRules(FakeWorld world)
        {
            var low = Steep(3);
            low.Scatter(Tree, chance: 1f, toHeight: 0.5f);
            var plan = low.Plan(Area());
            var misplaced = 0;
            for (var k = 0; k < Side * Side; k++)
            {
                misplaced += (plan.Trees[k] != 0) != (plan.Heights.Cells[k] <= 300f) ? 1 : 0;
            }

            Expect.Equal(0, misplaced, "a tree on every corner in the lower half, and none above");
            Expect.True(TreesIn(plan) > 20 && TreesIn(plan) < Side * Side - 20, "hills with both: " + TreesIn(plan));

            var rocky = Steep(3);
            rocky.Paint(Tile.LordaeronSummerGrass);
            rocky.Paint(Tile.LordaeronSummerRock, fromSlope: 20f);
            rocky.Scatter(Tree, chance: 1f, onTile: Tile.LordaeronSummerRock);
            rocky.Level(1280f, 1280f, 400f);
            plan = rocky.Plan(Area());
            misplaced = 0;
            var levelTrees = 0;
            for (var k = 0; k < Side * Side; k++)
            {
                var dx = k % Side * 128f - 1280f;
                var dy = k / Side * 128f - 1280f;
                var level = dx * dx + dy * dy <= 400f * 400f;
                levelTrees += level && plan.Trees[k] != 0 ? 1 : 0;
                misplaced += !level && (plan.Trees[k] != 0) != (plan.Tiles[k] == Tile.LordaeronSummerRock) ? 1 : 0;
            }

            Expect.Equal(0, misplaced, "a tree on every rock corner and none on grass");
            Expect.Equal(0, levelTrees, "none on the level place");
            Expect.True(TreesIn(plan) > 10, "some rock: " + TreesIn(plan));

            var two = Steep(3);
            two.Scatter(Tree, chance: 1f);
            two.Scatter(OtherTree, chance: 1f, fromHeight: 0.5f);
            plan = two.Plan(Area());
            misplaced = 0;
            for (var k = 0; k < Side * Side; k++)
            {
                misplaced += plan.Trees[k] != (plan.Heights.Cells[k] >= 300f ? OtherTree : Tree) ? 1 : 0;
            }

            Expect.Equal(0, misplaced, "the later rule takes the upper half, one tree a corner");

            var some = new TerrainGenerator(4);
            some.Scatter(Tree, chance: 0.1f);
            var scattered = TreesIn(some.Plan(Area()));
            Expect.True(scattered > 20 && scattered < 75, "about a tenth scattered: " + scattered);

            var big = Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(8192f, 8192f));
            var thick = new TerrainGenerator(5);
            thick.Forest(Tree, cover: 0.5f);
            var thin = new TerrainGenerator(5);
            thin.Forest(Tree, cover: 0.5f, density: 0.5f);
            var thickPlan = thick.Plan(big);
            var thinPlan = thin.Plan(big);
            var outside = 0;
            var together = 0;
            var corners = thickPlan.Trees.Length;
            for (var k = 0; k < corners; k++)
            {
                outside += thinPlan.Trees[k] != 0 && thickPlan.Trees[k] == 0 ? 1 : 0;
                together += thickPlan.Trees[k] != 0 && k % thickPlan.Columns < thickPlan.Columns - 1 && thickPlan.Trees[k + 1] != 0 ? 1 : 0;
            }

            var forest = TreesIn(thickPlan);
            Expect.True(forest > corners / 5 && forest < corners * 4 / 5, "a forest on part of the ground: " + forest + " of " + corners);
            Expect.True(together > forest * 3 / 4, "in patches: " + together + " of " + forest + " have a tree beside them");
            Expect.Equal(0, outside, "a thinner forest is a part of the thick one");
            Expect.True(TreesIn(thinPlan) > forest / 3 && TreesIn(thinPlan) < forest * 2 / 3, "about half: " + TreesIn(thinPlan));

            var walled = new TerrainGenerator(6);
            walled.Wall(Tree, gaps: 0f);
            var open = new TerrainGenerator(6);
            open.Wall(Tree, gaps: 0.5f);
            var walledPlan = walled.Plan(big);
            var openPlan = open.Plan(big);
            var walls = TreesIn(walledPlan);
            outside = 0;
            var variations = 0;
            var top = 0;
            for (var k = 0; k < corners; k++)
            {
                outside += openPlan.Trees[k] != 0 && walledPlan.Trees[k] == 0 ? 1 : 0;
                variations |= walledPlan.Trees[k] != 0 ? 1 << walledPlan.TreeVariations[k] : 0;
                top = Math.Max(top, walledPlan.TreeVariations[k]);
            }

            Expect.True(walls > corners / 40 && walls < corners / 4, "walls are lines: " + walls + " of " + corners);
            Expect.Equal(0, outside, "gaps only take trees away");
            Expect.True(TreesIn(openPlan) > walls / 4 && TreesIn(openPlan) < walls * 3 / 4, "gaps of half leave about half: " + TreesIn(openPlan) + " of " + walls);
            Expect.True(top <= 9 && variations > 1 && (variations & 1) != 0, "variations 0 to 9, several used");
        }

        /// <summary>
        /// Two plans of one generator sharing an edge have the same trees on it, and plans that only touch stand the trees
        /// of one plan over both, each once.
        /// </summary>
        public static void TreeSeams(FakeWorld world)
        {
            var generator = Hills(31);
            generator.EdgeFade = 0f;
            generator.Forest(Tree, cover: 0.4f, density: 0.8f);
            generator.Wall(OtherTree, spacing: 1024f);
            generator.Scatter(Tree, chance: 0.05f);
            var left = generator.Plan(Bounds.FromCorners(new Vector2(-2560f, 0f), new Vector2(0f, 2560f)));
            var right = generator.Plan(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(2560f, 2560f)));
            var mismatches = 0;
            for (var row = 0; row < Side; row++)
            {
                mismatches += left.Trees[row * Side + Side - 1] != right.Trees[row * Side] || left.TreeVariations[row * Side + Side - 1] != right.TreeVariations[row * Side] ? 1 : 0;
            }

            Expect.Equal(0, mismatches, "the shared edge has the same trees");

            var whole = generator.Plan(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(5120f, 2560f)));
            var first = generator.Plan(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(2432f, 2560f)));
            var second = generator.Plan(Bounds.FromCorners(new Vector2(2560f, 0f), new Vector2(5120f, 2560f)));
            mismatches = 0;
            for (var k = 0; k < whole.Trees.Length; k++)
            {
                var column = k % whole.Columns;
                var row = k / whole.Columns;
                var part = column < first.Columns ? first.Trees[row * first.Columns + column] : second.Trees[row * second.Columns + column - first.Columns];
                mismatches += part != whole.Trees[k] ? 1 : 0;
            }

            Expect.Equal(whole.Trees.Length, first.Trees.Length + second.Trees.Length, "touching plans cover each corner once");
            Expect.Equal(0, mismatches, "and stand the whole plan's trees");
            Expect.True(TreesIn(whole) > 50, "of which there are many: " + TreesIn(whole));
        }

        /// <summary>A plan's trees from a seed are the ones worked out in .NET, also in Lua.</summary>
        public static void TreesEverywhere(FakeWorld world)
        {
            var generator = Hills(41);
            generator.Forest(Tree, cover: 0.4f, density: 0.8f, toSlope: 30f);
            generator.Wall(OtherTree, spacing: 1024f);
            generator.Scatter(Tree, chance: 0.05f, onTile: Tile.LordaeronSummerDirt);
            var plan = generator.Plan(Bounds.FromCorners(new Vector2(-1280f, 640f), new Vector2(3840f, 5760f)));
            var sum = 0;
            for (var k = 0; k < plan.Trees.Length; k++)
            {
                sum = (sum * 31 + (plan.Trees[k] == Tree ? 1 : plan.Trees[k] == OtherTree ? 2 : 0) * 16 + plan.TreeVariations[k]) % 1000003;
            }

            Expect.Equal(TreesEverywhereCount, TreesIn(plan), "the trees");
            Expect.Equal(TreesEverywhereSum, sum, "their places, types and variations");
        }

        private const int TreesEverywhereCount = 471;
        private const int TreesEverywhereSum = 408741;

        /// <summary>
        /// Apply makes the plan's trees after the ground, each at the ground's height; made in steps they are the same trees,
        /// in a step of their own; ending the scope removes every one, cut and dead ones too.
        /// </summary>
        public static void MakeTrees(FakeWorld world)
        {
            Flat();
            var generator = Hills(8);
            generator.Forest(Tree, cover: 0.5f);
            generator.Scatter(OtherTree, chance: 0.05f);
            var plan = generator.Plan(Area());
            var holder = Game.Instance.Add(new Holder());
            plan.Apply(holder);
            var whole = world.DestructableSpawns.ToArray();
            Expect.Equal(TreesIn(plan), whole.Length, "a tree for each in the plan");
            var raised = 0;
            var wrong = 0;
            foreach (var spawn in whole)
            {
                raised += spawn.Z > 50f ? 1 : 0;
                wrong += Math.Abs(spawn.Z - Terrain.Height(spawn.X, spawn.Y)) > 0.01f ? 1 : 0;
            }

            Expect.True(whole.Length > 20, "a forest: " + whole.Length);
            Expect.Equal(0, wrong, "each at the ground's height");
            Expect.True(raised > 0, "on hills");
            world.SlayDestructable(whole[0].Made!);
            world.PutDestructableLife(whole[1].Made!, 5f);
            holder.End();
            var removed = 0;
            foreach (var spawn in whole)
            {
                removed += world.RemovedDestructables.Contains(spawn.Made!) ? 1 : 0;
            }

            Expect.Equal(whole.Length, removed, "ending the scope removes every tree, the dead ones too");

            world.DestructableSpawns.Clear();
            var stepped = Game.Instance.Add(new Holder());
            var making = generator.Make(Area(), stepped, msPerStep: 0.2f);
            var steps = making.Step;
            for (var k = 0; k < 20000 && !making.IsDone; k++)
            {
                world.Advance(0.02f);
                steps += making.Step != steps.Substring(steps.LastIndexOf(',') + 1) ? "," + making.Step : "";
            }

            Expect.Equal("Heights,Tiles,Blocked,Trees", steps, "trees are a step of their own, the last");
            Expect.Equal(whole.Length, world.DestructableSpawns.Count, "made in steps, the same number of trees");
            var differ = 0;
            for (var k = 0; k < whole.Length; k++)
            {
                var a = whole[k];
                var b = world.DestructableSpawns[k];
                differ += a.TypeId != b.TypeId || a.X != b.X || a.Y != b.Y || a.Z != b.Z || a.Variation != b.Variation ? 1 : 0;
            }

            Expect.Equal(0, differ, "the same trees at the same heights");
            stepped.End();
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
