using Moonwell.Tool;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A map whose code is in a library project of its own: what the library uses is installed as if the map used it.</summary>
    public sealed class LibraryBuildTests : IDisposable
    {
        private readonly TestFolders folders;

        public LibraryBuildTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void packages_a_library_uses_are_installed()
        {
            var project = Path.Combine(folders.TemporaryCopyOf("LibraryMap"), "Map");
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));
            Assert.True(exitCode == 0, error.ToString() + output);

            var main = File.ReadAllText(Path.Combine(project, "build", "generated", "MapMain.g.cs"));
            Assert.Contains("global::Moonwell.DamageSystem.Install();", main);
            Assert.Contains("global::Moonwell.Terrain.Install(", main);
            Assert.Contains("global::Moonwell.AreaSystem.Install(", main);
            Assert.Contains("global::Moonwell.InputSystem.Install(global::Moonwell.Generated.InputData.Mouse, true,", main);
            Assert.Contains("global::Moonwell.Physics.Install(", main);

            // The Lua compiles with those calls, and the script loads and runs its main.
            var script = File.ReadAllText(Path.Combine(project, "build", "Map", "war3map.lua"));
            using var lua = new NLua.Lua();
            lua.DoString("print = function() end");
            lua.DoString("called = {}");
            lua.DoString("setmetatable(_G, { __index = function(_, name) return function() called[#called + 1] = name; return nil end end })");
            lua.DoString(script);
            lua.DoString("main()");
            var called = (string)lua.DoString("return ',' .. table.concat(called, ',') .. ','")[0];
            Assert.Contains(",GetWorldBounds,", called);
            Assert.Contains("Moonwell.DamageSystem.Install()", script);
        }

        [Fact]
        public void a_package_a_package_references_is_not_the_maps_use_of_it()
        {
            var project = Path.Combine(folders.TemporaryCopyOf("LibraryMap"), "Map");
            var assembly = DotnetBuild.Run(MapProject.Find(project), false, TextWriter.Null);

            // Moonwell.Physics references Moonwell.Interactions, and the library names neither it nor the objects.
            Assert.True(Declarations.References(assembly, "Moonwell.Physics"));
            Assert.False(Declarations.References(assembly, "Moonwell.Interactions"));
            Assert.False(Declarations.UsesObjects(assembly));
        }
    }
}
