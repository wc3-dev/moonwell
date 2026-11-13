using System;
using System.Collections.Generic;
using Moonwell.Procgen;

namespace Moonwell.Testing
{
    /// <summary>Cases for procgen biomes and presets (decisions 291 and 292): biomes by area and in patches, blended at their borders, meeting across plans, made in steps, and presets changed rule by rule.</summary>
    public static class BiomeCases
    {
        private static readonly int Tree = ObjectTypes.FourCC("LTlt");
        private static readonly int SnowTree = ObjectTypes.FourCC("WTst");

        /// <summary>A game component that holds what a case makes, ended by the case.</summary>
        private sealed class Holder : GameComponent
        {
        }

        /// <summary>An area of 41 by 41 corners from (0, 0).</summary>
        private static Bounds Area()
        {
            return Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(5120f, 5120f));
        }

        /// <summary>Far past every plan of the cases.</summary>
        private static Bounds Endless()
        {
            return Bounds.FromCorners(new Vector2(-100000f, -100000f), new Vector2(100000f, 100000f));
        }

        /// <summary>A generator whose own ground is flat grass, with no fade.</summary>
        private static TerrainGenerator Plain(int seed)
        {
            var land = new TerrainGenerator(seed) { Lowest = 0f, Highest = 0f, EdgeFade = 0f };
            land.Paint(Tile.LordaeronSummerGrass);
            return land;
        }

        /// <summary>A biome flat at a height, painted with a tile, with a tree on every corner when a type is given.</summary>
        private static Biome Plateau(float height, int tile, int tree)
        {
            var biome = new Biome { Lowest = height, Highest = height };
            biome.Paint(tile);
            if (tree != 0)
            {
                biome.Scatter(tree, chance: 1f);
            }

            return biome;
        }

        /// <summary>
        /// One biome in an area: its height, tile and trees inside, the generator's own past the blend, and between them
        /// heights that fall steadily and tiles from one or the other, each with its own trees. A later call lies over an
        /// earlier one, a biome changed after the call changes the next plan, and no blend gives a sharp border.
        /// </summary>
        public static void Use(FakeWorld world)
        {
            var land = Plain(5);
            var snow = Plateau(200f, Tile.LordaeronWinterSnow, SnowTree);
            land.Use(snow, Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(1280f, 5120f)));
            var plan = land.Plan(Area());
            var wrong = 0;
            var rising = 0;
            var snowy = 0;
            var grassy = 0;
            for (var row = 0; row < plan.Rows; row++)
            {
                for (var column = 0; column < plan.Columns; column++)
                {
                    var at = row * plan.Columns + column;
                    var height = plan.Heights[column, row];
                    var isSnow = plan.Tiles[at] == Tile.LordaeronWinterSnow;
                    if (column <= 10)
                    {
                        wrong += height == 200f && isSnow && plan.Trees[at] == SnowTree ? 0 : 1;
                    }
                    else if (column >= 14)
                    {
                        wrong += height == 0f && plan.Tiles[at] == Tile.LordaeronSummerGrass && plan.Trees[at] == 0 ? 0 : 1;
                    }
                    else
                    {
                        wrong += height > 0f && height < 200f ? 0 : 1;
                        wrong += isSnow ? (plan.Trees[at] == SnowTree ? 0 : 1) : (plan.Tiles[at] == Tile.LordaeronSummerGrass && plan.Trees[at] == 0 ? 0 : 1);
                        snowy += isSnow ? 1 : 0;
                        grassy += isSnow ? 0 : 1;
                    }

                    rising += column > 0 && height > plan.Heights[column - 1, row] ? 1 : 0;
                }
            }

            Expect.Equal(0, wrong, "the biome inside, the generator's own past the blend, one or the other between");
            Expect.Equal(0, rising, "the heights fall steadily across the blend");
            Expect.True(snowy > 0 && grassy > 0, "both in the blend: " + snowy + ", " + grassy);

