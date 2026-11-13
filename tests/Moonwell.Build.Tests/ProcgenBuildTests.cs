using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A map whose project references only the procgen package builds (decision 269): its script loads with the package's code, and the terrain package that comes with it is installed.</summary>
    [Collection("Sample")]
    public sealed class ProcgenBuildTests
    {
        private readonly FixtureBuilds fixtures;

        public ProcgenBuildTests(FixtureBuilds fixtures)
        {
            this.fixtures = fixtures;
        }

        [Fact]
        public void map_with_the_generator_builds_and_loads()
        {
            var project = fixtures.Of("Procgen").Folder;
            var script = File.ReadAllText(Path.Combine(project, "build", "Procgen", "war3map.lua"));
            using var lua = new NLua.Lua();
            lua.DoString("print = function() end");
            lua.DoString("called = {}");
            lua.DoString("setmetatable(_G, { __index = function(_, name) return function() called[#called + 1] = name; return nil end end })");
            lua.DoString(script);
            lua.DoString("main()");
            Assert.Contains("TerrainGenerator", script);
            Assert.Contains("SnowyForest", script);
            Assert.Contains("Moonwell.Procgen", script);
        }

        [Fact]
        public void the_terrain_under_the_generator_is_installed()
        {
            var main = File.ReadAllText(Path.Combine(fixtures.Of("Procgen").Folder, "build", "generated", "MapMain.g.cs"));
            Assert.Contains("global::Moonwell.Terrain.Install(", main);
        }
    }
}
