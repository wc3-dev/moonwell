using Moonwell.BuildTime;
using War3Net.Build.Environment;
using War3Net.Build.Extensions;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>
    /// <c>MapReader.HeightAt</c> (J127): the ground's height in build code, by the rule the build writes the game's terrain grid
    /// with, so it gives what <c>Terrain.Height</c> gives in the game.
    /// </summary>
    public sealed class MapHeightTests : IDisposable
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

        private readonly TestFolders folders;

        public MapHeightTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void the_height_is_the_corners_blended_with_cliffs_ramps_and_water()
        {
            // Two tiles across, one up, from (-128, 0): a plain corner, a cliff level up, a ramp under a higher ramp, and water.
            var environment = new MapEnvironment(MapEnvironmentFormatVersion.v11) { Width = 2, Height = 1, Left = -128f, Bottom = 0f };
            environment.TerrainTiles.Add(new TerrainTile { Height = 0.5f, CliffLevel = 2 });
            environment.TerrainTiles.Add(new TerrainTile { Height = 0f, CliffLevel = 3 });
            environment.TerrainTiles.Add(new TerrainTile { Height = 0f, CliffLevel = 2, Flags = TileFlags.Ramp });
            environment.TerrainTiles.Add(new TerrainTile { Height = 0f, CliffLevel = 2, Flags = TileFlags.Water, WaterHeight = 1f });
            environment.TerrainTiles.Add(new TerrainTile { Height = 0f, CliffLevel = 2 });
            environment.TerrainTiles.Add(new TerrainTile { Height = 0f, CliffLevel = 3, Flags = TileFlags.Ramp });
            var map = Reader(environment);

            Assert.Equal(64f, map.HeightAt(-128f, 0f), 3);
            Assert.Equal(128f, map.HeightAt(0f, 0f), 3);

            // The ramp corner has a ramp one level up beside it along y, a corner of two of its tiles: 64 up.
            Assert.Equal(64f, map.HeightAt(128f, 0f), 3);

            // The water's surface is above the ground: 128 - 89.6.
            Assert.Equal(38.4f, map.HeightAt(-128f, 128f), 3);

            // Between the corners, and outside the map at its edge.
            Assert.Equal((64f + 128f + 38.4f + 0f) / 4f, map.HeightAt(-64f, 64f), 3);
            Assert.Equal(map.HeightAt(128f, 128f), map.HeightAt(5000f, 5000f), 3);
        }

        [Fact]
        public void the_height_matches_the_terrain_grid_the_build_writes_for_the_game()
        {
            var folder = folders.TemporaryCopyOf("BuildTime");
            var map = new MapReader(Path.Combine(folder, "map"));
            var environment = map.Environment;
            var text = TerrainWriter.Encode(environment);
            var columns = (int)environment.Width + 1;
            var rows = (int)environment.Height + 1;

            // What the game's grid holds at each corner, read back from the text as Moonwell.Terrain's HeightGrid reads it.
            var corners = new float[columns * rows];
            for (var index = 0; index < corners.Length; index++)
            {
                var value = 0;
                for (var at = 0; at < 4; at++)
                {
                    value = (value << 6) | Alphabet.IndexOf(text[(index * 4) + at]);
                }

                corners[index] = ((value >> 7) - 49152) / 20f;
            }

            var random = new Random(3);
            for (var one = 0; one < 2000; one++)
            {
                var x = environment.Left + ((float)random.NextDouble() * (columns - 1) * 128f);
                var y = environment.Bottom + ((float)random.NextDouble() * (rows - 1) * 128f);
                Assert.Equal(Blend(corners, columns, rows, environment.Left, environment.Bottom, x, y), map.HeightAt(x, y), 3);
            }
        }

        [Fact]
        public void a_map_with_no_ground_file_stops_the_build()
        {
            var folder = folders.TemporaryCopyOf("BuildTime");
            File.Delete(Path.Combine(folder, "map", "war3map.w3e"));
            var map = new MapReader(Path.Combine(folder, "map"));

            Assert.Equal("the map has no war3map.w3e: save the map in World Editor", Assert.Throws<BuildFailure>(() => map.HeightAt(0f, 0f)).Message);
        }

        // HeightGrid.Height's blend: the four corners of the point's tile, bilinearly.
        private static float Blend(float[] corners, int columns, int rows, float left, float bottom, float x, float y)
        {
            var u = Math.Clamp((x - left) / 128f, 0f, columns - 1);
            var v = Math.Clamp((y - bottom) / 128f, 0f, rows - 1);
            var i = Math.Min((int)MathF.Floor(u), columns - 2);
            var j = Math.Min((int)MathF.Floor(v), rows - 2);
            var fx = u - i;
            var fy = v - j;
            float At(int column, int row) => corners[(row * columns) + column];
            return ((1f - fy) * (((1f - fx) * At(i, j)) + (fx * At(i + 1, j)))) + (fy * (((1f - fx) * At(i, j + 1)) + (fx * At(i + 1, j + 1))));
        }

        private MapReader Reader(MapEnvironment environment)
        {
            var folder = Path.Combine(folders.TemporaryCopyOf("BuildTime"), "map");
            using (var writer = new BinaryWriter(File.Create(Path.Combine(folder, "war3map.w3e"))))
            {
                writer.Write(environment);
            }

            return new MapReader(folder);
        }
    }
}
