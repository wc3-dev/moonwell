using System;
using System.Collections.Generic;

namespace Moonwell.Procgen
{
    /// <summary>
    /// Makes ground from a seed: rolling heights from layered noise, tiles, blocked corners and trees by rules on height
    /// and slope, and level places for bases. Set it up, then <c>Plan(area)</c> works the ground out and the plan's
    /// <c>Apply</c> makes it. The same seed, settings and area give the same ground on every machine; a seed from
    /// <c>Game.Random</c> gives new ground every game. The game makes no cliffs, ramps or water while it runs, so the
    /// ground is slopes. Slopes are read from the heights the plan adds, so the rules fit ground that starts flat.
    /// </summary>
    public sealed class TerrainGenerator
    {
        /// <summary>The distance between corners.</summary>
        private const float TileSize = 128f;

        /// <summary>How many corners across a patch of a rule's cover is.</summary>
        private const float PatchSize = 4f;

        /// <summary>How many corners across a forest's patch is.</summary>
        private const float ForestPatch = 8f;

        /// <summary>How many corners across a gap in a forest wall is.</summary>
        private const int GapSize = 4;

        // What the work costs in the game's Lua, in microseconds, measured on 3.0.1.24371 (game facts, 11-13): fixed numbers,
        // so a step of Make does the same corners on every machine.
        private const float NoiseCost = 3.6f;   // a corner's noise, a layer
        private const float LevelCost = 1.5f;   // a corner in a level place's reach
        private const float FadeCost = 0.5f;    // a corner faded
        private const float RuleCost = 0.5f;    // a corner, a rule
        private const float RaiseCost = 6.2f;   // a corner's crater
        private const float PaintCost = 4f;     // a corner painted
        private const float ScanCost = 0.5f;    // a corner looked at for blocking (only blocked ones are walked)
        private const float BlockCost = 130f;   // a blocked corner, a kind
        private const float TreeCost = 45f;     // a tree made at the ground's height (200 in 9 ms)

        // Inferred from NoiseCost, not measured: deciding a corner's tree, a rule.
        private const float ForestCost = 8f;
        private const float WallCost = 14f;
        private const float ScatterCost = 1f;

        /// <summary>The names of Make's steps, as <c>TerrainMaking.Step</c> gives them.</summary>
        private const string HeightsStep = "Heights";

        private const string TilesStep = "Tiles";

        private const string BlockedStep = "Blocked";

        private const string TreesStep = "Trees";

        private readonly List<Rule> paints = new List<Rule>();
        private readonly List<Rule> blocks = new List<Rule>();
        private readonly List<TreeRule> trees = new List<TreeRule>();

        /// <summary>The level places: x, y, radius and blend each.</summary>
        private readonly List<float[]> levels = new List<float[]>();

        /// <summary>A generator for a seed.</summary>
        public TerrainGenerator(int seed)
        {
            Seed = seed;
        }

        /// <summary>The seed the ground comes from.</summary>
        [LocalSafe]
        public int Seed { get; }

        /// <summary>
        /// How far below the ground as it was the ground goes at most, in the game's units; below 0 digs. The same for
        /// every area, so plans of one generator meet. With a range
        /// around 0, as it starts, the ground stays at its level on the whole and the edge fade is gentle; a range all above
        /// 0 makes a plateau, with a steep rim where it fades.
        /// </summary>
        public float Lowest { get; set; } = -150f;

        /// <summary>How far above the ground as it was the ground goes at most. Most areas reach close to Lowest and Highest, and a few corners sit flat on them.</summary>
        public float Highest { get; set; } = 150f;

        /// <summary>How far across a hill is, in the game's units.</summary>
        public float FeatureSize { get; set; } = 1536f;

        /// <summary>The layers of noise: each adds bumps half the size of the one before.</summary>
        public int Layers { get; set; } = 4;

        /// <summary>How strong each layer is beside the one before, from 0 (smooth) to 1 (rough).</summary>
        public float Roughness { get; set; } = 0.5f;

        /// <summary>How far in from the area's edge the heights fade to the ground as it was, so an area inside a hand-made map has no seam; 0 for no fade, for areas that meet each other.</summary>
        public float EdgeFade { get; set; } = 512f;

