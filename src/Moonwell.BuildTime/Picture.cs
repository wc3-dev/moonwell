using System.Buffers.Binary;
using System.Drawing;
using System.IO.Compression;

namespace Moonwell.BuildTime
{
    /// <summary>
    /// A picture of a map for the map maker to look at, a pixel for each pathing cell, north at the top. It starts black;
    /// <see cref="Fill"/> colours a set of cells, later calls over earlier ones, and <see cref="BuildStep"/>'s <c>Report</c> writes it.
    /// </summary>
    public sealed class Picture
    {
        private readonly PathingGrid grid;

        private readonly byte[] pixels;

        /// <summary>Makes a black picture the size of a map's pathing.</summary>
        /// <param name="map">The map.</param>
        public Picture(MapReader map)
            : this(map.Cells.Grid)
        {
        }

        internal Picture(PathingGrid grid)
        {
            this.grid = grid;
            pixels = new byte[grid.Columns * grid.Rows * 3];
        }

        /// <summary>Colours the pixels of a set of cells.</summary>
        /// <param name="cells">The cells.</param>
        /// <param name="color">Their colour, such as <c>Color.Blue</c>.</param>
        /// <returns>The picture, to chain another fill.</returns>
        public Picture Fill(Cells cells, Color color)
        {
            for (var index = 0; index < grid.Flags.Length; index++)
            {
                if (cells.Holds(index))
                {
                    pixels[index * 3] = color.R;
                    pixels[(index * 3) + 1] = color.G;
                    pixels[(index * 3) + 2] = color.B;
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
                for (var row = grid.Rows - 1; row >= 0; row--)
                {
                    packed.WriteByte(0);
                    packed.Write(pixels, row * grid.Columns * 3, grid.Columns * 3);
                }
            }

            var header = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header, grid.Columns);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), grid.Rows);
            header[8] = 8;
            header[9] = 2;

            var png = new MemoryStream();
            png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            Chunk(png, "IHDR", header);
            Chunk(png, "IDAT", lines.ToArray());
            Chunk(png, "IEND", Array.Empty<byte>());
            return png.ToArray();
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
