using System.Text;
using Moonwell.Tool;
using War3Net.Build.Extensions;
using War3Net.Build.Widget;
using War3Net.Common.Extensions;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A map's code reaching what an editor placed, through Placed and the Named lookups (decision 254).</summary>
    [Collection("Sample")]
    public sealed class PlacedBuildTests : IDisposable
    {
        private const string Code = "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class Settings : GameComponent\n    {\n        public override void OnGameStart()\n        {\n            BODY\n        }\n    }\n}\n";

        private readonly TestFolders folders;

        public PlacedBuildTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void code_naming_placed_units_and_items_builds_and_the_script_keeps_them_in_their_globals()
        {
            var project = folders.TemporaryCopyOf("Sample");
            AddItem(project, "ratc", 8);
            Write(project, "Placed.Units.Hpal_0002?.Kill(); Unit.Named(\"Hblm_0003\")?.Kill(); Placed.Items.ratc_0008?.Remove();");

            var (exitCode, output) = Build(project);

            Assert.True(exitCode == 0, output);
            var script = File.ReadAllText(Path.Combine(project, "build", "Sample", "war3map.lua"));
            Assert.Contains("gg_unit_Hpal_0002 = ", script);
            Assert.Contains("gg_item_ratc_0008 = CreateItem(FourCC(\"ratc\")", script);
            Assert.DoesNotContain("MoonwellPlacedDestructables = {", script);
        }

        [Fact]
        public void code_naming_a_placed_destructable_gets_the_table_the_runtime_finds_it_by()
        {
            var project = folders.TemporaryCopyOf("Sample");
            AddDestructable(project, "LTlt", 12);
            Write(project, "Destructable.Named(\"LTlt_0012\")?.Kill();");

            var (exitCode, output) = Build(project);

            Assert.True(exitCode == 0, output);
            var script = File.ReadAllText(Path.Combine(project, "build", "Sample", "war3map.lua"));
            Assert.Contains("MoonwellPlacedDestructables = {\n\"LTlt_0012\", 1280601204, 256, -128,\n", script);
        }

        [Fact]
        public void code_naming_a_placed_destructable_through_using_static_gets_the_table()
        {
            var project = folders.TemporaryCopyOf("Sample");
            AddDestructable(project, "LTlt", 12);
            File.WriteAllText(Path.Combine(project, "Settings.cs"), "using static Moonwell.Destructable;\n" + Code.Replace("BODY", "Named(\"LTlt_0012\")?.Kill();"));

            var (exitCode, output) = Build(project);

            Assert.True(exitCode == 0, output);
            var script = File.ReadAllText(Path.Combine(project, "build", "Sample", "war3map.lua"));
            Assert.Contains("\"LTlt_0012\", ", script);
        }

        [Fact]
        public void a_comment_naming_destructable_named_or_placed_destructables_gets_no_table()
        {
            var project = folders.TemporaryCopyOf("Sample");
            AddDestructable(project, "LTlt", 12);
            Write(project, "// Destructable.Named and Placed.Destructables are not used here." + Environment.NewLine + "            Game.Print(\"hi\");");

            var (exitCode, output) = Build(project);

            Assert.True(exitCode == 0, output);
            var script = File.ReadAllText(Path.Combine(project, "build", "Sample", "war3map.lua"));
            Assert.DoesNotContain("MoonwellPlacedDestructables = {", script);
        }

        [Fact]
        public void placed_destructables_named_only_in_placed_destructables_get_the_table_but_the_trees()
        {
            var project = folders.TemporaryCopyOf("Sample");
            AddDestructable(project, "LTlt", 12);
            AddDestructable(project, "YTpb", 13);
            Write(project, "Placed.Destructables.YTpb_0013?.Kill();");

            var (exitCode, output) = Build(project);

            Assert.True(exitCode == 0, output);
            var script = File.ReadAllText(Path.Combine(project, "build", "Sample", "war3map.lua"));
            Assert.Contains("\"YTpb_0013\", ", script);
            Assert.DoesNotContain("\"LTlt_0012\", ", script);
        }

        [Fact]
        public void a_name_the_map_has_no_placed_unit_of_fails_the_build_at_its_line()
        {
            var project = folders.TemporaryCopyOf("Sample");
            Write(project, "Unit.Named(\"hfoo_0099\")?.Kill();");

            var (exitCode, output) = Build(project);

            Assert.Equal(1, exitCode);
            Assert.Contains("Settings.cs:9: the map has no World Editor placed unit named 'hfoo_0099'. Its placed units: Hblm_0003, Hpal_0002.", output);
        }

        [Theory]
        [InlineData("unit")]
        [InlineData("item")]
        [InlineData("destructable")]
        public void a_map_with_400_placed_things_of_a_kind_builds_and_keeps_their_names(string kind)
        {
            var project = folders.TemporaryCopyOf("Sample");
            for (var number = 100; number < 500; number++)
            {
                switch (kind)
                {
                    case "unit":
                        AddUnit(project, "hfoo", number);
                        break;
                    case "item":
                        AddItem(project, "ratc", number);
                        break;
                    default:
                        AddDestructable(project, "YTpb", number);
                        break;
                }
            }

            Write(project, kind switch
            {
                "unit" => "Placed.Units.hfoo_0100?.Kill(); Placed.Units.hfoo_0499?.Kill();",
                "item" => "Placed.Items.ratc_0100?.Remove(); Placed.Items.ratc_0499?.Remove();",
                _ => "Placed.Destructables.YTpb_0100?.Kill(); Placed.Destructables.YTpb_0499?.Kill();",
            });

            var (exitCode, output) = Build(project);

            Assert.True(exitCode == 0, output);
        }

        private static void Write(string project, string body)
        {
            File.WriteAllText(Path.Combine(project, "Settings.cs"), Code.Replace("BODY", body));
        }

        // A copy of a placed unit of war3mapUnits.doo, of another type and number.
        private static void AddUnit(string project, string type, int number)
        {
            var path = Path.Combine(project, "map", "war3mapUnits.doo");
            var units = ReadUnits(path);
            var unit = ReadUnits(path).Units.First(placed => placed.IsUnit() && !placed.IsPlayerStartLocation());
            unit.TypeId = type.FromRawcode();
            unit.SkinId = unit.TypeId;
            unit.CreationNumber = number;
            units.Units.Add(unit);
            using var writer = new BinaryWriter(File.Create(path), Encoding.UTF8);
            writer.Write(units);
        }

        // A copy of a start location of war3mapUnits.doo, made an item: it carries every field a placed item has.
        private static void AddItem(string project, string type, int number)
        {
            var path = Path.Combine(project, "map", "war3mapUnits.doo");
            var units = ReadUnits(path);
            var item = ReadUnits(path).Units.First(unit => unit.IsPlayerStartLocation());
            item.TypeId = type.FromRawcode();
            item.SkinId = item.TypeId;
            item.CreationNumber = number;
            units.Units.Add(item);
            using var writer = new BinaryWriter(File.Create(path), Encoding.UTF8);
            writer.Write(units);
        }

        private static void AddDestructable(string project, string type, int number)
        {
            var path = Path.Combine(project, "map", "war3map.doo");
            MapDoodads doodads;
            using (var reader = new BinaryReader(File.OpenRead(path), Encoding.UTF8))
            {
                doodads = reader.ReadMapDoodads();
            }

            doodads.UseNewFormat = true;

            doodads.Doodads.Add(new DoodadData
            {
                TypeId = type.FromRawcode(),
                SkinId = type.FromRawcode(),
                Position = new System.Numerics.Vector3(256, -128, 0),
                Scale = new System.Numerics.Vector3(1, 1, 1),
                State = DoodadState.Normal,
                Life = 100,
                MapItemTableId = -1,
                CreationNumber = number,
            });
            using var writer = new BinaryWriter(File.Create(path), Encoding.UTF8);
            writer.Write(doodads);
        }

        private static MapUnits ReadUnits(string path)
        {
            using var reader = new BinaryReader(File.OpenRead(path), Encoding.UTF8);
            return reader.ReadMapUnits();
        }

        private static (int ExitCode, string Output) Build(string project)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));
            return (exitCode, error.ToString() + output);
        }
    }
}
