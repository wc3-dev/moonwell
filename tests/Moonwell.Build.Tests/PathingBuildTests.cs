using System.Text;
using War3Net.Build.Extensions;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A map that uses the pathing package (decision 243) builds and loads, gets the twelve helper unit types, and installs the package after the terrain with their ids; one that does not gets none.</summary>
    [Collection("Sample")]
    public sealed class PathingBuildTests
    {
        private readonly FixtureBuilds fixtures;
        private readonly SampleBuilds builds;

        public PathingBuildTests(FixtureBuilds fixtures, SampleBuilds builds)
        {
            this.fixtures = fixtures;
            this.builds = builds;
        }

        [Fact]
        public void map_with_pathing_builds_and_loads()
        {
            var project = fixtures.Of("Pathing").Folder;
            var script = File.ReadAllText(Path.Combine(project, "build", "Pathing", "war3map.lua"));
            using var lua = new NLua.Lua();
            lua.DoString("print = function() end");
            lua.DoString("setmetatable(_G, { __index = function(_, name) return function() return nil end end })");
            lua.DoString(script);
            lua.DoString("main()");
            Assert.Contains("Moonwell.Pathing", script);
        }

        [Fact]
        public void the_package_is_installed_after_the_terrain_with_the_helper_types()
        {
            var main = File.ReadAllText(Path.Combine(fixtures.Of("Pathing").Folder, "build", "generated", "MapMain.g.cs"));
            var terrain = main.IndexOf("global::Moonwell.Terrain.Install(", StringComparison.Ordinal);
            var pathing = main.IndexOf(PathingTypes.Install, StringComparison.Ordinal);
            Assert.True(terrain >= 0 && pathing > terrain, main);
        }

        [Fact]
        public void the_helper_types_are_written_with_their_squares_and_movements()
        {
            var units = NewUnits(Path.Combine(fixtures.Of("Pathing").Folder, "build", "Pathing"));
            for (var i = 0; i < 12; i++)
            {
                var helper = units.Single(one => one.NewId == IdOf(PathingTypes.Ids[i]));
                Assert.Equal(new[] { 8f, 16f, 32f, 48f }[i / 3], Convert.ToSingle(Value(helper, "ucol")));
                Assert.Equal(new[] { "foot", "float", "amph" }[i % 3], Value(helper, "umvt"));
            }
        }

        [Fact]
        public void a_map_that_asks_by_unit_type_gets_the_unit_types_table_after_the_install()
        {
            var main = File.ReadAllText(Path.Combine(fixtures.Of("Pathing").Folder, "build", "generated", "MapMain.g.cs"));
            var install = main.IndexOf(PathingTypes.Install, StringComparison.Ordinal);
            var table = main.IndexOf("global::Moonwell.Paths.UnitTypes(\"", StringComparison.Ordinal);
            Assert.True(install >= 0 && table > install, main);
            Assert.Contains(",hfoog31,", main);
            Assert.Contains(",hgryy8,", main);
            Assert.Contains(",Hpalg32,", main);
        }

        [Fact]
        public void the_unit_types_table_takes_the_map_file_over_the_standard_and_the_declared_classes_over_both()
        {
            var fixture = fixtures.Of("Pathing").Folder;
            var standard = StandardData.Read(Directory.EnumerateFiles(fixture, "Moonwell.Objects.dll", SearchOption.AllDirectories).First());
            var folder = Directory.CreateTempSubdirectory("moonwell-unit-types-");
            try
            {
                var units = Moonwell.MapFiles.ObjectFile.Empty(Moonwell.GameData.Kind.Unit);
                units.AddOriginal("hfoo").Set("ucol", 0, 0, new Moonwell.MapFiles.FieldValue.Unreal(40f));
                units.AddCustom("hgry", "h000").Set("umvt", 0, 0, new Moonwell.MapFiles.FieldValue.Text("foot"));
                units.AddCustom("hgry", "h002").Set("ucol", 0, 0, new Moonwell.MapFiles.FieldValue.Unreal(99f));
                File.WriteAllBytes(Path.Combine(folder.FullName, "war3map.w3u"), units.ToBytes());
                var declared = new List<DeclaredObject>
                {
                    new DeclaredObject("Objects.Boat", "unit", "hfoo", false, null, null, new Dictionary<string, object?> { ["ucol"] = 20.5f, ["umvt"] = 4 }, Array.Empty<string>()),
                    new DeclaredObject("Objects.Peasant", "unit", "hpea", true, null, null, new Dictionary<string, object?> { ["ucol"] = 12f }, Array.Empty<string>()),
                    new DeclaredObject("Objects.Over", "unit", "hkni", false, "h002", null, new Dictionary<string, object?>(), Array.Empty<string>()),
                    new DeclaredObject("Objects.Spell", "ability", "AHbz", false, null, null, new Dictionary<string, object?>(), Array.Empty<string>()),
                };
                var ids = new Dictionary<string, string> { ["Objects.Boat"] = "h001", ["Objects.Peasant"] = "hpea", ["Objects.Over"] = "h002", ["Objects.Spell"] = "A000" };
                var entries = PathingTypes.Table(standard, GameData.Of(0, 1), folder.FullName, declared, ids).Split(',');
                Assert.Contains("hfoog40", entries);
                Assert.Contains("h000g8", entries);
                Assert.Contains("h001f20.5", entries);
                Assert.Contains("hpeag12", entries);
                Assert.Contains("h002g" + standard.Value("hkni", "ucol", 0, null), entries);
                Assert.DoesNotContain(entries, entry => entry.StartsWith("A000", StringComparison.Ordinal));
                Assert.Equal(entries.OrderBy(entry => entry.Substring(0, 4), StringComparer.Ordinal), entries);
            }
            finally
            {
                folder.Delete(true);
            }
        }

        [Fact]
        public void a_map_that_asks_where_a_building_fits_gets_the_buildings_table_after_the_install()
        {
            var main = File.ReadAllText(Path.Combine(fixtures.Of("Pathing").Folder, "build", "generated", "MapMain.g.cs"));
            var install = main.IndexOf(PathingTypes.Install, StringComparison.Ordinal);
            var table = main.IndexOf("global::Moonwell.Paths.Buildings(\"", StringComparison.Ordinal);
            Assert.True(install >= 0 && table > install, main);
            var entries = main.Substring(table + "global::Moonwell.Paths.Buildings(\"".Length).Split('"')[0].Split(';');
            Assert.Contains("hhou4x4,2,0,-", entries);
            Assert.Contains("htow16x16,2,0,g", entries);
            Assert.Contains("uzig6x6,2,1,-", entries);
            Assert.Contains("ngol16x16,2,0,m", entries);
            Assert.DoesNotContain(entries, entry => entry.StartsWith("hfoo", StringComparison.Ordinal));
        }

        [Fact]
        public void the_buildings_table_reads_the_map_file_the_declared_classes_and_the_maps_own_textures()
        {
            var fixture = fixtures.Of("Pathing").Folder;
            var standard = StandardData.Read(Directory.EnumerateFiles(fixture, "Moonwell.Objects.dll", SearchOption.AllDirectories).First());
            var root = Directory.CreateTempSubdirectory("moonwell-building-types-");
            try
            {
                File.WriteAllText(Path.Combine(root.FullName, "Buildings.csproj"), "<Project />");
                Directory.CreateDirectory(Path.Combine(root.FullName, "map.w3x"));
                File.WriteAllBytes(Path.Combine(root.FullName, "map.w3x", "war3map.w3i"), Array.Empty<byte>());
                var project = MapProject.Find(root.FullName);
                Directory.CreateDirectory(Path.Combine(project.AssetsFolder, "Textures"));

                // A TGA of 2 by 2 whose lower left and upper right pixels are white: cells 0 and 3, hex 9.
                var tga = new byte[18 + 4 * 3];
                tga[2] = 2;
                tga[12] = 2;
                tga[14] = 2;
                tga[16] = 24;
                for (var k = 0; k < 3; k++)
                {
                    tga[18 + k] = 255;
                    tga[18 + 9 + k] = 255;
                }

                File.WriteAllBytes(Path.Combine(project.AssetsFolder, "Textures", "Corners.tga"), tga);
                var units = Moonwell.MapFiles.ObjectFile.Empty(Moonwell.GameData.Kind.Unit);
                units.AddOriginal("hhou").Set("upat", 0, 0, new Moonwell.MapFiles.FieldValue.Text("PathTextures/8x8SimpleSolid.tga"));
                units.AddCustom("hbar", "h000").Set("upat", 0, 0, new Moonwell.MapFiles.FieldValue.Text("Textures/Corners.tga"));
                units.AddCustom("hfoo", "h003").Set("ubdg", 0, 0, new Moonwell.MapFiles.FieldValue.Int(1));
                File.WriteAllBytes(Path.Combine(project.MapFolder, "war3map.w3u"), units.ToBytes());
                var abilities = Moonwell.MapFiles.ObjectFile.Empty(Moonwell.GameData.Kind.Ability);
                abilities.AddCustom("Argl", "A001");
                File.WriteAllBytes(Path.Combine(project.MapFolder, "war3map.w3a"), abilities.ToBytes());
                var declared = new List<DeclaredObject>
                {
                    new DeclaredObject("Objects.Keep", "unit", "hbar", false, null, null, new Dictionary<string, object?> { ["upar"] = 1, ["uabi"] = new List<string> { "Objects.Returns" } }, Array.Empty<string>()),
                    new DeclaredObject("Objects.Returns", "ability", "Argl", false, null, null, new Dictionary<string, object?>(), Array.Empty<string>()),
                    new DeclaredObject("Objects.Mill", "unit", "hlum", false, null, null, new Dictionary<string, object?> { ["uabi"] = new List<string> { "A001" } }, Array.Empty<string>()),
                };
                var ids = new Dictionary<string, string> { ["Objects.Keep"] = "h001", ["Objects.Returns"] = "A000", ["Objects.Mill"] = "h002" };
                var entries = BuildingTypes.Table(standard, GameData.Of(0, 1), project, declared, ids, new Dictionary<string, string>()).Split(';');
                Assert.Contains("hhou8x8,2,0,-", entries);
                Assert.Contains("h0002x2:9,2,0,-", entries);
                Assert.Contains("h00112x12,2,1,g", entries);
                Assert.Contains("h00210x10,2,0,g", entries);
                Assert.Contains("hbar12x12,2,0,-", entries);
                Assert.Contains(entries, entry => entry.StartsWith("h003", StringComparison.Ordinal));
                Assert.DoesNotContain(entries, entry => entry.StartsWith("hfoo", StringComparison.Ordinal));
                Assert.Equal(entries.OrderBy(entry => entry.Substring(0, 4), StringComparer.Ordinal), entries);

                units.AddCustom("hbar", "h004").Set("upat", 0, 0, new Moonwell.MapFiles.FieldValue.Text("Textures/Missing.tga"));
                File.WriteAllBytes(Path.Combine(project.MapFolder, "war3map.w3u"), units.ToBytes());
                var missing = Assert.Throws<BuildException>(() => BuildingTypes.Table(standard, GameData.Of(0, 1), project, declared, ids, new Dictionary<string, string>()));
                Assert.Contains("h004", missing.Message);
            }
            finally
            {
                root.Delete(true);
            }
        }

        [Fact]
        public void map_without_pathing_gets_no_helpers()
        {
            Assert.Equal(0, builds.Development.ExitCode);
            var ids = NewUnits(Path.Combine(builds.Development.BuildFolder, "Sample")).Select(one => one.NewId).ToList();
            Assert.DoesNotContain(IdOf(PathingTypes.Ids[0]), ids);
        }

        private static List<War3Net.Build.Object.SimpleObjectModification> NewUnits(string mapFolder)
        {
            var path = Path.Combine(mapFolder, "war3map.w3u");
            if (!File.Exists(path))
            {
                return new List<War3Net.Build.Object.SimpleObjectModification>();
            }

            using var reader = new BinaryReader(File.OpenRead(path), Encoding.UTF8);
            return reader.ReadUnitObjectData().NewUnits.ToList();
        }

        private static object? Value(War3Net.Build.Object.SimpleObjectModification unit, string field)
        {
            return unit.Modifications.LastOrDefault(one => one.Id == IdOf(field))?.Value;
        }

        private static int IdOf(string text)
        {
            return BitConverter.ToInt32(Encoding.ASCII.GetBytes(text));
        }
    }
}
