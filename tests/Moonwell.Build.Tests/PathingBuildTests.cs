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
