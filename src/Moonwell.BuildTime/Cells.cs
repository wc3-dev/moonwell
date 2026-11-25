using System.Collections;
using War3Net.Build.Environment;

namespace Moonwell.BuildTime
{
    /// <summary>
    /// A set of a map's pathing cells, each 32 by 32, found by world coordinates. <see cref="MapReader.Cells"/> is every cell of
    /// the map; <see cref="In(Region)"/> and <see cref="Where"/> narrow it, and a set never changes: each call gives a new one.
    /// </summary>
    /// <remarks>
    /// The pathing file holds the ground, cliffs, water, doodads and bridges as World Editor saved them. Trees and other
    /// destructables, and placed units and buildings, are not in it: the game adds those while it runs.
    /// </remarks>
    public sealed class Cells : IEnumerable<Cell>
    {
        private readonly PathingGrid grid;

        private readonly bool[] inside;

        private Distances? distances;

        private Cells(PathingGrid grid, bool[] inside)
        {
            this.grid = grid;
            this.inside = inside;
            Count = inside.Count(one => one);
        }

        /// <summary>
        /// A set of no cells, of any map: a start to add to with <see cref="Or"/>. Every call on it gives an empty set, and
        /// <c>Cells.Empty.Or(cells)</c> gives <c>cells</c>.
        /// </summary>
        public static Cells Empty { get; } = new Cells(new PathingGrid(0, 0, 0f, 0f, Array.Empty<byte>()), Array.Empty<bool>());

        /// <summary>How many cells the set holds; a cell is 32 by 32, so 16 of them are one 128 tile.</summary>
        public int Count { get; }

        /// <summary>Whether the set holds no cell.</summary>
        public bool IsEmpty => Count == 0;

        /// <summary>Whether the cell a point is in is one of the set's.</summary>
        /// <param name="x">The point's x.</param>
        /// <param name="y">The point's y.</param>
        public bool Contains(float x, float y)
        {
            var index = grid.IndexAt(x, y);
            return index >= 0 && inside[index];
        }

        /// <summary>Whether a cell is one of the set's.</summary>
        /// <param name="cell">A cell of the same map.</param>
        public bool Contains(Cell cell)
        {
            return Holds(cell.Index);
        }

        /// <summary>The set's cells inside a region, by their centres.</summary>
        /// <param name="region">A region of the map, such as <c>Map.Region("Harbor")</c>.</param>
        public Cells In(Region region)
        {
            return In(region.Left, region.Bottom, region.Right, region.Top);
        }

        /// <summary>The set's cells inside a rectangle, by their centres.</summary>
        /// <param name="left">The rectangle's left side.</param>
        /// <param name="bottom">The rectangle's bottom side.</param>
        /// <param name="right">The rectangle's right side.</param>
        /// <param name="top">The rectangle's top side.</param>
        public Cells In(float left, float bottom, float right, float top)
        {
            // A centre is half a cell in from its cell's corner: the first and last column and row whose centres are inside.
            static int First(float side, float edge) => (int)MathF.Ceiling(((side - edge) / PathingGrid.CellSize) - 0.5f);
            var kept = new bool[inside.Length];
            var lastColumn = Math.Min(grid.Columns, First(right, grid.Left)) - 1;
            var lastRow = Math.Min(grid.Rows, First(top, grid.Bottom)) - 1;
            for (var row = Math.Max(0, First(bottom, grid.Bottom)); row <= lastRow; row++)
            {
                for (var column = Math.Max(0, First(left, grid.Left)); column <= lastColumn; column++)
                {
                    var index = (row * grid.Columns) + column;
                    kept[index] = inside[index];
                }
            }

            return new Cells(grid, kept);
        }

        /// <summary>The set's cells that pass a test: <c>cells.Where(cell => cell.CanBuild)</c>.</summary>
        /// <param name="test">What a cell must pass to be kept.</param>
        public Cells Where(Func<Cell, bool> test)
        {
            var kept = new bool[inside.Length];
            for (var index = 0; index < inside.Length; index++)
            {
                kept[index] = inside[index] && test(new Cell(grid, index));
            }

            return new Cells(grid, kept);
        }

        /// <summary>The cells in both this set and another.</summary>
        /// <param name="other">A set of the same map's cells.</param>
        public Cells And(Cells other)
        {
            return Where(cell => other.Holds(cell.Index));
        }