        /// <summary>
        /// Paints a tile (one of <c>Tile</c>'s) on every corner whose height and slope are in the ranges. A height is a
        /// share of the range, 0 at <c>Lowest</c> and 1 at <c>Highest</c>; a slope is in degrees. A
        /// <paramref name="cover"/> below 1 paints patches about 512 across: 0.5 covers about half of the corners, and
        /// less covers fewer. Later rules paint over earlier ones. A tile of 0 throws ArgumentException.
        /// </summary>
        public void Paint(int tile, float fromHeight = 0f, float toHeight = 1f, float fromSlope = 0f, float toSlope = 90f, float cover = 1f)
        {
            if (tile == 0)
            {
                throw new ArgumentException("TerrainGenerator.Paint takes one of Tile's tiles, not 0");
            }

            paints.Add(new Rule(tile, fromHeight, toHeight, fromSlope, toSlope, cover, Seed + 7919 * (paints.Count + 1)));
        }

        /// <summary>Takes kinds of pathing from every corner whose height and slope are in the ranges, read as <c>Paint</c>'s. Rules add up.</summary>
        public void Block(Pathing kinds, float fromHeight = 0f, float toHeight = 1f, float fromSlope = 0f, float toSlope = 90f)
        {
            blocks.Add(new Rule((int)kinds, fromHeight, toHeight, fromSlope, toSlope, 1f, 0));
        }

        /// <summary>
        /// Stands patches of forest about 1024 across on the corners whose height, slope and tile fit, read as <c>Paint</c>'s,
        /// with <paramref name="onTile"/> a tile this generator paints there (0 for any corner). <paramref name="cover"/> is
        /// as <c>Paint</c>'s: 0.5 covers about half of the corners, and less covers fewer. <paramref name="density"/> is the
        /// share of a patch's corners that get a tree, 1 for a thick forest. A tree is a destructable of
        /// <paramref name="type"/> (<c>ObjectTypes.FourCC("LTlt")</c>) in one of its first <paramref name="variations"/>
        /// models (the game's tree walls have 10). Trees keep off level places, a corner has one tree at most, and a later
        /// tree rule takes a corner from an earlier one. A type of 0, or fewer than 1 variation, throws ArgumentException.
        /// </summary>
        public void Forest(int type, float cover = 0.5f, float density = 1f, float fromHeight = 0f, float toHeight = 1f, float fromSlope = 0f, float toSlope = 90f, int onTile = 0, int variations = 10)
        {
            trees.Add(new TreeRule(TreeRule.Forest, type, variations, cover, density, 0f, TreeSeed(), new Rule(onTile, fromHeight, toHeight, fromSlope, toSlope, 1f, 0)));
        }

        /// <summary>
        /// Stands forest walls along winding lines through the world, about <paramref name="spacing"/> apart (in the game's
        /// units) and <paramref name="width"/> trees thick, with a share <paramref name="gaps"/> of their length left open
        /// in gaps about 512 across, so they edge open ground and channel movement. The lines come from where the corners
        /// are in the world, so they go on across plans of this generator. Read as <c>Forest</c>'s otherwise. A spacing or
        /// width of 0 or less throws ArgumentException, as <c>Forest</c> does.
        /// </summary>
        public void Wall(int type, float spacing = 2048f, float width = 2f, float gaps = 0.15f, float fromHeight = 0f, float toHeight = 1f, float fromSlope = 0f, float toSlope = 90f, int onTile = 0, int variations = 10)
        {
            if (!(spacing > 0f) || !(width > 0f))
            {
                throw new ArgumentException("TerrainGenerator.Wall takes a spacing and a width above 0, not " + spacing + " and " + width);
            }

            trees.Add(new TreeRule(TreeRule.Walls, type, variations, gaps, width * width / 4f, spacing / TileSize, TreeSeed(), new Rule(onTile, fromHeight, toHeight, fromSlope, toSlope, 1f, 0)));
        }

        /// <summary>Stands single trees on a share <paramref name="chance"/> of the corners that fit, read as <c>Forest</c>'s.</summary>
        public void Scatter(int type, float chance = 0.02f, float fromHeight = 0f, float toHeight = 1f, float fromSlope = 0f, float toSlope = 90f, int onTile = 0, int variations = 10)
        {
            trees.Add(new TreeRule(TreeRule.Single, type, variations, chance, 0f, 0f, TreeSeed(), new Rule(onTile, fromHeight, toHeight, fromSlope, toSlope, 1f, 0)));
        }

