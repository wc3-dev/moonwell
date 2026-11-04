using System.Text;
using Moonwell.MapFiles;
using Moonwell.Tool;
using War3Net.Build.Extensions;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A map whose code uses player files gets Moonwell's store ability, whose tooltips hold a file's pieces when it is read back, and its save folder.</summary>
    [Collection("Sample")]
    public sealed class FileStoreBuildTests : IDisposable
    {
        private const string UsesFiles = "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class Settings : GameComponent\n    {\n        public override void OnGameStart()\n        {\n            PlayerFile.Write(Player.One, \"settings.txt\", \"x\");\n        }\n    }\n}\n";

        private readonly TestFolders folders;

        public FileStoreBuildTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void map_using_files_gets_the_store_ability_and_its_folder()
        {
            var project = folders.TemporaryCopyOf("Sample");
            File.WriteAllText(Path.Combine(project, "Settings.cs"), UsesFiles);

            Build(project);

            var store = StoreIn(project, "war3mapSkin.w3a");
            Assert.NotNull(store);
            Assert.Equal(100, store!.Modifications.Count(one => one.Id == Rawcodes.IdOf("atp1") && (string)one.Value == "-"));
            Assert.Equal(100, store.Modifications.Count(one => one.Id == Rawcodes.IdOf("aub1") && (string)one.Value == "-"));
            var levels = StoreIn(project, "war3map.w3a");
            Assert.Equal(100, Assert.Single(levels!.Modifications, one => one.Id == Rawcodes.IdOf("alev")).Value);

            var main = File.ReadAllText(Path.Combine(project, "build", "generated", "MapMain.g.cs"));
            Assert.Contains("global::Moonwell.Runtime.InstallSaveFolder(\"Samplemap\");", main);
        }

        [Fact]
        public void map_reading_a_file_gets_the_store_ability()
        {
            var project = folders.TemporaryCopyOf("Sample");
            File.WriteAllText(
                Path.Combine(project, "Settings.cs"),
                "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class Settings : GameComponent\n    {\n        public override void OnGameStart()\n        {\n            ReadFile(Player.One, \"settings.txt\", text => Game.Print(text ?? \"none\"));\n        }\n    }\n}\n");

            Build(project);

            Assert.NotNull(StoreIn(project, "war3mapSkin.w3a"));
        }

        [Fact]
        public void the_folder_is_the_maps_SaveFolder_when_it_sets_one()
        {
            var project = folders.TemporaryCopyOf("Sample");
            File.WriteAllText(Path.Combine(project, "Settings.cs"), UsesFiles);
            var map = Path.Combine(project, "Map.cs");
            File.WriteAllText(map, File.ReadAllText(map).Replace("Name = \"Sample map\";", "Name = \"Sample map\";\n            SaveFolder = \"My-Rpg 2\";"));

            Build(project);

            Assert.Contains("global::Moonwell.Runtime.InstallSaveFolder(\"MyRpg2\");", File.ReadAllText(Path.Combine(project, "build", "generated", "MapMain.g.cs")));
        }

        [Fact]
        public void map_without_files_gets_neither()
        {
            var project = folders.TemporaryCopyOf("Sample");

            Build(project);

            Assert.Null(StoreIn(project, "war3mapSkin.w3a"));
            Assert.DoesNotContain("InstallSaveFolder", File.ReadAllText(Path.Combine(project, "build", "generated", "MapMain.g.cs")));
        }

        [Fact]
        public void map_with_objects_it_declares_nothing_with_gets_the_store()
        {
            var project = folders.TemporaryCopyOf("Sample");
            File.Delete(Path.Combine(project, "Objects.cs"));
            File.WriteAllText(Path.Combine(project, "Settings.cs"), UsesFiles);

            Build(project);

            Assert.NotNull(StoreIn(project, "war3mapSkin.w3a"));
        }

        [Fact]
        public void map_using_files_without_objects_stops()
        {
            var project = folders.TemporaryCopyOf("Sample");
            File.Delete(Path.Combine(project, "Objects.cs"));
            var csproj = Path.Combine(project, "Sample.csproj");
            File.WriteAllText(csproj, string.Join("\n", File.ReadAllLines(csproj).Where(line => !line.Contains("Moonwell.Objects"))));
            File.WriteAllText(Path.Combine(project, "Settings.cs"), UsesFiles);

            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));

            Assert.True(exitCode == 1, "the build should fail: " + output);
            Assert.Contains("PlayerFile reads files back through an ability Moonwell declares, which needs the Moonwell.Objects package", error.ToString());
        }

        private static War3Net.Build.Object.LevelObjectModification? StoreIn(string project, string file)
        {
            var path = Path.Combine(project, "build", "Sample", file);
            if (!File.Exists(path))
            {
                return null;
            }

            using var reader = new BinaryReader(File.OpenRead(path), Encoding.UTF8);
            return reader.ReadAbilityObjectData().NewAbilities.SingleOrDefault(one => one.NewId == Rawcodes.IdOf("AMWf"));
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
