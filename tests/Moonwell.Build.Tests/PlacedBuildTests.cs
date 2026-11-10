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
            Assert.Contains("MoonwellPlacedDestructables = {\n[\"LTlt_0012\"] = { " + "LTlt".FromRawcode() + ", 256, -128 },", script);
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

        private static void Write(string project, string body)
        {
            File.WriteAllText(Path.Combine(project, "Settings.cs"), Code.Replace("BODY", body));
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
