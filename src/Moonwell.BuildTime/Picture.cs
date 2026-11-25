using System.Buffers.Binary;
using System.Drawing;
using System.IO.Compression;

namespace Moonwell.BuildTime
{
    /// <summary>
    /// A picture of a map, or of a part of it, for the map maker to look at: a square of pixels for each pathing cell, one by
    /// one unless a scale is given, north at the top. It starts black; <see cref="Fill"/> colours a set of cells and
    /// <see cref="Outline"/> draws a line round one, later calls over earlier ones, and <see cref="BuildStep"/>'s
    /// <c>Report</c> writes it.
    /// </summary>
    public sealed class Picture
    {
        /// <summary>The most pixels a picture may be across or up.</summary>
        public const int MostPixels = 4096;

        private readonly PathingGrid grid;

        private readonly int firstColumn;

        private readonly int firstRow;

        private readonly int columns;

        private readonly int rows;

        // Three bytes a pixel, the bottom row first.
        private readonly byte[] pixels;

        /// <summary>Makes a black picture of the whole map, a pixel a cell.</summary>
        /// <param name="map">The map.</param>
        public Picture(MapReader map)
            : this(map.Cells.Grid)
        {
        }

        /// <summary>
        /// Makes a black picture of a part of the map: every cell the rectangle reaches into, kept to the map, each a square of
        /// <paramref name="scale"/> by <paramref name="scale"/> pixels. <see cref="Left"/>, <see cref="Bottom"/>,
        /// <see cref="Right"/> and <see cref="Top"/> say what it shows.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="left">The part's left side.</param>
        /// <param name="bottom">The part's bottom side.</param>
        /// <param name="right">The part's right side.</param>
        /// <param name="top">The part's top side.</param>
        /// <param name="scale">The pixels across a cell, 1 and up.</param>
        /// <exception cref="BuildFailure">The rectangle is empty or off the map, the scale is below 1, or the picture would be more than <see cref="MostPixels"/> across or up.</exception>
        public Picture(MapReader map, float left, float bottom, float right, float top, int scale = 1)
            : this(map.Cells.Grid, left, bottom, right, top, scale)
        {
        }

        internal Picture(PathingGrid grid)
            : this(grid, (0, 0, grid.Columns, grid.Rows), 1)
        {
        }

        internal Picture(PathingGrid grid, float left, float bottom, float right, float top, int scale)
            : this(grid, Area(grid, left, bottom, right, top), scale)
        {
        }

        private Picture(PathingGrid grid, (int Column, int Row, int Columns, int Rows) area, int scale)
        {
            if (scale < 1)
            {
                throw new BuildFailure($"a Picture's scale is the pixels across a cell, 1 and up, not {scale}");
            }

            if ((long)area.Columns * scale > MostPixels || (long)area.Rows * scale > MostPixels)
            {
                throw new BuildFailure(
                    $"a Picture of {area.Columns} by {area.Rows} cells at a scale of {scale} is {(long)area.Columns * scale} by {(long)area.Rows * scale} pixels, more than the {MostPixels} a side a picture may be: draw a smaller part or at a smaller scale");
            }

            this.grid = grid;
            firstColumn = area.Column;
            firstRow = area.Row;
            columns = area.Columns;
            rows = area.Rows;
            Scale = scale;
            pixels = new byte[Width * Height * 3];
        }

        /// <summary>The pixels across a cell.</summary>
        public int Scale { get; }

        /// <summary>The picture's width in pixels.</summary>
        public int Width => columns * Scale;

        /// <summary>The picture's height in pixels.</summary>
        public int Height => rows * Scale;

        /// <summary>The x of the picture's left edge, a cell's side.</summary>
        public float Left => grid.Left + (firstColumn * PathingGrid.CellSize);

        /// <summary>The y of the picture's bottom edge, a cell's side.</summary>
        public float Bottom => grid.Bottom + (firstRow * PathingGrid.CellSize);

        /// <summary>The x of the picture's right edge.</summary>
        public float Right => Left + (columns * PathingGrid.CellSize);

        /// <summary>The y of the picture's top edge.</summary>
        public float Top => Bottom + (rows * PathingGrid.CellSize);

        /// <summary>Colours the pixels of a set of cells.</summary>
        /// <param name="cells">The cells; those outside the picture are left out.</param>
        /// <param name="color">Their colour, such as <c>Color.Blue</c>.</param>
        /// <returns>The picture, to chain another call.</returns>
        public Picture Fill(Cells cells, Color color)
        {
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    if (cells.Holds(Index(column, row)))
                    {
                        Paint(column * Scale, row * Scale, Scale, Scale, color);
                    }
                }
            }