        /// <summary>
        /// A level place, for a base or a spawn: the ground within a radius of a point (the game's coordinates) is level
        /// at the height of its centre, and blends back to the hills over <paramref name="blend"/> past it. It is never
        /// blocked and has no trees. A place nearer the area's edge than <c>EdgeFade</c> is tilted by the fade. A radius of 0 or less throws
        /// ArgumentException.
        /// </summary>
        public void Level(float x, float y, float radius, float blend = 256f)
        {
            if (!(radius > 0f))
            {
                throw new ArgumentException("TerrainGenerator.Level takes a radius above 0, not " + radius);
            }

            levels.Add(new[] { x, y, radius, blend > 0f ? blend : 0f });
        }

        /// <summary>
        /// Works out the ground for an area: a corner every 128 from its lower left. It reads only the settings, so it
        /// changes nothing and gives the same plan each time. The ground comes from the corners' places in the world, so
        /// plans of one generator meet where their areas touch when <c>EdgeFade</c> is 0, and an area's ground depends on
        /// where it is. A <c>FeatureSize</c> of 0 or less, or fewer than 1 layer, throws ArgumentException.
        /// </summary>
        [LocalSafe]
        public TerrainPlan Plan(Bounds area)
        {
            var ground = new Groundwork(this, area, null);
            ground.Hills(0, ground.Cells);
            ground.Levels(0, ground.Places);
            ground.Fade(0, ground.Cells);
            ground.Heights(0, ground.Corners);
            ground.Rules(0, ground.Corners);
            return ground.Plan;
        }

        /// <summary>
        /// Makes the ground for an area in steps over frames, for as long as the scope holds it: the same ground as
        /// <c>Plan(area).Apply(scope)</c>, from the settings as they are now. Each step does about
        /// <paramref name="msPerStep"/> of work, a fixed amount counted in corners, so every machine makes the same corners at
        /// the same moment; a step comes about every frame, the first one after this call. The <c>TerrainMaking</c> it gives
        /// tells how far it is, for a bar. Throws as <c>Plan</c> does, and for <paramref name="msPerStep"/> of 0 or less.
        /// </summary>
        [ChangesState]
        public TerrainMaking Make(Bounds area, Scope scope, float msPerStep = 4f)
        {
            if (!(msPerStep > 0f))
            {
                throw new ArgumentException("TerrainGenerator.Make takes milliseconds a step above 0, not " + msPerStep);
            }

            var ground = new Groundwork(this, area, scope);
            var work = new Work();
            work.Add(HeightsStep, ground.Cells, NoiseCost * Layers, ground.Hills);
            work.Add(HeightsStep, ground.Places, LevelCost * ground.PlaceCorners, ground.Levels);
            if (EdgeFade > 0f)
            {
                work.Add(HeightsStep, ground.Cells, FadeCost, ground.Fade);
            }

            work.Add(HeightsStep, ground.Corners, RaiseCost, ground.Heights);
            work.Add(TilesStep, ground.Corners, RuleCost * (2 + paints.Count + blocks.Count + trees.Count) + PaintCost + TreeRulesCost(), ground.Rules);
            if (blocks.Count > 0)
            {
                // shortcut: a guess of one blocked corner in eight until the rules have run, when the bar's speed changes to fit.
                work.Add(BlockedStep, ground.Corners, ScanCost + BlockCost / 8f, ground.Blocks, ground.BlockCostOf, ground.BlocksCost);
            }

            if (trees.Count > 0)
            {
                // shortcut: a guess of one tree in eight corners until the rules have run, as for blocking.
                work.Add(TreesStep, ground.Corners, ScanCost + TreeCost / 8f, ground.Trees, ground.TreeCostOf, ground.TreesCost);
            }

            return new TerrainMaking(work, msPerStep * 1000f, scope);
        }

        /// <summary>
        /// How far the noise is stretched about its middle: layers added together stay nearer the middle than one does, so
        /// this brings them back to one layer's spread, whatever the layers and roughness.
        /// </summary>
        private float Spread()
        {
            var sum = 0f;
            var squares = 0f;
            var strength = 1f;
            for (var layer = 0; layer < Layers; layer++)
            {
                sum += strength;
                squares += strength * strength;
                strength *= Roughness;
            }

            return sum / Maths.Sqrt(squares);
        }

