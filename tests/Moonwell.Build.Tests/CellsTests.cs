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

        [Fact]
        public void a_flood_fill_from_a_set_takes_every_piece_the_set_touches()
        {
            var cells = Grid(0f, 0f,
                "..X..X.",
                "..X..X.",
                ".XX..X.");
            var walkable = cells.Where(cell => cell.CanWalk);

            // Starting cells in the left room and the right strip, one of them on the boundary, which is not in the set.
            var starts = cells.In(0f, 64f, 32f, 96f).Or(cells.In(192f, 0f, 224f, 32f)).Or(cells.In(64f, 64f, 96f, 96f));
            var reached = walkable.FloodFrom(starts);

            Assert.Equal(5 + 3, reached.Count);
            Assert.True(reached.Contains(16f, 16f));
            Assert.True(reached.Contains(208f, 80f));
            Assert.False(reached.Contains(112f, 48f));

            // The same as flooding from each start and joining the pieces.
            Assert.Equal(walkable.FloodFrom(16f, 80f).Or(walkable.FloodFrom(208f, 16f)).ToList(), reached.ToList());
            Assert.True(walkable.FloodFrom(cells.Where(cell => !cell.CanWalk)).IsEmpty);
            Assert.True(walkable.FloodFrom(Cells.Empty).IsEmpty);
        }

        [Fact]
        public void the_empty_set_is_a_start_to_add_to_on_any_map()
        {
            var cells = Grid(0f, 0f,
                "..~",
                "...");
            var water = cells.Where(cell => cell.IsWater);

            Assert.True(Cells.Empty.IsEmpty);
            Assert.Equal(0, Cells.Empty.Count);
            Assert.Same(water, Cells.Empty.Or(water));
            Assert.Same(water, water.Or(Cells.Empty));
            Assert.Equal(water.ToList(), water.Except(Cells.Empty).ToList());
            Assert.True(water.And(Cells.Empty).IsEmpty);
            Assert.True(Cells.Empty.And(water).IsEmpty);
            Assert.True(Cells.Empty.Except(water).IsEmpty);
            Assert.True(Cells.Empty.In(-1000f, -1000f, 1000f, 1000f).IsEmpty);
            Assert.True(Cells.Empty.Where(cell => true).IsEmpty);
            Assert.True(Cells.Empty.FloodFrom(water).IsEmpty);
            Assert.True(Cells.Empty.FloodFrom(16f, 16f).IsEmpty);
            Assert.False(Cells.Empty.Contains(16f, 16f));
            Assert.False(Cells.Empty.Contains(water.First()));
            Assert.Empty(Cells.Empty);
            Assert.True(Cells.Empty.LargestRectangle().IsEmpty);
            Assert.Equal(float.PositiveInfinity, water.DistanceTo(Cells.Empty));
            Assert.Equal(float.PositiveInfinity, Cells.Empty.DistanceTo(water));
            Assert.Equal(float.PositiveInfinity, Cells.Empty.Distances()[water.First()]);

            // Joined piece by piece from nothing, as a loop over regions does.
            var joined = Cells.Empty;
            foreach (var cell in water)
            {
                joined = joined.Or(cells.FloodFrom(cell.X, cell.Y).And(water));
            }

            Assert.Equal(water.ToList(), joined.ToList());
        }

        [Fact]
        public void a_picture_of_a_part_takes_the_cells_it_reaches_into_at_a_scale()
        {
            var cells = Grid(-64f, -32f,
                "....",
                "~...",
                "....");

            // From the middle of the first column to just into the third, and the second row only.
            var picture = new Picture(cells.Grid, -50f, 0f, 1f, 32f, 3).Fill(cells.Where(cell => cell.IsWater), System.Drawing.Color.FromArgb(10, 20, 30));

            Assert.Equal((-64f, 0f, 32f, 32f), (picture.Left, picture.Bottom, picture.Right, picture.Top));
            Assert.Equal((9, 3, 3), (picture.Width, picture.Height, picture.Scale));
            var (width, height, pixels) = Pixels(picture.ToPng());
            Assert.Equal((9, 3), (width, height));

            // The water cell is the first three pixels of each of the three rows; the rest is black.
            for (var row = 0; row < 3; row++)
            {
                for (var x = 0; x < 9; x++)
                {
                    Assert.Equal(x < 3 ? (10, 20, 30) : (0, 0, 0), Pixel(pixels, width, x, row));
                }
            }
        }

        [Fact]
        public void a_picture_of_a_part_is_kept_to_the_map_and_refuses_what_shows_nothing()
        {
            var cells = Grid(0f, 0f,
                "...",
                "...");

            var kept = new Picture(cells.Grid, -1000f, -1000f, 1000f, 1000f, 2);
            Assert.Equal((0f, 0f, 96f, 64f, 6, 4), (kept.Left, kept.Bottom, kept.Right, kept.Top, kept.Width, kept.Height));

            Assert.Contains("off the map", Assert.Throws<BuildFailure>(() => new Picture(cells.Grid, 200f, 0f, 300f, 64f, 1)).Message);
            Assert.Contains("not past its left", Assert.Throws<BuildFailure>(() => new Picture(cells.Grid, 50f, 0f, 50f, 64f, 1)).Message);
            Assert.Contains("1 and up", Assert.Throws<BuildFailure>(() => new Picture(cells.Grid, 0f, 0f, 64f, 64f, 0)).Message);
            Assert.Contains("more than the 4096", Assert.Throws<BuildFailure>(() => new Picture(cells.Grid, 0f, 0f, 96f, 64f, 2000)).Message);
        }

        [Fact]
        public void an_outline_is_a_line_a_pixel_wide_inside_the_sets_edge()
        {
            var cells = Grid(0f, 0f,
                "....",
                "....",
                "....");
            var white = System.Drawing.Color.White;

            // A region of two by two cells in the middle of the bottom, drawn at a scale of 3.
            var region = cells.In(32f, 0f, 96f, 64f);
            var (width, height, pixels) = Pixels(new Picture(cells.Grid, 0f, 0f, 128f, 96f, 3).Outline(region, white).ToPng());
            Assert.Equal((12, 9), (width, height));

            // From the bottom: the region's pixels are x 3 to 8 and rows 0 to 5; its line is their edge, the inside blank.
            for (var row = 0; row < 9; row++)
            {
                for (var x = 0; x < 12; x++)
                {
                    var inRegion = x >= 3 && x <= 8 && row <= 5;
                    var onEdge = inRegion && (x == 3 || x == 8 || row == 0 || row == 5);
                    Assert.True(onEdge == (Pixel(pixels, width, x, row) == (255, 255, 255)), $"pixel {x}, {row}");
                }
            }

            // At a scale of 1 the line is the set's cells at its edge; a cell inside a bigger set is not on it.
            var all = Pixels(new Picture(cells.Grid).Outline(cells, white).ToPng());
            Assert.Equal((0, 0, 0), Pixel(all.Pixels, all.Width, 1, 1));
            Assert.Equal((255, 255, 255), Pixel(all.Pixels, all.Width, 0, 1));

            // A picture cut through a set draws no line where the set goes on past the cut.
            var cut = Pixels(new Picture(cells.Grid, 0f, 0f, 64f, 96f, 1).Outline(cells, white).ToPng());
            Assert.Equal((0, 0, 0), Pixel(cut.Pixels, cut.Width, 1, 1));
        }

        // A PNG's size and its pixels, three bytes each, the bottom row first.
        private static (int Width, int Height, byte[] Pixels) Pixels(byte[] png)
        {
            var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16));
            var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20));
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(33));
            using var unpacked = new MemoryStream();
            using (var packed = new ZLibStream(new MemoryStream(png, 41, length), CompressionMode.Decompress))
            {
                packed.CopyTo(unpacked);
            }

            var lines = unpacked.ToArray();
            var pixels = new byte[width * height * 3];
            for (var line = 0; line < height; line++)
            {
                Array.Copy(lines, (line * ((width * 3) + 1)) + 1, pixels, (height - 1 - line) * width * 3, width * 3);
            }

            return (width, height, pixels);
        }

        private static (int, int, int) Pixel(byte[] pixels, int width, int x, int row)
        {
            var at = ((row * width) + x) * 3;
            return (pixels[at], pixels[at + 1], pixels[at + 2]);
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
