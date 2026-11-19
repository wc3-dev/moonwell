using System.Buffers.Binary;
using System.IO.Compression;
using Moonwell.BuildTime;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>The cell helpers build code measures a map with (decision 376), on small grids drawn in the test.</summary>
    public sealed class CellsTests
    {
        [Fact]
        public void a_cell_is_found_by_world_coordinates_from_the_grids_bottom_left()
        {
            var cells = Grid(-64f, -32f,
                "....",
                "....",
                "....");

            Assert.Equal(12, cells.Count);
            Assert.True(cells.Contains(-64f, -32f));
            Assert.True(cells.Contains(63.9f, 63.9f));
            Assert.False(cells.Contains(64f, 0f));
            Assert.False(cells.Contains(0f, 64f));
            Assert.False(cells.Contains(-64.1f, 0f));

            var first = cells.First();
            Assert.Equal((0, 0, -48f, -16f), (first.Column, first.Row, first.X, first.Y));
            var last = cells.Last();
            Assert.Equal((3, 2, 48f, 48f), (last.Column, last.Row, last.X, last.Y));
        }

        [Fact]
        public void a_cells_tests_read_the_pathing_byte_in_which_a_set_flag_means_cannot()
        {
            // Dry ground, deep water, dry unbuildable ground, the map's boundary, and blighted ground, as World Editor writes them.
            var cells = Grid(0f, 0f, ".~#Xb").ToList();

            Assert.Equal((true, true, true, false, false), Tests(cells[0]));
            Assert.Equal((false, true, false, true, false), Tests(cells[1]));
            Assert.Equal((true, true, false, false, false), Tests(cells[2]));
            Assert.Equal((false, false, false, false, false), Tests(cells[3]));
            Assert.Equal((true, true, true, false, true), Tests(cells[4]));
            Assert.Equal(War3Net.Build.Environment.PathingType.Walk | War3Net.Build.Environment.PathingType.Build, cells[1].Flags);
        }

        [Fact]
        public void in_takes_the_cells_whose_centres_are_inside()
        {
            var cells = Grid(0f, 0f,
                "....",
                "....",
                "....");

            Assert.Equal(2, cells.In(0f, 0f, 64f, 32f).Count);
            Assert.Equal(12, cells.In(-1000f, -1000f, 1000f, 1000f).Count);
            Assert.Equal(0, cells.In(200f, 0f, 300f, 96f).Count);

            // A side through a cell: the cell counts when its centre (16 in) is inside.
            Assert.Equal(1, cells.In(10f, 10f, 40f, 40f).Count);
            Assert.Equal(0, cells.In(17f, 17f, 40f, 40f).Count);

            var region = new War3Net.Build.Environment.Region { Name = "Yard", Left = 32f, Bottom = 32f, Right = 96f, Top = 96f };
            Assert.Equal(new[] { (1, 1), (2, 1), (1, 2), (2, 2) }, cells.In(region).Select(cell => (cell.Column, cell.Row)));
        }

        [Fact]
        public void where_and_or_and_except_give_new_sets()
        {
            var cells = Grid(0f, 0f,
                "..~~",
                ".##~");
            var water = cells.Where(cell => cell.IsWater);
            var buildable = cells.Where(cell => cell.CanBuild);

            Assert.Equal(3, water.Count);
            Assert.Equal(3, buildable.Count);
            Assert.Equal(8, cells.Count);
            Assert.Equal(0, water.And(buildable).Count);
            Assert.Equal(6, water.Or(buildable).Count);
            Assert.Equal(5, cells.Except(water).Count);
            Assert.True(cells.Except(water).Except(buildable).All(cell => !cell.CanBuild && !cell.IsWater));
            Assert.True(water.IsEmpty == false && water.And(buildable).IsEmpty);
        }

        [Fact]
        public void a_flood_fill_takes_the_cells_joined_side_by_side()
        {
            var cells = Grid(0f, 0f,
                "..X..",
                "..X..",
                ".X...");
            var walkable = cells.Where(cell => cell.CanWalk);

            // The top left room: the cell under it touches the bottom middle only corner to corner.
            Assert.Equal(5, walkable.FloodFrom(16f, 80f).Count);
            Assert.Equal(7, walkable.FloodFrom(144f, 80f).Count);
            Assert.False(walkable.FloodFrom(16f, 80f).Contains(144f, 80f));
            Assert.True(walkable.FloodFrom(16f, 80f).Contains(16f, 16f));

            // From a cell that is not in the set, or from outside the map, nothing is reached.
            Assert.True(walkable.FloodFrom(80f, 80f).IsEmpty);
            Assert.True(walkable.FloodFrom(-500f, 0f).IsEmpty);
        }

        [Fact]
        public void distances_are_straight_lines_between_centres()
        {
            var cells = Grid(0f, 0f,
                "....~",
                ".....",
                ".....",
                ".....",
                ".....");
            var water = cells.Where(cell => cell.IsWater);
            var toWater = water.Distances();

            Assert.Equal(0f, toWater.At(144f, 144f));
            Assert.Equal(32f, toWater.At(112f, 144f));

            // Three cells left and four down: a 3-4-5 triangle of 32s.
            Assert.Equal(160f, toWater.At(48f, 16f), 3);
            Assert.Equal(160f, toWater[cells.Single(cell => cell.Column == 1 && cell.Row == 0)], 3);
            Assert.Equal(float.PositiveInfinity, toWater.At(-100f, 0f));

            var corner = cells.In(0f, 0f, 64f, 64f);
            Assert.Equal(MathF.Sqrt((3 * 3) + (3 * 3)) * 32f, corner.DistanceTo(water), 3);
            Assert.Equal(0f, cells.DistanceTo(water));
            Assert.Equal(float.PositiveInfinity, corner.DistanceTo(cells.Where(cell => cell.IsBlighted)));
            Assert.Equal(float.PositiveInfinity, cells.Where(cell => cell.IsBlighted).DistanceTo(water));
        }

        [Fact]
        public void distances_match_a_search_of_every_pair()
        {
            var random = new Random(7);
            var flags = new byte[23 * 17];
            for (var index = 0; index < flags.Length; index++)
            {
                flags[index] = random.Next(9) == 0 ? (byte)0x0A : (byte)0x40;
            }

            var cells = Cells.All(new PathingGrid(23, 17, -200f, 300f, flags));
            var water = cells.Where(cell => cell.IsWater).ToList();
            var toWater = cells.Where(cell => cell.IsWater).Distances();
            foreach (var cell in cells)
            {
                var nearest = water.Min(one => MathF.Sqrt(((one.X - cell.X) * (one.X - cell.X)) + ((one.Y - cell.Y) * (one.Y - cell.Y))));
                Assert.Equal(nearest, toWater[cell], 2);
            }
        }

        [Fact]
        public void the_largest_rectangle_is_the_one_of_most_cells()
        {
            var cells = Grid(-64f, 0f,
                "..X...",
                "..X...",
                "......",
                "X.....");
            var free = cells.Where(cell => cell.CanWalk).LargestRectangle();

            // Columns 3 to 5 over all four rows: 12 cells, more than the 10 of the two full rows' five.
            Assert.Equal((3, 4), (free.Columns, free.Rows));
            Assert.Equal((32f, 0f, 128f, 128f), (free.Left, free.Bottom, free.Right, free.Top));
            Assert.Equal((96f, 128f, 80f, 64f), (free.Width, free.Height, free.CenterX, free.CenterY));
            Assert.False(free.IsEmpty);
        }

        [Fact]
        public void of_rectangles_as_large_the_lowest_then_the_leftmost_wins()
        {
            var cells = Grid(0f, 0f,
                "..X..",
                "..X..",
                "XXXXX",
                "..X..",
                "..X..");
            var free = cells.Where(cell => cell.CanWalk).LargestRectangle();

            Assert.Equal((0f, 0f, 2, 2), (free.Left, free.Bottom, free.Columns, free.Rows));
            Assert.True(cells.Where(cell => cell.IsBlighted).LargestRectangle().IsEmpty);
        }

        [Fact]
        public void a_picture_is_a_png_with_a_pixel_a_cell_and_north_at_the_top()
        {
            var cells = Grid(0f, 0f,
                "~..",
                "...");
            var png = new Picture(cells.Grid).Fill(cells.Where(cell => cell.IsWater), System.Drawing.Color.FromArgb(10, 20, 30)).ToPng();

            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png.Take(8));
            Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
            Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
            Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));

            // The first chunk after the header is the pixels: a filter byte, then the top row, whose first pixel is the water.
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(33));
            Assert.Equal("IDAT", System.Text.Encoding.ASCII.GetString(png, 37, 4));
            using var unpacked = new MemoryStream();
            using (var packed = new ZLibStream(new MemoryStream(png, 41, length), CompressionMode.Decompress))
            {
                packed.CopyTo(unpacked);
            }

            Assert.Equal(new byte[] { 0, 10, 20, 30, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, unpacked.ToArray());
        }

        private static (bool Walk, bool Fly, bool Build, bool Water, bool Blight) Tests(Cell cell)
        {
            return (cell.CanWalk, cell.CanFly, cell.CanBuild, cell.IsWater, cell.IsBlighted);
        }

        // Every cell of a grid drawn top row first: . dry ground, ~ deep water, # dry unbuildable ground, X the boundary, b blight.
        private static Cells Grid(float left, float bottom, params string[] rows)
        {
            var columns = rows[0].Length;
            var flags = new byte[columns * rows.Length];
            for (var row = 0; row < rows.Length; row++)
            {
                var line = rows[rows.Length - 1 - row];
                for (var column = 0; column < columns; column++)
                {
                    flags[(row * columns) + column] = line[column] switch
                    {
                        '.' => 0x40,
                        '~' => 0x0A,
                        '#' => 0x48,
                        'X' => 0xCE,
                        'b' => 0x60,
                        _ => throw new ArgumentException("no such cell: " + line[column]),
                    };
                }
            }

            return Cells.All(new PathingGrid(columns, rows.Length, left, bottom, flags));
        }
    }
}