        /// <summary>The cells in this set, another, or both.</summary>
        /// <param name="other">A set of the same map's cells.</param>
        public Cells Or(Cells other)
        {
            if (IsBlank)
            {
                return other;
            }

            if (other.IsBlank)
            {
                return this;
            }

            return new Cells(grid, inside.Select((one, index) => one || other.inside[index]).ToArray());
        }

        /// <summary>The cells in this set that are not in another.</summary>
        /// <param name="other">A set of the same map's cells.</param>
        public Cells Except(Cells other)
        {
            return Where(cell => !other.Holds(cell.Index));
        }

        /// <summary>
        /// The set's cells joined to a point: the cell the point is in and every cell reached from it by steps to a cell beside it,
        /// left, right, up or down, without leaving the set. Empty when the point's cell is not in the set.
        /// </summary>
        /// <param name="x">The point's x.</param>
        /// <param name="y">The point's y.</param>
        public Cells FloodFrom(float x, float y)
        {
            var start = grid.IndexAt(x, y);
            return Flood(start < 0 ? Array.Empty<int>() : new[] { start });
        }

        /// <summary>
        /// The set's cells joined to any cell of another set: every cell of this set that is in the other and every cell reached
        /// from those by steps to a cell beside it, left, right, up or down, without leaving this set. The whole of each piece
        /// of this set that touches the other, in one call; empty when the two share no cell.
        /// </summary>
        /// <param name="from">A set of the same map's cells to start from, such as <c>ground.In(region)</c>.</param>
        public Cells FloodFrom(Cells from)
        {
            var starts = new List<int>();
            for (var index = 0; index < inside.Length; index++)
            {
                if (from.Holds(index))
                {
                    starts.Add(index);
                }
            }

            return Flood(starts);
        }

        // The cells of the set reached from the starts that are in it.
        private Cells Flood(IReadOnlyList<int> starts)
        {
            var reached = new bool[inside.Length];
            var waiting = new Stack<int>();
            foreach (var start in starts)
            {
                if (inside[start] && !reached[start])
                {
                    reached[start] = true;
                    waiting.Push(start);
                }
            }

            void Visit(int index)
            {
                if (inside[index] && !reached[index])
                {
                    reached[index] = true;
                    waiting.Push(index);
                }
            }

            while (waiting.Count > 0)
            {
                var index = waiting.Pop();
                var column = index % grid.Columns;
                if (column > 0)
                {
                    Visit(index - 1);
                }

                if (column < grid.Columns - 1)
                {
                    Visit(index + 1);
                }

                if (index >= grid.Columns)
                {
                    Visit(index - grid.Columns);
                }

                if (index < inside.Length - grid.Columns)
                {
                    Visit(index + grid.Columns);
                }
            }

            return new Cells(grid, reached);
        }

        /// <summary>How far every cell of the map is from the nearest cell of this set, in a straight line between centres.</summary>
        /// <returns>The distances, to read by cell or by point: <c>var toWater = water.Distances(); toWater.At(x, y)</c>.</returns>
        public Distances Distances()
        {
            // A set never changes, so its distances are worked out once: asking how far each of many regions is from the same water is cheap.
            return distances ??= new Distances(grid, inside);
        }

        /// <summary>The shortest straight line from a cell of this set to a cell of another, between centres; infinite when either is empty.</summary>
        /// <param name="other">A set of the same map's cells, such as its water.</param>
        public float DistanceTo(Cells other)
        {
            if (IsEmpty || other.IsEmpty)
            {
                return float.PositiveInfinity;
            }

            var distances = other.Distances();
            var nearest = float.PositiveInfinity;
            for (var index = 0; index < inside.Length; index++)
            {
                if (inside[index])
                {
                    nearest = MathF.Min(nearest, distances[index]);
                }
            }

            return nearest;
        }

        /// <summary>
        /// The largest rectangle of cells that fits in the set, its sides along the map's; of several as large, the one met first
        /// going up the map from its bottom left, so the same map gives the same one. Empty when the set is.
        /// </summary>
        public CellRectangle LargestRectangle()
        {
            // The tallest run of cells ending at each column of the row at hand, and for each row the widest rectangle under it.
            var heights = new int[grid.Columns + 1];
            var open = new Stack<int>();
            var best = default(CellRectangle);
            for (var row = 0; row < grid.Rows; row++)
            {
                for (var column = 0; column < grid.Columns; column++)
                {
                    heights[column] = inside[(row * grid.Columns) + column] ? heights[column] + 1 : 0;
                }

                open.Clear();
                for (var column = 0; column <= grid.Columns; column++)
                {
                    while (open.Count > 0 && heights[open.Peek()] > heights[column])
                    {
                        var height = heights[open.Pop()];
                        var left = open.Count == 0 ? 0 : open.Peek() + 1;
                        var width = column - left;
                        if (width * height > best.Columns * best.Rows)
                        {
                            best = new CellRectangle(grid, left, row - height + 1, width, height);
                        }
                    }

                    open.Push(column);
                }
            }

            return best;
        }