        /// <summary>A seed for the next tree rule, apart from the paint rules' seeds.</summary>
        private int TreeSeed()
        {
            return Seed + 104729 * (trees.Count + 1);
        }

        /// <summary>What deciding a corner's tree costs, every tree rule.</summary>
        private float TreeRulesCost()
        {
            var cost = 0f;
            foreach (var rule in trees)
            {
                cost += rule.Kind == TreeRule.Forest ? ForestCost : rule.Kind == TreeRule.Walls ? WallCost : ScatterCost;
            }

            return cost;
        }

        /// <summary>The kinds in a set of pathing.</summary>
        private static int Kinds(Pathing kinds)
        {
            var count = 0;
            for (var bits = (int)kinds; bits != 0; bits &= bits - 1)
            {
                count++;
            }

            return count;
        }

        /// <summary>Levels the corners within a radius of a point (from the first corner of the cells) at a height, blends the ones in the rim past it, and marks the level ones.</summary>
        private static void LevelPlace(float[] cells, bool[] level, int columns, int rows, float x, float y, float radius, float blend, float target)
        {
            var reach = radius + blend;
            var lastColumn = Math.Min(Noise.Floor((x + reach) / TileSize) + 1, columns - 1);
            var lastRow = Math.Min(Noise.Floor((y + reach) / TileSize) + 1, rows - 1);
            for (var row = Math.Max(Noise.Floor((y - reach) / TileSize), 0); row <= lastRow; row++)
            {
                for (var column = Math.Max(Noise.Floor((x - reach) / TileSize), 0); column <= lastColumn; column++)
                {
                    var dx = column * TileSize - x;
                    var dy = row * TileSize - y;
                    var distance = Maths.Sqrt(dx * dx + dy * dy);
                    var at = row * columns + column;
                    if (distance <= radius)
                    {
                        cells[at] = target;
                        level[at] = true;
                    }
                    else if (distance < reach)
                    {
                        var t = (distance - radius) / blend;
                        cells[at] = target + (cells[at] - target) * (t * t * (3f - 2f * t));
                    }
                }
            }
        }

        /// <summary>
        /// One area's ground being worked out, and made when a scope is given, in passes over ranges of units, so Plan runs
        /// each whole and Make a step at a time. It takes the generator's settings as they are when it starts.
        /// </summary>
        private sealed class Groundwork
        {
            internal readonly TerrainPlan Plan;

            private readonly Scope? scope;
            private readonly int seed;
            private readonly float featureCells;
            private readonly int layers;
            private readonly float roughness;
            private readonly float lowest;
            private readonly float highest;
            private readonly float spread;
            private readonly float fade;
            private readonly Rule[] paints;
            private readonly Rule[] blocks;
            private readonly TreeRule[] trees;
            private readonly float[][] places;
            private readonly int firstColumn;
            private readonly int firstRow;
            private readonly int columns;
            private readonly int rows;

            /// <summary>The cells across: the area's corners and one more on each side, so a slope at the edge is read as the neighbouring plan reads it.</summary>
            private readonly int wide;

            private readonly float[] cells;
            private readonly bool[] level;

            /// <summary>The kinds of pathing the rules have taken so far, over every corner.</summary>
            private int kindsBlocked;

            /// <summary>The trees the rules have stood so far.</summary>
            private int treesStood;

            internal Groundwork(TerrainGenerator generator, Bounds area, Scope? scope)
            {
                if (!(generator.FeatureSize > 0f) || generator.Layers < 1)
                {
                    throw new ArgumentException("TerrainGenerator.Plan takes a FeatureSize above 0 and 1 layer or more, not " + generator.FeatureSize + " and " + generator.Layers);
                }

                this.scope = scope;
                seed = generator.Seed;
                featureCells = generator.FeatureSize / TileSize;
                layers = generator.Layers;
                roughness = generator.Roughness;
                lowest = generator.Lowest;
                highest = generator.Highest;
                spread = generator.Spread();
                fade = generator.EdgeFade / TileSize;
                paints = generator.paints.ToArray();
                blocks = generator.blocks.ToArray();
                trees = generator.trees.ToArray();
                places = generator.levels.ToArray();
                var size = area.Size;
                columns = (int)(size.X / TileSize + 0.5f) + 1;
                rows = (int)(size.Y / TileSize + 0.5f) + 1;
                Plan = new TerrainPlan(area.Min.X, area.Min.Y, columns, rows);
                firstColumn = Noise.Floor(area.Min.X / TileSize + 0.5f);
                firstRow = Noise.Floor(area.Min.Y / TileSize + 0.5f);
                wide = columns + 2;
                cells = new float[wide * (rows + 2)];
                level = new bool[cells.Length];
            }

