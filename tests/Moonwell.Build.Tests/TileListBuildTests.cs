using War3Net.Build.Environment;
using War3Net.Build.Extensions;
using Moonwell.Tool;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>The tiles a map class declares join the end of the built map's tile list, past the game's limit an error (decision 282).</summary>
    [Collection("Sample")]
    public sealed class TileListBuildTests : IDisposable
    {
        // The Terrain fixture's w3e lists Ldrt, Ldro, Ldrg, Lrok, Lgrs and Lgrd.
        private static readonly string[] FixtureTiles = { "Ldrt", "Ldro", "Ldrg", "Lrok", "Lgrs", "Lgrd" };

        private readonly TestFolders folders;

        public TileListBuildTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void declared_tiles_join_the_end_of_the_list()
        {
            var project = WithTiles("Tiles.Add(Tile.LordaeronWinterSnow, Tile.LordaeronSummerDirt, Tile.BarrensDesert, Tile.LordaeronWinterSnow);");

            var (source, built) = Build(project);

            Assert.Equal(FixtureTiles.Concat(new[] { "Wsnw", "Bdsr" }), Ids(built));
            // Two ids more in the list, and every byte after it, the corners, as World Editor wrote them.
            var listEnd = 17 + (4 * FixtureTiles.Length);
            Assert.Equal(source.Length + 8, built.Length);
            Assert.Equal(source.AsSpan(listEnd).ToArray(), built.AsSpan(listEnd + 8).ToArray());

            var data = File.ReadAllText(Path.Combine(project, "build", "generated", "TerrainData.g.cs"));
            Assert.Contains("Tiles() => \"LdrtLdroLdrgLrokLgrsLgrdWsnwBdsr\"", data);
        }

        [Fact]
        public void no_declared_tiles_leave_the_w3e_as_it_is()
        {
            var project = WithTiles("Tiles.Add(Tile.LordaeronSummerDirt, Tile.LordaeronSummerGrass);");

            var (source, built) = Build(project);

            Assert.Equal(source, built);
        }

        [Theory]
        [InlineData(
            "Tiles.Add(Tile.LordaeronFallDirt, Tile.LordaeronFallRoughDirt, Tile.LordaeronFallGrassyDirt, Tile.LordaeronFallRock, Tile.LordaeronFallGrass, Tile.LordaeronFallDarkGrass, Tile.LordaeronWinterDirt, Tile.LordaeronWinterRoughDirt, Tile.LordaeronWinterGrassySnow, Tile.LordaeronWinterRock, Tile.LordaeronWinterGrass);",
            "TerrainFixture.TerrainMap.Tiles: the map's tile list would hold 17 tiles, past the game's 16: 6 from map\\war3map.w3e and 11 declared (Fdrt, Fdro, Fdrg, Frok, Fgrs, Fgrd, Wdrt, Wdro, Wsng, Wrok, Wgrs); declare fewer, or remove tiles the map does not use in World Editor")]
        [InlineData("Tiles.Add(1234);", "TerrainFixture.TerrainMap.Tiles: 1234 is no ground tile; use the Tile constants")]
        public void a_list_the_game_cannot_take_is_an_error(string tiles, string message)
        {
            var project = WithTiles(tiles);
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));

            Assert.NotEqual(0, exitCode);
            Assert.Contains(message, error.ToString() + output);
        }

        private static (byte[] Source, byte[] Built) Build(string project)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));
            Assert.True(exitCode == 0, error.ToString() + output);
            return (File.ReadAllBytes(Path.Combine(project, "map", "war3map.w3e")), File.ReadAllBytes(Path.Combine(project, "build", "Terrain", "war3map.w3e")));
        }

        private static IEnumerable<string> Ids(byte[] w3e)
        {
            using var reader = new BinaryReader(new MemoryStream(w3e));
            return reader.ReadMapEnvironment().TerrainTypes.Select(type => Moonwell.MapFiles.Rawcodes.TextOf((int)type)).ToList();
        }

        // A copy of the Terrain fixture whose map class declares the tiles.
        private string WithTiles(string tiles)
        {
            var project = folders.TemporaryCopyOf("Terrain");
            var map = Path.Combine(project, "Map.cs");
            File.WriteAllText(map, File.ReadAllText(map).Replace("Components.Add(new Probe());", "Components.Add(new Probe());\n            " + tiles));
            return project;
        }
    }
}