        /// <summary>The set's cells, row by row from the bottom left.</summary>
        public IEnumerator<Cell> GetEnumerator()
        {
            for (var index = 0; index < inside.Length; index++)
            {
                if (inside[index])
                {
                    yield return new Cell(grid, index);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal static Cells All(PathingGrid grid)
        {
            var every = new bool[grid.Flags.Length];
            Array.Fill(every, true);
            return new Cells(grid, every);
        }

        internal PathingGrid Grid => grid;

        // Whether the set holds the cell of an index; false past its end, as Empty holds no cell of any map.
        internal bool Holds(int index) => index < inside.Length && inside[index];

        // Empty, or a set of no map: it adds nothing to another set and takes nothing from one.
        private bool IsBlank => inside.Length == 0;
    }

    /// <summary>One 32 by 32 cell of a map's pathing, as World Editor saved it.</summary>
    public readonly struct Cell
    {
        private readonly PathingGrid grid;

        internal Cell(PathingGrid grid, int index)
        {
            this.grid = grid;
            Index = index;
        }

        /// <summary>The cell's column, 0 at the map's left edge.</summary>
        public int Column => Index % grid.Columns;

        /// <summary>The cell's row, 0 at the map's bottom edge.</summary>
        public int Row => Index / grid.Columns;

        /// <summary>The x of the cell's centre.</summary>
        public float X => grid.Left + (Column * PathingGrid.CellSize) + (PathingGrid.CellSize / 2f);

        /// <summary>The y of the cell's centre.</summary>
        public float Y => grid.Bottom + (Row * PathingGrid.CellSize) + (PathingGrid.CellSize / 2f);

        /// <summary>The cell's byte in the pathing file, in which a set flag means "cannot".</summary>
        public PathingType Flags => (PathingType)grid.Flags[Index];

        /// <summary>Whether ground units can walk here.</summary>
        public bool CanWalk => (grid.Flags[Index] & 0x02) == 0;

        /// <summary>Whether air units can fly here.</summary>
        public bool CanFly => (grid.Flags[Index] & 0x04) == 0;

        /// <summary>Whether a building can be placed here, as far as the ground goes.</summary>
        public bool CanBuild => (grid.Flags[Index] & 0x08) == 0;

        /// <summary>Whether this is water a boat floats on, shallow or deep.</summary>
        public bool IsWater => (grid.Flags[Index] & 0x40) == 0;

        /// <summary>Whether the ground here is blighted.</summary>
        public bool IsBlighted => (grid.Flags[Index] & 0x20) != 0;

        internal int Index { get; }
    }

    /// <summary>A rectangle of pathing cells, in world coordinates and in cells.</summary>
    public readonly struct CellRectangle
    {
        internal CellRectangle(PathingGrid grid, int column, int row, int columns, int rows)
        {
            Left = grid.Left + (column * PathingGrid.CellSize);
            Bottom = grid.Bottom + (row * PathingGrid.CellSize);
            Columns = columns;
            Rows = rows;
        }

        /// <summary>The rectangle's left side.</summary>
        public float Left { get; }

        /// <summary>The rectangle's bottom side.</summary>
        public float Bottom { get; }

        /// <summary>The rectangle's right side.</summary>
        public float Right => Left + Width;

        /// <summary>The rectangle's top side.</summary>
        public float Top => Bottom + Height;

        /// <summary>How many cells wide it is.</summary>
        public int Columns { get; }

        /// <summary>How many cells high it is.</summary>
        public int Rows { get; }

        /// <summary>Its width in world units, 32 a cell.</summary>
        public float Width => Columns * PathingGrid.CellSize;

        /// <summary>Its height in world units, 32 a cell.</summary>
        public float Height => Rows * PathingGrid.CellSize;

        /// <summary>The x of its middle.</summary>
        public float CenterX => Left + (Width / 2f);

        /// <summary>The y of its middle.</summary>
        public float CenterY => Bottom + (Height / 2f);

        /// <summary>Whether it holds no cell.</summary>
        public bool IsEmpty => Columns == 0 || Rows == 0;
    }

    /// <summary>How far each cell of a map is from the nearest cell of a set, in world units between centres.</summary>
    public sealed class Distances
    {
        private readonly PathingGrid grid;

        private readonly float[] distances;

        // Felzenszwalb and Huttenlocher's exact distance transform: along each column, then along each row.
        internal Distances(PathingGrid grid, bool[] from)
        {
            this.grid = grid;
            var columns = grid.Columns;
            var rows = grid.Rows;
            var squared = new double[from.Length];
            var longest = Math.Max(columns, rows);
            var line = new double[longest];
            var result = new double[longest];
            var hulls = new int[longest];
            var bounds = new double[longest + 1];
            for (var column = 0; column < columns; column++)
            {
                for (var row = 0; row < rows; row++)
                {
                    line[row] = from[(row * columns) + column] ? 0d : Far;
                }

                Along(line, result, rows, hulls, bounds);
                for (var row = 0; row < rows; row++)
                {
                    squared[(row * columns) + column] = result[row];
                }
            }

            distances = new float[from.Length];
            for (var row = 0; row < rows; row++)
            {
                Array.Copy(squared, row * columns, line, 0, columns);
                Along(line, result, columns, hulls, bounds);
                for (var column = 0; column < columns; column++)
                {
                    distances[(row * columns) + column] = result[column] >= Far ? float.PositiveInfinity : (float)(Math.Sqrt(result[column]) * PathingGrid.CellSize);
                }
            }
        }

        private const double Far = 1e18;

        /// <summary>The distance from a cell to the nearest cell of the set; 0 for a cell of the set, infinite when the set is empty.</summary>
        /// <param name="cell">A cell of the same map.</param>
        public float this[Cell cell] => cell.Index < distances.Length ? distances[cell.Index] : float.PositiveInfinity;

        internal float this[int index] => distances[index];

        /// <summary>The distance from the cell a point is in to the nearest cell of the set; infinite outside the map.</summary>
        /// <param name="x">The point's x.</param>
        /// <param name="y">The point's y.</param>
        public float At(float x, float y)
        {
            var index = grid.IndexAt(x, y);
            return index < 0 ? float.PositiveInfinity : distances[index];
        }

        // The squared distance, along one line, from each place to the nearest place of the lower envelope of parabolas.
        private static void Along(double[] line, double[] result, int count, int[] hulls, double[] bounds)
        {
            var last = 0;
            hulls[0] = 0;
            bounds[0] = double.NegativeInfinity;
            bounds[1] = double.PositiveInfinity;
            for (var at = 1; at < count; at++)
            {
                double meets;
                while (true)
                {
                    var hull = hulls[last];
                    meets = ((line[at] + ((double)at * at)) - (line[hull] + ((double)hull * hull))) / ((2d * at) - (2d * hull));
                    if (meets > bounds[last])
                    {
                        break;
                    }

                    last--;
                }

                last++;
                hulls[last] = at;
                bounds[last] = meets;
                bounds[last + 1] = double.PositiveInfinity;
            }

            last = 0;
            for (var at = 0; at < count; at++)
            {
                while (bounds[last + 1] < at)
                {
                    last++;
                }

                var hull = hulls[last];
                result[at] = (((double)at - hull) * (at - hull)) + line[hull];
            }
        }
    }

    // A map's pathing as one byte a cell, row 0 at the bottom, and where its bottom left corner is in the world.
    internal sealed class PathingGrid
    {
        public const float CellSize = 32f;

        public PathingGrid(MapPathingMap pathing, MapEnvironment ground)
            : this((int)pathing.Width, (int)pathing.Height, ground.Left, ground.Bottom, pathing.Cells.Select(cell => (byte)cell).ToArray())
        {
        }

        public PathingGrid(int columns, int rows, float left, float bottom, byte[] flags)
        {
            if (flags.Length != columns * rows)
            {
                throw new BuildFailure($"war3map.wpm lists {flags.Length} cells, but says it is {columns} by {rows}");
            }

            Columns = columns;
            Rows = rows;
            Left = left;
            Bottom = bottom;
            Flags = flags;
        }

        public int Columns { get; }

        public int Rows { get; }

        public float Left { get; }

        public float Bottom { get; }

        public byte[] Flags { get; }

        // The index of the cell a point is in, or -1 outside the map.
        public int IndexAt(float x, float y)
        {
            var column = (int)MathF.Floor((x - Left) / CellSize);
            var row = (int)MathF.Floor((y - Bottom) / CellSize);
            return column < 0 || row < 0 || column >= Columns || row >= Rows ? -1 : (row * Columns) + column;
        }
    }
}
