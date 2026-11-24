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