            /// <summary>The cells: the area's corners and one more on every side.</summary>
            internal int Cells => cells.Length;

            /// <summary>The area's corners.</summary>
            internal int Corners => columns * rows;

            /// <summary>The level places.</summary>
            internal int Places => places.Length;

            /// <summary>The most corners a level place reaches.</summary>
            internal float PlaceCorners
            {
                get
                {
                    var most = 0f;
                    for (var k = 0; k < places.Length; k++)
                    {
                        var across = 2f * (places[k][2] + places[k][3]) / TileSize + 3f;
                        most = Math.Max(most, across * across);
                    }

                    return most;
                }
            }

            /// <summary>The hills of the cells in a range, from the noise.</summary>
            internal void Hills(int from, int to)
            {
                for (var at = from; at < to; at++)
                {
                    cells[at] = Height(firstColumn - 1 + at % wide, firstRow - 1 + at / wide);
                }
            }

            /// <summary>Levels the places in a range, in order.</summary>
            internal void Levels(int from, int to)
            {
                for (var k = from; k < to; k++)
                {
                    var place = places[k];
                    var target = Height(Noise.Floor(place[0] / TileSize + 0.5f), Noise.Floor(place[1] / TileSize + 0.5f));
                    LevelPlace(cells, level, wide, rows + 2, place[0] - (firstColumn - 1) * TileSize, place[1] - (firstRow - 1) * TileSize, place[2], place[3], target);
                }
            }

            /// <summary>
            /// Fades the heights of the cells in a range to the ground as it was at the area's edge, over the fade's corners in, as
            /// <c>Field.FadeEdges</c> does; the cells outside the area become the ground as it was. No fade does nothing.
            /// </summary>
            internal void Fade(int from, int to)
            {
                if (!(fade > 0f))
                {
                    return;
                }

                for (var at = from; at < to; at++)
                {
                    var row = at / wide - 1;
                    var column = at % wide - 1;
                    var edge = Math.Min(Math.Min(column, columns - 1 - column), Math.Min(row, rows - 1 - row));
                    if (edge < fade)
                    {
                        var t = edge > 0 ? edge / fade : 0f;
                        cells[at] *= t * t * (3f - 2f * t);
                    }
                }
            }

            /// <summary>Puts the heights of the corners in a range in the plan, and raises them when making.</summary>
            internal void Heights(int from, int to)
            {
                for (var at = from; at < to; at++)
                {
                    Plan.Heights.Cells[at] = cells[Cell(at)];
                }

                if (scope != null)
                {
                    InRows(from, to, RaisePiece);
                }
            }

            /// <summary>Works out the tile and the blocked pathing of the corners in a range by the rules, and paints them when making.</summary>
            internal void Rules(int from, int to)
            {
                var span = highest - lowest;
                for (var at = from; at < to; at++)
                {
                    var cell = Cell(at);
                    var alongX = (cells[cell + 1] - cells[cell - 1]) / (2f * TileSize);
                    var alongY = (cells[cell + wide] - cells[cell - wide]) / (2f * TileSize);
                    var steep = alongX * alongX + alongY * alongY;
                    var height = span != 0f ? Maths.Clamp01((cells[cell] - lowest) / span) : 0f;
                    for (var k = 0; k < paints.Length; k++)
                    {
                        var rule = paints[k];
                        if (rule.Fits(height, steep) && (rule.Cover >= 1f || Noise.Layered(rule.Seed, (firstColumn + at % columns) / PatchSize, (firstRow + at / columns) / PatchSize, 2, 0.5f) < rule.Cover))
                        {
                            Plan.Tiles[at] = rule.Value;
                        }
                    }

                    if (level[cell])
                    {
                        continue;
                    }

                    var kinds = 0;
                    for (var k = 0; k < blocks.Length; k++)
                    {
                        if (blocks[k].Fits(height, steep))
                        {
                            kinds |= blocks[k].Value;
                        }
                    }

                    Plan.Blocked[at] = (Pathing)kinds;
                    kindsBlocked += Kinds((Pathing)kinds);
                    var column = firstColumn + at % columns;
                    var row = firstRow + at / columns;
                    for (var k = trees.Length - 1; k >= 0; k--)
                    {
                        var rule = trees[k];
                        if (rule.Ranges.Fits(height, steep) && (rule.Ranges.Value == 0 || rule.Ranges.Value == Plan.Tiles[at]) && rule.Stands(column, row))
                        {
                            Plan.Trees[at] = rule.Type;
                            Plan.TreeVariations[at] = rule.Variation(column, row);
                            treesStood++;
                            break;
                        }
                    }
                }

                if (scope != null)
                {
                    InRows(from, to, PaintPiece);
                }
            }

