using System;
using System.Collections.Generic;

namespace Moonwell.Procgen
{
    /// <summary>
    /// Makes ground from a seed: rolling heights from layered noise, tiles and blocked corners by rules on height and
    /// slope, and level places for bases. Set it up, then <c>Plan(area)</c> works the ground out and the plan's
    /// <c>Apply</c> makes it. The same seed, settings and area size give the same ground on every machine; a seed from
    /// <c>Game.Random</c> gives new ground every game. The game makes no cliffs, ramps or water while it runs, so the
    /// ground is slopes. Slopes are read from the heights the plan adds, so the rules fit ground that starts flat.
    /// </summary>
    public sealed class TerrainGenerator
    {
        /// <summary>The distance between corners.</summary>
        private const float TileSize = 128f;

        /// <summary>How many corners across a patch of a rule's cover is.</summary>
        private const float PatchSize = 4f;

        private readonly List<Rule> paints = new List<Rule>();
        private readonly List<Rule> blocks = new List<Rule>();

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
        /// How far the lowest corner is moved from the ground as it was, in the game's units; below 0 digs. With a range
        /// around 0, as it starts, the ground stays at its level on the whole and the edge fade is gentle; a range all above
        /// 0 makes a plateau, with a steep rim where it fades.
        /// </summary>
        public float Lowest { get; set; } = -150f;

        /// <summary>How far the highest corner is moved from the ground as it was.</summary>
        public float Highest { get; set; } = 150f;

        /// <summary>How far across a hill is, in the game's units.</summary>
        public float FeatureSize { get; set; } = 1536f;

        /// <summary>The layers of noise: each adds bumps half the size of the one before.</summary>
        public int Layers { get; set; } = 4;

        /// <summary>How strong each layer is beside the one before, from 0 (smooth) to 1 (rough).</summary>
        public float Roughness { get; set; } = 0.5f;

        /// <summary>How far in from the area's edge the heights fade to the ground as it was, so the area has no seam; 0 for no fade.</summary>
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
        /// A level place, for a base or a spawn: the ground within a radius of a point (the game's coordinates) is level
        /// at the height of its centre, and blends back to the hills over <paramref name="blend"/> past it. It is never
        /// blocked. A place nearer the area's edge than <c>EdgeFade</c> is tilted by the fade. A radius of 0 or less throws
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
        /// changes nothing and gives the same plan each time. A <c>FeatureSize</c> of 0 or less, or fewer than 1 layer,
        /// throws ArgumentException.
        /// </summary>
        [LocalSafe]
        public TerrainPlan Plan(Bounds area)
        {
            if (!(FeatureSize > 0f) || Layers < 1)
            {
                throw new ArgumentException("TerrainGenerator.Plan takes a FeatureSize above 0 and 1 layer or more, not " + FeatureSize + " and " + Layers);
            }

            var size = area.Size;
            var columns = (int)(size.X / TileSize + 0.5f) + 1;
            var rows = (int)(size.Y / TileSize + 0.5f) + 1;
            var plan = new TerrainPlan(area.Min.X, area.Min.Y, columns, rows);
            var noise = Field.FromNoise(Seed, columns, rows, FeatureSize / TileSize, Layers, Roughness);
            noise.Normalize();
            var heights = plan.Heights;
            var cells = heights.Cells;
            for (var k = 0; k < cells.Length; k++)
            {
                cells[k] = Lowest + (Highest - Lowest) * noise.Cells[k];
            }

            var level = new bool[cells.Length];
            for (var k = 0; k < levels.Count; k++)
            {
                var place = levels[k];
                LevelPlace(heights, level, place[0] - plan.X, place[1] - plan.Y, place[2], place[3]);
            }

            heights.FadeEdges(EdgeFade / TileSize);
            var span = Highest - Lowest;
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var at = row * columns + column;
                    var alongX = Rise(cells, at, 1, column, columns);
                    var alongY = Rise(cells, at, columns, row, rows);
                    var steep = alongX * alongX + alongY * alongY;
                    var height = span != 0f ? Maths.Clamp01((cells[at] - Lowest) / span) : 0f;
                    for (var k = 0; k < paints.Count; k++)
                    {
                        var rule = paints[k];
                        if (rule.Fits(height, steep) && (rule.Cover >= 1f || Noise.Layered(rule.Seed, column / PatchSize, row / PatchSize, 2, 0.5f) < rule.Cover))
                        {
                            plan.Tiles[at] = rule.Value;
                        }
                    }

                    if (level[at])
                    {
                        continue;
                    }

                    var kinds = 0;
                    for (var k = 0; k < blocks.Count; k++)
                    {
                        if (blocks[k].Fits(height, steep))
                        {
                            kinds |= blocks[k].Value;
                        }
                    }

                    plan.Blocked[at] = (Pathing)kinds;
                }
            }

            return plan;
        }

        /// <summary>
        /// How fast the heights rise along a column or a row at a cell, as rise over run: from the cell before to the
        /// cell after, or from the cell itself at an edge. <paramref name="step"/> is how far apart the cells are in the
        /// array, <paramref name="index"/> the cell's place along the line and <paramref name="count"/> the line's length.
        /// </summary>
        private static float Rise(float[] cells, int at, int step, int index, int count)
        {
            var before = index > 0 ? at - step : at;
            var after = index < count - 1 ? at + step : at;
            return after == before ? 0f : (cells[after] - cells[before]) / ((after - before) / step * TileSize);
        }

        /// <summary>Levels the corners within a radius of a point (from the plan's first corner) at its centre's height, blends the ones in the rim past it, and marks the level ones.</summary>
        private static void LevelPlace(Field heights, bool[] level, float x, float y, float radius, float blend)
        {
            var cells = heights.Cells;
            var columns = heights.Columns;
            var rows = heights.Rows;
            var centreColumn = Math.Min(Math.Max(Noise.Floor(x / TileSize + 0.5f), 0), columns - 1);
            var centreRow = Math.Min(Math.Max(Noise.Floor(y / TileSize + 0.5f), 0), rows - 1);
            var target = cells[centreRow * columns + centreColumn];
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
