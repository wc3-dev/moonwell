using System.Globalization;
using System.Text;
using Moonwell.BuildTime;
using War3Net.Build.Environment;

namespace Moonwell.Build
{
    /// <summary>
    /// Writes the map's ground, read from <c>war3map.w3e</c>, as the C# file <c>Moonwell.Terrain</c> reads its grid from. Each corner
    /// is a 24-bit number written as four characters (see <c>HeightGrid</c> in the package, which reads them). A
    /// water-flagged corner takes the water's surface only where that is above its ground; elsewhere it takes the ground
    /// and its water bit is clear. (A water-flagged corner in the map's outer rings, which the game reads as ground,
    /// is still given by this rule: those rings are outside the playable area.) A ramp-flagged corner is raised 32 for each
    /// of the (up to four) tiles it is a corner of that has, among its other three corners, a ramp-flagged corner whose
    /// cliff level is exactly one higher: a higher ramp corner along x or y (in two of the tiles) gives 64, one only
    /// diagonal (in one tile) gives 32, an L of them (three tiles) gives 96. Corners outside the grid are not counted.
    /// The raised ground is what the water rule compares the surface with and what is written, while the corner's cliff
    /// level and flags are unchanged. The rule fits all 15,625 corners of Lost Temple's playable area (10-25).
    /// </summary>
    public static class TerrainWriter
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

        // The height times 20 is stored with this added, in 17 bits.
        private const int ZeroHeight = 49152;

        private const int HeightLimit = 131072;

        private const string FileName = "TerrainData.g.cs";

        /// <summary>The grid's text: four characters a corner, row by row from the bottom left.</summary>
        /// <param name="environment">The map's ground.</param>
        /// <returns>The text.</returns>
        /// <exception cref="BuildException">A corner's height is outside what the grid can hold, or the map lists the wrong number of corners.</exception>
        public static string Encode(MapEnvironment environment)
        {
            var columns = Columns(environment);
            var rows = Rows(environment);
            if (environment.TerrainTiles.Count != columns * rows)
            {
                throw new BuildException($"war3map.w3e lists {environment.TerrainTiles.Count} corners, but a map of {environment.Width} by {environment.Height} tiles has {columns} by {rows}");
            }

            var text = new StringBuilder(columns * rows * 4);
            for (var index = 0; index < environment.TerrainTiles.Count; index++)
            {
                var tile = environment.TerrainTiles[index];

                // Water counts only where its surface is above the ground; below it, the game reads the ground.
                var z = GroundHeights.Corner(environment, index, out var water);
                var h = (int)MathF.Round(z * 20f) + ZeroHeight;
                if (h < 0 || h >= HeightLimit)
                {
                    throw new BuildException(
                        $"war3map.w3e: the ground at corner ({index % columns}, {index / columns}) is {z.ToString("0.##", CultureInfo.InvariantCulture)} high, outside the -2457.6 to 4096 the terrain grid holds");
                }

                var value = (h << 7) | (tile.CliffLevel << 3) | (tile.IsRamp ? 2 : 0) | (water ? 1 : 0);
                text.Append(Alphabet[(value >> 18) & 63]);
                text.Append(Alphabet[(value >> 12) & 63]);
                text.Append(Alphabet[(value >> 6) & 63]);
                text.Append(Alphabet[value & 63]);
            }

            return text.ToString();
        }

        /// <summary>
        /// The ground's height at a point beneath any water, which the grid does not keep (it keeps the water's surface where
        /// that is above the ground): the four corners of the point's tile, each its ground raised on a ramp as
        /// <see cref="Encode"/> raises it and kept to a twentieth as the grid keeps it, blended as the grid blends them. A
        /// point outside the grid reads its edge. A placed bridge's deck stands on it.
        /// </summary>
        /// <param name="environment">The map's ground.</param>
        /// <param name="x">The point's x.</param>
        /// <param name="y">The point's y.</param>
        /// <returns>The height.</returns>
        public static float GroundBeneath(MapEnvironment environment, float x, float y)
        {
            return GroundHeights.Blend(
                Columns(environment),
                Rows(environment),
                environment.Left,
                environment.Bottom,
                x,
                y,
                index => GroundHeights.Kept(GroundHeights.Ground(environment, index)));
        }

        /// <summary>
        /// Writes <c>TerrainData.g.cs</c> into the project's generated folder: <c>Moonwell.Generated.TerrainData</c> with the grid's
        /// size and origin as constants and its text from <c>Corners()</c>, a method so that CSharp.lua writes the text once.
        /// </summary>
        /// <param name="project">The map project.</param>
        /// <param name="environment">The map's ground.</param>
        /// <returns>The path of the written file.</returns>
        /// <exception cref="BuildException">See <see cref="Encode"/>.</exception>
        public static string Write(MapProject project, MapEnvironment environment)
        {
            var corners = Encode(environment);
            Directory.CreateDirectory(project.GeneratedFolder);
            var path = Path.Combine(project.GeneratedFolder, FileName);
            var text = string.Join(
                "\n",
                "// Generated by Moonwell's build from war3map.w3e. Do not edit; every build writes it anew.",
                "namespace Moonwell.Generated",
                "{",
                "    public static class TerrainData",
                "    {",
                "        public const int Columns = " + Columns(environment).ToString(CultureInfo.InvariantCulture) + ";",
                "        public const int Rows = " + Rows(environment).ToString(CultureInfo.InvariantCulture) + ";",
                "        public const float Left = " + Literal(environment.Left) + ";",
                "        public const float Bottom = " + Literal(environment.Bottom) + ";",
                "        public static string Corners() => \"" + corners + "\";",
                "        public static string Tiles() => \"" + Tiles(environment) + "\";",
                "    }",
                "}",
                string.Empty);
            File.WriteAllText(path, text);
            return path;
        }

        /// <summary>The map's list of ground tiles, their four-letter ids one after another, as the w3e lists them.</summary>
        /// <param name="environment">The map's ground.</param>
        /// <returns>The ids, four characters each.</returns>
        public static string Tiles(MapEnvironment environment)
        {
            var text = new System.Text.StringBuilder();
            foreach (var type in environment.TerrainTypes)
            {
                // The w3e keeps an id as its four bytes in order, read as a little-endian number.
                text.Append(System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes((int)type)));
            }

            return text.ToString();
        }

        /// <summary>Removes the file an earlier build wrote, which a map that no longer references the package would not compile.</summary>
        /// <param name="project">The map project.</param>
        public static void Delete(MapProject project)
        {
            var path = Path.Combine(project.GeneratedFolder, FileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        // War3Net's Width and Height count tiles; the grid counts the corners between and around them, one more each way.
        private static int Columns(MapEnvironment environment) => GroundHeights.Columns(environment);

        private static int Rows(MapEnvironment environment) => GroundHeights.Rows(environment);

        private static string Literal(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture) + "f";
        }
    }
}
