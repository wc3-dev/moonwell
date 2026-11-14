namespace Moonwell.Procgen
{
    /// <summary>
    /// Wide craters fitted to an area's heights, for an area too big for a crater a corner (decision 343): one every
    /// <c>Spacing</c> corners, each of <c>Radius</c>, kept far enough in from the area's edge that none moves a corner outside
    /// it. Their depths are a least-squares fit of the shape they sum to (depth x cos(90 degrees x distance / radius), game
    /// facts 11-09) to the heights, worked out in a few steps of conjugate gradients, each step passes over the craters and
    /// the cells, so a making can spread it over frames. The cells are the plan's: the area's corners and one more on each
    /// side, <c>wide</c> across.
    /// </summary>
    internal sealed class WideCraters
    {
        /// <summary>The corners between craters.</summary>
        internal const int Spacing = 4;

        /// <summary>A crater's radius: a corner it moves is less than this far from its centre (6 corners).</summary>
        internal const float Radius = 768f;

        /// <summary>The corners from the area's edge to its first craters: a crater moves nothing as far as its radius.</summary>
        internal const int Inset = 6;

        /// <summary>The steps of the fit. Measured in .NET on a 469 by 469 SnowyForest plan (11-14): 6 steps come within 2 of the full fit's typical error of 16.</summary>
        internal const int FitSteps = 6;

        private const float TileSize = 128f;

        /// <summary>The craters across and up.</summary>
        internal readonly int Across;

        internal readonly int Up;

        /// <summary>Each crater's depth, row by row from the bottom left, as fitted so far.</summary>
        internal readonly float[] Depths;

        /// <summary>The cell of the first crater.</summary>
        private readonly int first;

        private readonly int wide;

        /// <summary>The heights to fit, by cell, read only.</summary>
        private readonly float[] target;

        /// <summary>The heights the fit misses by, the shape of the step's direction, and the ground the fitted craters make, by cell.</summary>
        private readonly float[] residual;

        private readonly float[] product;

        /// <summary>The ground the fitted craters make, by cell; filled by Sum.</summary>
        internal readonly float[] Under;

        private readonly float[] gradient;
        private readonly float[] direction;

        /// <summary>The cells a crater moves, from its own, and by how much a unit of its depth.</summary>
        private readonly int[] offsets;

        private readonly float[] weights;

        private float gamma;
        private float alpha;
        private float productNorm;

        /// <summary>The craters for an area of so many corners, its cells a margin of one round them.</summary>
        internal WideCraters(int columns, int rows, float[] cells)
        {
            wide = columns + 2;
            Across = (columns - 1 - 2 * Inset) / Spacing + 1;
            Up = (rows - 1 - 2 * Inset) / Spacing + 1;
            first = (Inset + 1) * wide + Inset + 1;
            target = cells;
            residual = new float[cells.Length];
            product = new float[cells.Length];
            Under = new float[cells.Length];
            Depths = new float[Across * Up];
            gradient = new float[Depths.Length];
            direction = new float[Depths.Length];
            var reach = Inset - 1;
            var count = 0;
            var cellOffsets = new int[(2 * reach + 1) * (2 * reach + 1)];
            var cellWeights = new float[cellOffsets.Length];
            for (var dj = -reach; dj <= reach; dj++)
            {
                for (var di = -reach; di <= reach; di++)
                {
                    var d = TileSize * Maths.Sqrt(di * di + dj * dj);
                    if (d < Radius)
                    {
                        cellOffsets[count] = dj * wide + di;
                        cellWeights[count] = Angles.Cos(90f * d / Radius);
                        count++;
                    }
                }
            }

            offsets = new int[count];
            weights = new float[count];
            for (var k = 0; k < count; k++)
            {
                offsets[k] = cellOffsets[k];
                weights[k] = cellWeights[k];
            }
        }

        /// <summary>Whether an area of so many corners across and up holds at least one crater.</summary>
        internal static bool Fits(int columns, int rows)
        {
            return columns > 2 * Inset && rows > 2 * Inset;
        }

        /// <summary>The craters.</summary>
        internal int Count => Depths.Length;

        /// <summary>The cells a crater moves.</summary>
        internal int Reach => offsets.Length;

        /// <summary>The cell of a crater's centre.</summary>
        internal int CellOf(int m)
        {
            return first + (m / Across) * Spacing * wide + (m % Across) * Spacing;
        }

        /// <summary>
        /// The first pass of a step, over craters: how the miss leans on each crater (the first step reads the heights, as
        /// nothing is fitted yet); at its end the step's direction is set.
        /// </summary>
        internal void Gather(int step, int from, int to)
        {
            var source = step == 0 ? target : residual;
            for (var m = from; m < to; m++)
            {
                var at = CellOf(m);
                var sum = 0f;
                for (var k = 0; k < offsets.Length; k++)
                {
                    sum += weights[k] * source[at + offsets[k]];
                }

                gradient[m] = sum;
            }

            if (to < Count)
            {
                return;
            }

            var next = 0f;
            for (var m = 0; m < Count; m++)
            {
                next += gradient[m] * gradient[m];
            }

            var beta = step == 0 || !(gamma > 0f) ? 0f : next / gamma;
            gamma = next;
            for (var m = 0; m < Count; m++)
            {
                direction[m] = gradient[m] + beta * direction[m];
            }
        }

        /// <summary>The second pass of a step, over craters: the shape the step's direction makes.</summary>
        internal void Scatter(int from, int to)
        {
            Spread(direction, product, from, to);
        }

        /// <summary>The third, over cells: how big that shape is; at its end, how far to go along the direction, and the depths moved.</summary>
        internal void Measure(int from, int to)
        {
            if (from == 0)
            {
                productNorm = 0f;
            }

            for (var at = from; at < to; at++)
            {
                productNorm += product[at] * product[at];
            }

            if (to < product.Length)
            {
                return;
            }

            alpha = productNorm > 0f ? gamma / productNorm : 0f;
            for (var m = 0; m < Count; m++)
            {
                Depths[m] += alpha * direction[m];
            }
        }

        /// <summary>The last, over cells: the miss after the step, and the shape cleared for the next.</summary>
        internal void Update(int step, int from, int to)
        {
            for (var at = from; at < to; at++)
            {
                residual[at] = (step == 0 ? target[at] : residual[at]) - alpha * product[at];
                product[at] = 0f;
            }
        }

        /// <summary>After the steps, over craters: the ground the fitted craters make.</summary>
        internal void Sum(int from, int to)
        {
            Spread(Depths, Under, from, to);
        }

        /// <summary>Works the whole fit out at once.</summary>
        internal void All()
        {
            for (var step = 0; step < FitSteps; step++)
            {
                Gather(step, 0, Count);
                Scatter(0, Count);
                Measure(0, product.Length);
                Update(step, 0, product.Length);
            }

            Sum(0, Count);
        }

        /// <summary>Adds the shape of craters of some depths to a grid of cells.</summary>
        private void Spread(float[] depths, float[] into, int from, int to)
        {
            for (var m = from; m < to; m++)
            {
                var depth = depths[m];
                if (depth == 0f)
                {
                    continue;
                }

                var at = CellOf(m);
                for (var k = 0; k < offsets.Length; k++)
                {
                    into[at + offsets[k]] += weights[k] * depth;
                }
            }
        }
    }
}
