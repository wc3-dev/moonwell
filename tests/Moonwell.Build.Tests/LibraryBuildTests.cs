using System.Text;
using Moonwell.Tool;
using War3Net.Build.Extensions;
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

        [Fact]
        public void a_standard_class_a_library_uses_is_stubbed_and_the_script_runs()
        {
            var project = Path.Combine(folders.TemporaryCopyOf("ObjectsLibraryMap"), "Map");

            Build(project);

            var stubs = File.ReadAllText(Path.Combine(project, "build", "generated", "ObjectStubs.g.cs"));
            Assert.Contains("class Footman", stubs);
            var script = File.ReadAllText(Path.Combine(project, "build", "Map", "war3map.lua"));
            using var lua = new NLua.Lua();
            lua.DoString("print = function() end");
            lua.DoString("setmetatable(_G, { __index = function() return function() return nil end end })");
            lua.DoString(script);
            lua.DoString("main()");
        }

        [Fact]
        public void a_library_that_uses_the_objects_makes_the_map_use_them()
        {
            var project = Path.Combine(folders.TemporaryCopyOf("ObjectsLibraryMap"), "Map");

            var assembly = DotnetBuild.Run(MapProject.Find(project), false, TextWriter.Null);

            Assert.True(Declarations.UsesObjects(assembly));
        }

        [Fact]
        public void an_abstract_base_in_a_library_can_be_declared_in_the_map()
        {
            var copy = folders.TemporaryCopyOf("ObjectsLibraryMap");
            File.AppendAllText(Path.Combine(copy, "Code", "Spawner.cs"), """

                namespace ObjectsLibraryCode
                {
                    public abstract class Guard : Moonwell.Objects.Human.Units.Footman
                    {
                        protected Guard()
                        {
                            MaxLife = 900;
                        }
                    }
                }
                """);
            File.AppendAllText(Path.Combine(copy, "Map", "Map.cs"), """

                namespace ObjectsLibraryMapFixture
                {
                    [Moonwell.Objects.ObjectId("h001")]
                    public sealed class Captain : ObjectsLibraryCode.Guard
                    {
                        public Captain()
                        {
                            Name = "Captain";
                        }
                    }
                }
                """);
            var project = Path.Combine(copy, "Map");

            Build(project);

            using var reader = new BinaryReader(File.OpenRead(Path.Combine(project, "build", "Map", "war3map.w3u")), Encoding.UTF8);
            var captain = Assert.Single(reader.ReadUnitObjectData().NewUnits);
            Assert.Equal(900, Assert.Single(captain.Modifications, one => one.Id == BitConverter.ToInt32(Encoding.ASCII.GetBytes("uhpm"), 0)).Value);
        }

        [Fact]
        public void an_object_class_a_library_declares_is_an_error()
        {
            var copy = folders.TemporaryCopyOf("ObjectsLibraryMap");
            File.AppendAllText(Path.Combine(copy, "Code", "Spawner.cs"), """

                namespace ObjectsLibraryCode
                {
                    [Moonwell.Objects.ObjectId("h002")]
                    public sealed class Shared : Moonwell.Objects.Human.Units.Footman
                    {
                    }
                }
                """);

            var error = Assert.Throws<BuildException>(() => MapBuild.Run(Path.Combine(copy, "Map"), new BuildOptions(), new StringWriter()));

            Assert.Contains("ObjectsLibraryCode.Shared in the library Code is an object class", error.Message);
            Assert.Contains("declare it in the map's own project", error.Message);
        }

        [Fact]
        public void a_name_a_library_uses_only_in_nameof_is_an_error_that_says_why()
        {
            // The build reads the map's source for nameof, not a library's.
            var copy = folders.TemporaryCopyOf("ObjectsLibraryMap");
            File.AppendAllText(Path.Combine(copy, "Code", "Spawner.cs"), """

                namespace ObjectsLibraryCode
                {
                    public static class Names
                    {
                        public static readonly string Life = nameof(Moonwell.Objects.Human.Units.Paladin.GoldCost);
                    }
                }
                """);

            var error = Assert.Throws<BuildException>(() => MapBuild.Run(Path.Combine(copy, "Map"), new BuildOptions(), new StringWriter()));

            // Paladin is named nowhere else, so CSharp.lua stops at it before GoldCost.
            Assert.Contains("'Human.Units' does not contain a definition for 'Paladin'", error.Message);
            Assert.Contains("a name a library uses only in nameof is not one", error.Message);
        }

        [Fact]
        public void a_save_class_a_library_declares_is_saved()
        {
            var copy = folders.TemporaryCopyOf("ObjectsLibraryMap");
            File.WriteAllText(Path.Combine(copy, "Code", "Saves.cs"), """
                using Moonwell;

                namespace ObjectsLibraryCode
                {
                    public sealed class Gear
                    {
                        [Saved(1)] public int Id;
                    }

                    public sealed class LibrarySave : SaveGame
                    {
                        [Saved(1)] public int Level;
                        [Saved(2)] public Gear? Weapon;
                    }

                    public static class SaveRoundTrip
                    {
                        public static string Run()
                        {
                            var save = new LibrarySave { Level = 7, Weapon = new Gear { Id = 3 } };
                            SaveGame.Save(Player.One, save);
                            var back = SaveGame.FromCode<LibrarySave>(Player.One, save.Code);
                            return back == null ? "null" : back.Level + " " + back.Weapon!.Id;
                        }
                    }
                }
                """);
            var project = Path.Combine(copy, "Map");
            var map = Path.Combine(project, "Map.cs");
            File.WriteAllText(map, File.ReadAllText(map).Replace("Name = \"ObjectsLibraryMap\";", "Name = \"ObjectsLibraryMap\";\n            SaveKey = \"the tests' key\";", StringComparison.Ordinal));

            Build(project);

            Assert.Equal("7 3", SaveBuildTests.RoundTrip(Path.Combine(project, "build", "Map", "war3map.lua"), "ObjectsLibraryCode"));
        }

        [Fact]
        public void a_default_object_a_library_reads_gets_the_types_values()
        {
            var copy = folders.TemporaryCopyOf("ObjectsLibraryMap");
            File.AppendAllText(Path.Combine(copy, "Code", "Spawner.cs"), """

                namespace ObjectsLibraryCode
                {
                    public static class Reader
                    {
                        public static string Life(Moonwell.Unit unit)
                        {
                            return unit.Default.MaxLife + " " + unit.Default.Name;
                        }
                    }
                }
                """);
            var project = Path.Combine(copy, "Map");

            Build(project);

            Assert.Contains("MoonwellTypeValues = {", File.ReadAllText(Path.Combine(project, "build", "Map", "war3map.lua")));
        }

        private static void Build(string project)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));
            Assert.True(exitCode == 0, error.ToString() + output);
        }
    }
}
