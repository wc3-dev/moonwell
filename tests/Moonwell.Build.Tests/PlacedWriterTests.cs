using Microsoft.CodeAnalysis.CSharp;
using War3Net.Build.Environment;
using War3Net.Build.Widget;
using War3Net.Common.Extensions;
using WorldEditorCamera = War3Net.Build.Environment.Camera;

namespace Moonwell.Build.Tests
{
    /// <summary>The Placed class the build writes from the map's placed things before the compile (decision 254).</summary>
    public sealed class PlacedWriterTests : IDisposable
    {
        private readonly TestFolders folders;

        public PlacedWriterTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void writes_a_member_for_each_placed_thing_named_as_world_editor_names_its_variable()
        {
            var project = Project();
            var map = Map();

            var text = File.ReadAllText(PlacedWriter.Write(project, map, false));

            Assert.Contains("public static global::Moonwell.Unit? hfoo_0001 => global::Moonwell.Unit.Named(\"hfoo_0001\");", text);
            Assert.Contains("public static global::Moonwell.Item? ratc_0005 => global::Moonwell.Item.Named(\"ratc_0005\");", text);
            Assert.Contains("public static global::Moonwell.Destructable? LTg1_0002 => global::Moonwell.Destructable.Named(\"LTg1_0002\");", text);
            Assert.Contains("public static global::Moonwell.PlacedRegion Spawn_Left => new global::Moonwell.PlacedRegion(\"Spawn Left\", -512f, -256f, 0f, 256.5f);", text);
            Assert.Contains("public static global::Moonwell.PlacedCamera Intro => new global::Moonwell.PlacedCamera(\"Intro\");", text);
            Assert.Contains("public static global::Moonwell.PlacedSound? Rain => global::Moonwell.PlacedSound.Named(\"Rain\");", text);
            Assert.Empty(CSharpSyntaxTree.ParseText(text).GetDiagnostics());
        }

        [Fact]
        public void leaves_out_trees_doodads_and_start_locations()
        {
            var project = Project();
            var map = Map();

            var text = File.ReadAllText(PlacedWriter.Write(project, map, false));

            Assert.DoesNotContain("LTlt_0012", text);
            Assert.DoesNotContain("ZPsh_0013", text);
            Assert.DoesNotContain("sloc", text);
            Assert.Contains("LTlt_0012", PlacedWriter.DestructableNames(project, map));
            Assert.DoesNotContain("ZPsh_0013", PlacedWriter.DestructableNames(project, map));
        }

        [Fact]
        public void types_a_member_as_the_class_whose_object_id_is_its_type()
        {
            var project = Project();
            File.WriteAllText(Path.Combine(project.Folder, "Captain.cs"), "namespace Game.Heroes { [ObjectId(\"h000\")] public sealed class Captain : Footman { } }");
            var map = Map();

            var text = File.ReadAllText(PlacedWriter.Write(project, map, false));

            Assert.Contains("public static global::Game.Heroes.Captain? h000_0004 => (object?)global::Moonwell.Unit.Named(\"h000_0004\") as global::Game.Heroes.Captain;", text);
        }

        [Fact]
        public void a_name_that_is_no_identifier_or_taken_is_made_one()
        {
            var project = Project();
            var map = new War3Net.Build.Map { Regions = new MapRegions(MapRegionsFormatVersion.v5) };
            foreach (var name in new[] { "2 Gate", "class", "Regions", "Shop-A", "Shop A" })
            {
                map.Regions.Regions.Add(new Region { Name = name, Left = 0, Bottom = 0, Right = 32, Top = 32 });
            }

            var text = File.ReadAllText(PlacedWriter.Write(project, map, false));

            Assert.Contains(" _2_Gate => ", text);
            Assert.Contains(" _class => ", text);
            Assert.Contains(" Regions_2 => ", text);
            Assert.Contains(" Shop_A => ", text);
            Assert.Contains(" Shop_A_2 => ", text);
            Assert.Empty(CSharpSyntaxTree.ParseText(text).GetDiagnostics());
        }

        [Fact]
        public void the_destructable_table_is_written_only_for_code_that_names_them()
        {
            var project = Project();
            var map = Map();
            var code = Path.Combine(project.Folder, "Code.cs");

            Assert.Equal(string.Empty, PlacedWriter.DestructableTable(project, map));

            File.WriteAllText(code, "class A { object B => Placed.Destructables.LTg1_0002; }");
            var some = PlacedWriter.DestructableTable(project, map);
            Assert.Contains("[\"LTg1_0002\"] = { " + "LTg1".FromRawcode() + ", 64, -32 },", some);
            Assert.DoesNotContain("LTlt_0012", some);

            File.WriteAllText(code, "class A { object B => Destructable.Named(\"LTlt_0012\"); }");
            Assert.Contains("[\"LTlt_0012\"] = { " + "LTlt".FromRawcode() + ", 128.5, 0 },", PlacedWriter.DestructableTable(project, map));
        }

        private MapProject Project()
        {
            var folder = folders.TemporaryFolderOutsideRepository();
            Directory.CreateDirectory(Path.Combine(folder, "map"));
            File.WriteAllText(Path.Combine(folder, "map", "war3map.w3i"), string.Empty);
            File.WriteAllText(Path.Combine(folder, "Test.csproj"), "<Project />");
            return MapProject.Find(folder);
        }

        private static War3Net.Build.Map Map()
        {
            var units = new MapUnits(MapWidgetsFormatVersion.v8, MapWidgetsSubVersion.v11, true);
            units.Units.Add(Unit("hfoo", 1, 1));
            units.Units.Add(Unit("h000", 4, 1));
            units.Units.Add(Unit("ratc", 5, 0));
            units.Units.Add(Unit("sloc", 0, 0));
            var doodads = new MapDoodads(MapWidgetsFormatVersion.v8, MapWidgetsSubVersion.v11, true);
            doodads.Doodads.Add(new DoodadData { TypeId = "LTg1".FromRawcode(), CreationNumber = 2, Position = new System.Numerics.Vector3(64, -32, 0) });
            doodads.Doodads.Add(new DoodadData { TypeId = "LTlt".FromRawcode(), CreationNumber = 12, Position = new System.Numerics.Vector3(128.5f, 0, 0) });
            doodads.Doodads.Add(new DoodadData { TypeId = "ZPsh".FromRawcode(), CreationNumber = 13, Position = new System.Numerics.Vector3(0, 0, 0) });
            var regions = new MapRegions(MapRegionsFormatVersion.v5);
            regions.Regions.Add(new Region { Name = "Spawn Left", Left = -512, Bottom = -256, Right = 0, Top = 256.5f });
            var cameras = new MapCameras(MapCamerasFormatVersion.v0, false);
            cameras.Cameras.Add(new WorldEditorCamera { Name = "Intro" });
            var sounds = new War3Net.Build.Audio.MapSounds(War3Net.Build.Audio.MapSoundsFormatVersion.v1);
            sounds.Sounds.Add(new War3Net.Build.Audio.Sound { Name = "gg_snd_Rain" });
            return new War3Net.Build.Map { Units = units, Doodads = doodads, Regions = regions, Cameras = cameras, Sounds = sounds };
        }

        private static UnitData Unit(string type, int number, int heroLevel)
        {
            return new UnitData { TypeId = type.FromRawcode(), CreationNumber = number, HeroLevel = heroLevel, Position = new System.Numerics.Vector3(0, 0, 0) };
        }
    }
}