            return this;
        }

        /// <summary>
        /// Draws a line one pixel wide round a set of cells, such as a region's (<c>Map.Cells.In(region)</c>): along each side of
        /// a cell of the set that has no cell of the set beside it, or the map's edge, inside the cell. At a scale of 1 that is
        /// the set's cells at its edge; at a larger one a thin line over what is filled.
        /// </summary>
        /// <param name="cells">The cells; those outside the picture are left out.</param>
        /// <param name="color">The line's colour.</param>
        /// <returns>The picture, to chain another call.</returns>
        public Picture Outline(Cells cells, Color color)
        {
            bool Open(int column, int row) => column < 0 || row < 0 || column >= grid.Columns || row >= grid.Rows || !cells.Holds((row * grid.Columns) + column);
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    if (!cells.Holds(Index(column, row)))
                    {
                        continue;
                    }

                    var mapColumn = firstColumn + column;
                    var mapRow = firstRow + row;
                    var x = column * Scale;
                    var y = row * Scale;
                    if (Open(mapColumn - 1, mapRow))
                    {
                        Paint(x, y, 1, Scale, color);
                    }

                    if (Open(mapColumn + 1, mapRow))
                    {
                        Paint(x + Scale - 1, y, 1, Scale, color);
                    }

                    if (Open(mapColumn, mapRow - 1))
                    {
                        Paint(x, y, Scale, 1, color);
                    }

                    if (Open(mapColumn, mapRow + 1))
                    {
                        Paint(x, y + Scale - 1, Scale, 1, color);
                    }
                }
            }

            return this;
        }

        /// <summary>The picture as a PNG file's bytes.</summary>
        public byte[] ToPng()
        {
            // Each line of pixels, the top row first, behind a byte saying it is not filtered.
            var lines = new MemoryStream();
            using (var packed = new ZLibStream(lines, CompressionLevel.Optimal, true))
            {
                for (var row = Height - 1; row >= 0; row--)
                {
                    packed.WriteByte(0);
                    packed.Write(pixels, row * Width * 3, Width * 3);
                }
            }

            var header = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header, Width);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), Height);
            header[8] = 8;
            header[9] = 2;

            var png = new MemoryStream();
            png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            Chunk(png, "IHDR", header);
            Chunk(png, "IDAT", lines.ToArray());
            Chunk(png, "IEND", Array.Empty<byte>());
            return png.ToArray();
        }

        // The cells a rectangle reaches into, kept to the map: the first column and row, and how many of each.
        private static (int, int, int, int) Area(PathingGrid grid, float left, float bottom, float right, float top)
        {
            if (!(right > left) || !(top > bottom))
            {
                throw new BuildFailure(FormattableString.Invariant($"a Picture of the part from ({left}, {bottom}) to ({right}, {top}): its right is not past its left or its top past its bottom"));
            }

            var first = Math.Clamp((int)MathF.Floor((left - grid.Left) / PathingGrid.CellSize), 0, grid.Columns);
            var last = Math.Clamp((int)MathF.Ceiling((right - grid.Left) / PathingGrid.CellSize), 0, grid.Columns);
            var low = Math.Clamp((int)MathF.Floor((bottom - grid.Bottom) / PathingGrid.CellSize), 0, grid.Rows);
            var high = Math.Clamp((int)MathF.Ceiling((top - grid.Bottom) / PathingGrid.CellSize), 0, grid.Rows);
            if (last <= first || high <= low)
            {
                throw new BuildFailure(FormattableString.Invariant($"a Picture of the part from ({left}, {bottom}) to ({right}, {top}) shows nothing: the part is off the map"));
            }

            return (first, low, last - first, high - low);
        }

        // The map's index of a cell of the picture.
        private int Index(int column, int row) => ((firstRow + row) * grid.Columns) + firstColumn + column;

        // Colours a rectangle of pixels, from the bottom left.
        private void Paint(int x, int y, int width, int height, Color color)
        {
            for (var row = y; row < y + height; row++)
            {
                for (var at = (row * Width) + x; at < (row * Width) + x + width; at++)
                {
                    pixels[at * 3] = color.R;
                    pixels[(at * 3) + 1] = color.G;
                    pixels[(at * 3) + 2] = color.B;
                }
            }
        }

        private static void Chunk(Stream png, string kind, byte[] data)
        {
            var named = new byte[4 + data.Length];
            System.Text.Encoding.ASCII.GetBytes(kind, named);
            data.CopyTo(named, 4);
            var number = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
            png.Write(number);
            png.Write(named);
            BinaryPrimitives.WriteUInt32BigEndian(number, Checksum(named));
            png.Write(number);
        }

        // PNG's CRC-32 of a chunk's kind and data.
        private static uint Checksum(byte[] bytes)
        {
            var sum = 0xFFFFFFFFu;
            foreach (var one in bytes)
            {
                sum ^= one;
                for (var bit = 0; bit < 8; bit++)
                {
                    sum = (sum & 1) != 0 ? 0xEDB88320u ^ (sum >> 1) : sum >> 1;
                }
            }

            return ~sum;
        }
    }
}
