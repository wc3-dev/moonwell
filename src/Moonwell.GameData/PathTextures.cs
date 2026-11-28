using System.Globalization;
using System.Text;

namespace Moonwell.GameData;

/// <summary>
/// The cells a pathing texture (<c>upat</c>, <c>PathTextures\16x16Simple.tga</c>) gives a building: a cell of 32 a pixel,
/// every pixel that is not black taken (game facts 11-28: a building takes its whole texture, the unbuildable ring of the
/// "Simple" ones too). The game's own textures are embedded (<c>path-textures.txt</c>, written by the generator's
/// <c>footprints</c> command from the install); a map's own one is read from its TGA.
/// </summary>
public static class PathTextures
{
    private static Dictionary<string, Footprint>? standard;

    /// <summary>A texture's cells: its width and height, and which cells it takes, row by row from the lowest (the TGA's own order when it starts at the bottom).</summary>
    public sealed record Footprint(int Width, int Height, bool[] Cells)
    {
        /// <summary>Whether it takes every cell of its rectangle.</summary>
        public bool Full => Cells.All(cell => cell);

        /// <summary>
        /// The cells as text: <c>w x h</c>, then, when it does not take every cell, a colon and its cells as hex digits,
        /// four cells to a digit from the lowest bit, row by row from the lowest.
        /// </summary>
        public string Text()
        {
            var text = new StringBuilder();
            text.Append(Width.ToString(CultureInfo.InvariantCulture)).Append('x').Append(Height.ToString(CultureInfo.InvariantCulture));
            if (Full)
            {
                return text.ToString();
            }

            text.Append(':');
            for (var i = 0; i < Cells.Length; i += 4)
            {
                var digit = 0;
                for (var bit = 0; bit < 4 && i + bit < Cells.Length; bit++)
                {
                    if (Cells[i + bit])
                    {
                        digit |= 1 << bit;
                    }
                }

                text.Append("0123456789abcdef"[digit]);
            }

            return text.ToString();
        }

        /// <summary>Reads what <see cref="Text"/> writes.</summary>
        public static Footprint Parse(string text)
        {
            var colon = text.IndexOf(':');
            var size = colon < 0 ? text : text[..colon];
            var x = size.IndexOf('x');
            var width = int.Parse(size[..x], CultureInfo.InvariantCulture);
            var height = int.Parse(size[(x + 1)..], CultureInfo.InvariantCulture);
            var cells = new bool[width * height];
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = colon < 0 || (Convert.ToInt32(text[colon + 1 + i / 4].ToString(), 16) & (1 << (i % 4))) != 0;
            }

            return new Footprint(width, height, cells);
        }
    }

    /// <summary>The game's own texture at a path as object data writes it (any case, either slash, with or without <c>.tga</c>); null when the game has none.</summary>
    public static Footprint? Standard(string path)
    {
        standard ??= ReadStandard();
        return standard.TryGetValue(Key(path), out var footprint) ? footprint : null;
    }

    /// <summary>The key a path is kept by: lower case, forward slashes, no extension.</summary>
    public static string Key(string path)
    {
        var key = path.Trim().Replace('\\', '/').ToLowerInvariant();
        return key.EndsWith(".tga", StringComparison.Ordinal) ? key[..^4] : key;
    }

    /// <summary>The cells of a TGA: uncompressed or run-length, 24 or 32 bits, rows from the bottom or the top.</summary>
    /// <exception cref="InvalidDataException">The file is not such a TGA.</exception>
    public static Footprint Read(byte[] tga)
    {
        if (tga.Length < 18)
        {
            throw new InvalidDataException("too short for a TGA");
        }

        int idLength = tga[0], type = tga[2];
        var width = tga[12] | (tga[13] << 8);
        var height = tga[14] | (tga[15] << 8);
        var bytes = tga[16] / 8;
        var fromTop = (tga[17] & 0x20) != 0;
        if ((type != 2 && type != 10) || (bytes != 3 && bytes != 4) || width == 0 || height == 0)
        {
            throw new InvalidDataException($"a TGA of type {type} and {bytes * 8} bits; pathing textures are type 2 or 10, of 24 or 32 bits");
        }

        var pixels = new bool[width * height];
        var at = 18 + idLength + (tga[1] != 0 ? (tga[5] | (tga[6] << 8)) * ((tga[7] + 7) / 8) : 0);
        var count = 0;
        bool Pixel(int offset)
        {
            if (offset + 3 > tga.Length)
            {
                throw new InvalidDataException("the TGA ends inside its pixels");
            }

            return tga[offset] != 0 || tga[offset + 1] != 0 || tga[offset + 2] != 0;
        }

        while (count < pixels.Length)
        {
            if (type == 2)
            {
                pixels[count++] = Pixel(at);
                at += bytes;
                continue;
            }

            if (at >= tga.Length)
            {
                throw new InvalidDataException("the TGA ends inside its pixels");
            }

            var header = tga[at++];
            var run = (header & 127) + 1;
            if ((header & 128) != 0)
            {
                var taken = Pixel(at);
                at += bytes;
                for (var k = 0; k < run && count < pixels.Length; k++)
                {
                    pixels[count++] = taken;
                }
            }
            else
            {
                for (var k = 0; k < run && count < pixels.Length; k++)
                {
                    pixels[count++] = Pixel(at);
                    at += bytes;
                }
            }
        }

        if (!fromTop)
        {
            return new Footprint(width, height, pixels);
        }

        var flipped = new bool[pixels.Length];
        for (var row = 0; row < height; row++)
        {
            Array.Copy(pixels, row * width, flipped, (height - 1 - row) * width, width);
        }

        return new Footprint(width, height, flipped);
    }

    // The embedded table: a line for each of the game's textures, its key, a tab and its cells' text.
    private static Dictionary<string, Footprint> ReadStandard()
    {
        var assembly = typeof(PathTextures).Assembly;
        using var stream = assembly.GetManifestResourceStream("path-textures.txt")
            ?? throw new InvalidOperationException("path-textures.txt is not embedded in " + assembly.GetName().Name);
        using var reader = new StreamReader(stream);
        var table = new Dictionary<string, Footprint>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            var tab = line.IndexOf('\t');
            if (line.Length == 0 || line[0] == '#' || tab < 0)
            {
                continue;
            }

            table[line[..tab]] = Footprint.Parse(line[(tab + 1)..]);
        }

        return table;
    }
}