            land.Use(Plateau(100f, Tile.LordaeronWinterRock, 0), Bounds.FromCorners(new Vector2(256f, 256f), new Vector2(768f, 768f)));
            snow.Highest = 300f;
            snow.Lowest = 300f;
            var over = land.Plan(Area());
            Expect.Near(100f, over.Heights[4, 4], "a later call lies over an earlier one");
            Expect.Equal(Tile.LordaeronWinterRock, over.Tiles[4 * over.Columns + 4], "its tile");
            Expect.Equal(0, over.Trees[4 * over.Columns + 4], "and its trees, none");
            Expect.Near(300f, over.Heights[8, 30], "a biome changed after the call changes the next plan");

            land.BiomeBlend = 0f;
            var sharp = land.Plan(Area());
            Expect.Near(300f, sharp.Heights[10, 20], "with no blend the area's edge is the biome's");
            Expect.Near(0f, sharp.Heights[11, 20], "and the next corner the generator's own");
        }

        /// <summary>
        /// Several biomes in patches: both cover part of the area, the heights blend smoothly at their borders, each corner
        /// takes its tile from one of them, and a biome listed twice covers more.
        /// </summary>
        public static void Mix(FakeWorld world)
        {
            var land = Plain(7);
            land.Mix(Endless(), 2048f, Plateau(0f, Tile.LordaeronSummerDirt, 0), Plateau(100f, Tile.LordaeronWinterSnow, 0));
            var plan = land.Plan(Area());
            var low = 0;
            var high = 0;
            var blended = 0;
            var wrong = 0;
            var steepest = 0f;
            for (var row = 0; row < plan.Rows; row++)
            {
                for (var column = 0; column < plan.Columns; column++)
                {
                    var height = plan.Heights[column, row];
                    var tile = plan.Tiles[row * plan.Columns + column];
                    low += height == 0f ? 1 : 0;
                    high += height == 100f ? 1 : 0;
                    blended += height > 0f && height < 100f ? 1 : 0;
                    wrong += height < 0f || height > 100f ? 1 : 0;
                    wrong += height == 0f && tile != Tile.LordaeronSummerDirt || height == 100f && tile != Tile.LordaeronWinterSnow ? 1 : 0;
                    wrong += tile != Tile.LordaeronSummerDirt && tile != Tile.LordaeronWinterSnow ? 1 : 0;
                    if (column > 0)
                    {
                        steepest = Math.Max(steepest, Math.Abs(height - plan.Heights[column - 1, row]));
                    }
                }
            }

            Expect.Equal(0, wrong, "heights between the two, and each corner one of their tiles");
            Expect.True(low > 100 && high > 100 && blended > 100, "both biomes and borders between: " + low + ", " + high + ", " + blended);
            Expect.True(steepest < 60f, "the borders blend smoothly: " + steepest);

            var twice = Plain(7);
            var snow = Plateau(100f, Tile.LordaeronWinterSnow, 0);
            twice.Mix(Endless(), 2048f, Plateau(0f, Tile.LordaeronSummerDirt, 0), snow, snow);
            var more = twice.Plan(Area());
            var snowy = 0;
            var once = 0;
            for (var k = 0; k < more.Tiles.Length; k++)
            {
                snowy += more.Tiles[k] == Tile.LordaeronWinterSnow ? 1 : 0;
                once += plan.Tiles[k] == Tile.LordaeronWinterSnow ? 1 : 0;
            }

            Expect.True(snowy > once, "a biome listed twice covers more: " + snowy + " against " + once);
        }

        /// <summary>A world of biomes: an area of snowy forest over rocky hills and grassland in patches, on hills of the generator's own.</summary>
        private static TerrainGenerator World(int seed)
        {
            var land = new TerrainGenerator(seed) { Lowest = -100f, Highest = 300f, FeatureSize = 1024f, EdgeFade = 0f };
            land.Paint(Tile.LordaeronSummerGrass);
            land.Block(Pathing.Walk | Pathing.Build, fromSlope: 30f);
            land.Scatter(Tree, chance: 0.05f);
            land.Mix(Bounds.FromCorners(new Vector2(-100000f, -100000f), new Vector2(100000f, 1500f)), 1536f, new RockyHills(), new Grassland());
            land.Use(new SnowyForest(), Bounds.FromCorners(new Vector2(-1500f, 1800f), new Vector2(100000f, 100000f)));
            return land;
        }

        /// <summary>Two plans of one generator agree on biome borders across their shared edge, and the halves are the whole planned in one go.</summary>
        public static void Seams(FakeWorld world)
        {
            var land = World(21);
            var whole = land.Plan(Bounds.FromCorners(new Vector2(-2560f, 0f), new Vector2(2560f, 2560f)));
            var left = land.Plan(Bounds.FromCorners(new Vector2(-2560f, 0f), new Vector2(0f, 2560f)));
            var right = land.Plan(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(2560f, 2560f)));
            var differ = 0;
            var tiles = new List<int>();
            for (var row = 0; row < 21; row++)
            {
                var edge = whole.Tiles[row * whole.Columns + 20];
                if (!tiles.Contains(edge))
                {
                    tiles.Add(edge);
                }

                for (var column = 0; column < 21; column++)
                {
                    differ += Same(whole, column, row, left, column, row) ? 0 : 1;
                    differ += Same(whole, column + 20, row, right, column, row) ? 0 : 1;
                }
            }

            Expect.True(tiles.Count >= 3, "biome borders cross the shared edge: " + tiles.Count + " tiles on it");
            Expect.Equal(0, differ, "both halves match the whole, the shared edge too");
        }

        /// <summary>A plan's biomes from a seed are the ones worked out in .NET, also in Lua.</summary>
        public static void Everywhere(FakeWorld world)
        {
            var plan = World(41).Plan(Bounds.FromCorners(new Vector2(-1280f, 640f), new Vector2(3840f, 5760f)));
            var sum = 0;
            var trees = 0;
            for (var k = 0; k < plan.Tiles.Length; k++)
            {
                sum = (sum * 31 + plan.Tiles[k] % 1000 + (int)plan.Blocked[k] * 7 + (plan.Trees[k] % 1000) * 13 + plan.TreeVariations[k]) % 1000003;
                trees += plan.Trees[k] != 0 ? 1 : 0;
            }

            Expect.Equal(EverywhereTrees, trees, "the trees");
            Expect.Equal(EverywhereSum, sum, "the tiles, blocking and trees");
        }

        private const int EverywhereTrees = 428;
        private const int EverywhereSum = 70398;

        /// <summary>A road from a level place on low ground to one among high rocky hills crosses the biome border at an easy slope.</summary>
        public static void Road(FakeWorld world)
        {
            var land = new TerrainGenerator(9) { Lowest = 0f, Highest = 200f, FeatureSize = 1024f, EdgeFade = 0f };
            land.Paint(Tile.LordaeronSummerGrass);
            land.Use(new RockyHills { Lowest = 300f, Highest = 900f }, Bounds.FromCorners(new Vector2(2560f, -100000f), new Vector2(100000f, 100000f)));
            land.RoadTile = Tile.LordaeronSummerDirt;
            var road = land.Road(land.Level(640f, 2560f, 256f), land.Level(4480f, 2560f, 256f));
            var plan = land.Plan(Area());
            var steepest = 0f;
            var across = 0;
            for (var row = 1; row < plan.Rows; row++)
            {
                for (var column = 1; column < plan.Columns; column++)
                {
                    if (PlacesAndRoadsCases.Gap(road, column * 128f, row * 128f) <= 0f && PlacesAndRoadsCases.Gap(road, (column - 1) * 128f, row * 128f) <= 0f)
                    {
                        steepest = Math.Max(steepest, Math.Abs(plan.Heights[column, row] - plan.Heights[column - 1, row]));
                        across += column == 20 ? 1 : 0;
                    }
                }
            }

            Expect.True(across > 0, "the road crosses the border");
            Expect.True(steepest <= 0.14f * 128f * 1.5f, "at an easy slope: " + steepest);
            Expect.Near(0f, plan.Heights[35, 20], "to a place level at the ground as it was, among the hills");
            Expect.True(plan.Heights[30, 35] > 300f, "which are high: " + plan.Heights[30, 35]);
        }

        /// <summary>Made in steps, ground of several biomes is the one-go plan's, corner for corner, its trees too.</summary>
        public static void MakeInSteps(FakeWorld world)
        {
            TerrainGeneratorCases.Flat();
            var land = World(8);
            var area = Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(2560f, 2560f));
            var holder = Game.Instance.Add(new Holder());
            var making = land.Make(area, holder, msPerStep: 0.2f);
            for (var k = 0; k < 20000 && !making.IsDone; k++)
            {
                world.Advance(0.02f);
            }

            Expect.True(making.IsDone, "the ground is made");
            var stepped = TerrainGeneratorCases.Ground();
            var steppedTrees = world.DestructableSpawns.ToArray();
            holder.End();
            world.DestructableSpawns.Clear();
            land.Plan(area).Apply(Game.Instance);
            var whole = TerrainGeneratorCases.Ground();
            var differ = 0;
            for (var k = 0; k < whole.Length; k++)
            {
                differ += whole[k] == stepped[k] ? 0 : 1;
            }

            var treesDiffer = steppedTrees.Length == world.DestructableSpawns.Count ? 0 : 1;
            for (var k = 0; treesDiffer == 0 && k < steppedTrees.Length; k++)
            {
                var a = steppedTrees[k];
                var b = world.DestructableSpawns[k];
                treesDiffer += a.TypeId != b.TypeId || a.X != b.X || a.Y != b.Y || a.Z != b.Z || a.Variation != b.Variation ? 1 : 0;
            }

            Expect.Equal(0, differ, "the ground made in steps is the one-go plan's, corner for corner");
            Expect.Equal(0, treesDiffer, "and so are its trees: " + steppedTrees.Length);
            Expect.True(steppedTrees.Length > 10, "of which there are some");
        }

        /// <summary>
        /// Each preset makes ground of its own tiles; one changed rule by rule makes what the changes say: a dropped rule paints
        /// nothing, a changed one stands another tree, an added one stands its own, and its tiles leave the dropped one out.
        /// </summary>
        public static void Presets(FakeWorld world)
        {
            var presets = new Biome[] { new Grassland(), new RockyHills(), new SnowyForest(), new Desert() };
            var trees = 0;
            foreach (var preset in presets)
            {
                var land = new TerrainGenerator(3) { EdgeFade = 0f };
                land.Use(preset, Endless());
                var plan = land.Plan(Area());
                var foreign = 0;
                var own = new List<int>(preset.Tiles);
                var used = new List<int>();
                for (var k = 0; k < plan.Tiles.Length; k++)
                {
                    foreign += own.Contains(plan.Tiles[k]) ? 0 : 1;
                    trees += plan.Trees[k] != 0 ? 1 : 0;
                    if (!used.Contains(plan.Tiles[k]))
                    {
                        used.Add(plan.Tiles[k]);
                    }
                }

                Expect.Equal(0, foreign, "a preset paints only its own tiles");
                Expect.True(used.Count >= 2, "more than one of them: " + used.Count);
            }

            Expect.True(trees > 100, "and stands trees: " + trees);

            var north = new SnowyForest { Highest = 900f, FeatureSize = 1024f };
            var hills = new TerrainGenerator(3) { EdgeFade = 0f };
            hills.Use(north, Endless());
            var rocky = Count(hills.Plan(Area()).Tiles, Tile.LordaeronWinterRock);
            north.Remove(north.Rock);
            north.Remove(north.Single);
            north.Trees.Type = Tree;
            var changed = hills.Plan(Area());
            Expect.True(rocky > 0, "rock on the steep ground first: " + rocky);
            Expect.Equal(0, Count(changed.Tiles, Tile.LordaeronWinterRock), "none once its rule is dropped");
            Expect.Equal(0, Count(changed.Trees, SnowTree), "no snowy trees once both tree rules are changed or dropped");
            Expect.True(Count(changed.Trees, Tree) > 50, "the changed rule's trees: " + Count(changed.Trees, Tree));
            var tiles = new List<int>(north.Tiles);
            Expect.True(!tiles.Contains(Tile.LordaeronWinterRock) && tiles.Contains(Tile.LordaeronWinterSnow), "its tiles leave the dropped rule's out");

            north.Scatter(SnowTree, chance: 1f, onTile: Tile.LordaeronWinterGrassySnow);
            var added = hills.Plan(Area());
            var bare = 0;
            for (var k = 0; k < added.Tiles.Length; k++)
            {
                bare += added.Tiles[k] == Tile.LordaeronWinterGrassySnow && added.Trees[k] != SnowTree ? 1 : 0;
            }

            Expect.True(Count(added.Tiles, Tile.LordaeronWinterGrassySnow) > 0 && bare == 0, "an added rule stands its trees on every corner it fits");
        }

        /// <summary>A null biome, a patch size of 0, no biomes, a rule set to nothing and a biome with no layers each throw.</summary>
        public static void Refused(FakeWorld world)
        {
            var land = new TerrainGenerator(1);
            Expect.Throws<ArgumentException>(() => land.Use(null!, Area()), "a null biome");
            Expect.Throws<ArgumentException>(() => land.Mix(Area(), 0f, new Biome()), "a patch size of 0");
            Expect.Throws<ArgumentException>(() => land.Mix(Area(), 1024f), "no biomes");
            Expect.Throws<ArgumentException>(() => land.Mix(Area(), 1024f, new Biome(), null!), "a null one among them");
            var biome = new Biome();
            Expect.Throws<ArgumentException>(() => biome.Paint(Tile.LordaeronSummerGrass).Tile = 0, "a paint rule's tile set to 0");
            Expect.Throws<ArgumentException>(() => biome.Forest(Tree).Type = 0, "a tree rule's type set to 0");
            Expect.Throws<ArgumentException>(() => biome.Scatter(Tree).Variations = 0, "no variations");
            Expect.Throws<ArgumentException>(() => biome.Wall(Tree).Spacing = 0f, "a wall spacing of 0");
            Expect.Throws<ArgumentException>(() => biome.Wall(Tree).Width = -1f, "a wall width below 0");
            land.Use(new Biome { Layers = 0 }, Area());
            Expect.Throws<ArgumentException>(() => land.Plan(Area()), "a biome with no layers");
        }

        private static int Count(int[] values, int value)
        {
            var count = 0;
            foreach (var each in values)
            {
                count += each == value ? 1 : 0;
            }

            return count;
        }

        /// <summary>Whether a corner of one plan has the same height, tile, blocked pathing and tree as a corner of another.</summary>
        private static bool Same(TerrainPlan a, int columnA, int rowA, TerrainPlan b, int columnB, int rowB)
        {
            var at = rowA * a.Columns + columnA;
            var bt = rowB * b.Columns + columnB;
            return a.Heights.Cells[at] == b.Heights.Cells[bt] && a.Tiles[at] == b.Tiles[bt] && a.Blocked[at] == b.Blocked[bt] && a.Trees[at] == b.Trees[bt] && a.TreeVariations[at] == b.TreeVariations[bt];
        }
    }
}