            /// <summary>Blocks the corners in a range.</summary>
            internal void Blocks(int from, int to)
            {
                InRows(from, to, BlockPiece);
            }

            /// <summary>Makes the trees of the corners in a range.</summary>
            internal void Trees(int from, int to)
            {
                Plan.MakeTrees(from, to, scope!);
            }

            /// <summary>What making a corner's tree costs.</summary>
            internal float TreeCostOf(int at)
            {
                return ScanCost + (Plan.Trees[at] != 0 ? TreeCost : 0f);
            }

            /// <summary>What making every tree costs, once the rules have run.</summary>
            internal float TreesCost()
            {
                return ScanCost * Corners + TreeCost * treesStood;
            }

            /// <summary>What blocking a corner costs.</summary>
            internal float BlockCostOf(int at)
            {
                return ScanCost + BlockCost * Kinds(Plan.Blocked[at]);
            }

            /// <summary>What blocking every corner costs, once the rules have run.</summary>
            internal float BlocksCost()
            {
                return ScanCost * Corners + BlockCost * kindsBlocked;
            }

            /// <summary>The cell of a corner of the area.</summary>
            private int Cell(int at)
            {
                return (at / columns + 1) * wide + at % columns + 1;
            }

            /// <summary>The height of a corner of the world, counted in corners from (0, 0), from the noise at its place, between Lowest and Highest.</summary>
            private float Height(int column, int row)
            {
                var noise = Noise.Layered(seed, column / featureCells, row / featureCells, layers, roughness);
                return lowest + (highest - lowest) * Maths.Clamp01(0.5f + (noise - 0.5f) * spread);
            }

            /// <summary>Runs a piece for each row's part of a range of corners, with its first corner and the one past its last.</summary>
            private void InRows(int from, int to, Action<int, int> piece)
            {
                while (from < to)
                {
                    var end = Math.Min(to, (from / columns + 1) * columns);
                    piece(from, end);
                    from = end;
                }
            }

            private void RaisePiece(int from, int to)
            {
                var by = new float[to - from];
                var any = false;
                for (var at = from; at < to; at++)
                {
                    by[at - from] = Plan.Heights.Cells[at];
                    any = any || by[at - from] != 0f;
                }

                if (any)
                {
                    Terrain.Raise(X(from), Y(from), by.Length, by, scope!);
                }
            }

            private void PaintPiece(int from, int to)
            {
                var tiles = new int[to - from];
                var any = false;
                for (var at = from; at < to; at++)
                {
                    tiles[at - from] = Plan.Tiles[at];
                    any = any || tiles[at - from] != 0;
                }

                if (any)
                {
                    Terrain.SetTile(X(from), Y(from), tiles.Length, tiles, scope!);
                }
            }

            private void BlockPiece(int from, int to)
            {
                var kinds = new Pathing[to - from];
                var any = false;
                for (var at = from; at < to; at++)
                {
                    kinds[at - from] = Plan.Blocked[at];
                    any = any || kinds[at - from] != Pathing.None;
                }

                if (any)
                {
                    Terrain.Block(X(from), Y(from), kinds.Length, kinds, scope!);
                }
            }

            /// <summary>The x of a corner of the area.</summary>
            private float X(int at)
            {
                return Plan.X + at % columns * TileSize;
            }

            /// <summary>The y of a corner of the area.</summary>
            private float Y(int at)
            {
                return Plan.Y + at / columns * TileSize;
            }
        }

        /// <summary>A tree rule: its kind and type, where it stands trees, and the heights, slopes and tile it fits.</summary>
        private sealed class TreeRule
        {
            internal const int Forest = 0;
            internal const int Walls = 1;
            internal const int Single = 2;

            internal readonly int Kind;
            internal readonly int Type;

            /// <summary>The heights and slopes it fits, with the tile as its value (0 for any).</summary>
            internal readonly Rule Ranges;

            private readonly int variations;

            /// <summary>A forest's cover, a wall's gaps, or a scatter's chance.</summary>
            private readonly float share;

            /// <summary>A forest's density, or a wall's half width in corners, squared.</summary>
            private readonly float spare;

            /// <summary>A wall's spacing in corners.</summary>
            private readonly float spacing;

            private readonly int seed;

            internal TreeRule(int kind, int type, int variations, float share, float spare, float spacing, int seed, Rule ranges)
            {
                if (type == 0 || variations < 1)
                {
                    throw new ArgumentException("TerrainGenerator's tree rules take a destructable type, not 0, and 1 variation or more, not " + type + " and " + variations);
                }

                Kind = kind;
                Type = type;
                this.variations = variations;
                this.share = share;
                this.spare = spare;
                this.spacing = spacing;
                this.seed = seed;
                Ranges = ranges;
            }

            /// <summary>Whether the rule stands a tree on a corner of the world, counted in corners from (0, 0).</summary>
            internal bool Stands(int column, int row)
            {
                if (Kind == Single)
                {
                    return Noise.Hash(seed, column, row) < share;
                }

                if (Kind == Forest)
                {
                    return Noise.Layered(seed, column / ForestPatch, row / ForestPatch, 2, 0.5f) < share && Noise.Hash(seed + 1, column, row) < spare;
                }

                // A wall is where the noise crosses its middle: the corners nearer that line than half the width, by the noise's slope there.
                var here = Noise.At(seed, column / spacing, row / spacing);
                var alongX = Noise.At(seed, (column + 1) / spacing, row / spacing) - here;
                var alongY = Noise.At(seed, column / spacing, (row + 1) / spacing) - here;
                var off = here - 0.5f;
                return off * off <= spare * (alongX * alongX + alongY * alongY)
                    && Noise.Hash(seed + 1, Noise.Floor(column / (float)GapSize), Noise.Floor(row / (float)GapSize)) >= share;
            }

            /// <summary>The model a tree on a corner of the world is made with.</summary>
            internal int Variation(int column, int row)
            {
                return Math.Min((int)(Noise.Hash(seed + 2, column, row) * variations), variations - 1);
            }
        }

        /// <summary>A paint or block rule: what it gives, and the heights and slopes it fits.</summary>
        private sealed class Rule
        {
            /// <summary>The tile, or the kinds of pathing.</summary>
            internal readonly int Value;

            /// <summary>The share of the corners a paint rule covers, 1 for all.</summary>
            internal readonly float Cover;

            /// <summary>The seed of a paint rule's patches.</summary>
            internal readonly int Seed;

            private readonly float fromHeight;
            private readonly float toHeight;

            /// <summary>The least steepness, as rise over run, squared.</summary>
            private readonly float fromSteep;

            /// <summary>The most steepness, as fromSteep; below 0 for no limit.</summary>
            private readonly float toSteep;

            internal Rule(int value, float fromHeight, float toHeight, float fromSlope, float toSlope, float cover, int seed)
            {
                Value = value;
                this.fromHeight = fromHeight;
                this.toHeight = toHeight;
                fromSteep = Steepness(fromSlope);
                toSteep = toSlope >= 90f ? -1f : Steepness(toSlope);
                Cover = cover;
                Seed = seed;
            }

            /// <summary>Whether a corner's height (a share of the range) and steepness (rise over run, squared) are in the rule's ranges.</summary>
            internal bool Fits(float height, float steep)
            {
                return height >= fromHeight && height <= toHeight && steep >= fromSteep && (toSteep < 0f || steep <= toSteep);
            }

            /// <summary>A slope in degrees as rise over run, squared, so a corner's slope is compared with no root or angle.</summary>
            private static float Steepness(float degrees)
            {
                if (!(degrees > 0f))
                {
                    return 0f;
                }

                var rise = Angles.Sin(degrees) / Angles.Cos(degrees);
                return rise * rise;
            }
        }
    }
}
