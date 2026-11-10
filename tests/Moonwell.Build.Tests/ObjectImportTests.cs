using Moonwell.GameData;
using Moonwell.MapFiles;
using Moonwell.Tool;

namespace Moonwell.Build.Tests
{
    /// <summary><c>moonwell import</c>'s second half: an old map's object data as declared classes, which build back to the same fields (decision 257).</summary>
    [Collection("Sample")]
    public sealed class ObjectImportTests : IDisposable
    {
        private readonly TestFolders folders;

        public ObjectImportTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void each_object_becomes_a_class_whose_build_gives_its_fields_back()
        {
            var project = Project();
            var map = Path.Combine(project, "map");
            Add(map, Kind.Unit, file =>
            {
                var captain = file.AddCustom("hfoo", "h000");
                captain.Set("unam", 0, 0, new FieldValue.Text("Captain of the |cffff0000Guard|r"));
                captain.Set("uhpm", 0, 0, new FieldValue.Int(900));
                captain.Set("udty", 0, 0, new FieldValue.Text("fort"));
                captain.Set("uabi", 0, 0, new FieldValue.Text("Adef,A000"));
                captain.Set("ua1g", 0, 0, new FieldValue.Text("ground,structure"));
                file.AddOriginal("hfoo").Set("uhpm", 0, 0, new FieldValue.Int(1000000));
            });
            Add(map, Kind.Ability, file =>
            {
                var blizzard = file.AddCustom("AHbz", "A000");
                blizzard.Set("anam", 0, 0, new FieldValue.Text("Big Storm"));
                blizzard.Set("Hbz2", 2, 2, new FieldValue.Real(45.5f));
                blizzard.Set("abuf", 1, 0, new FieldValue.Text(string.Empty));
            });
            var log = new StringWriter();

            ObjectImport.Run(project, log);

            Assert.Contains("moonwell: 3 of 3 objects are C# classes in Objects", log.ToString());
            Assert.False(File.Exists(Path.Combine(map, "war3map.w3u")));
            Assert.False(File.Exists(Path.Combine(map, "war3map.w3a")));
            var units = File.ReadAllText(Path.Combine(project, "Objects", "Units.cs"));
            Assert.Contains("    [ObjectId(\"h000\")]\n    public sealed class CaptainOfTheGuard : global::Moonwell.Objects.Human.Units.Footman", units);
            Assert.Contains("            MaxLife = 900;", units);
            Assert.Contains("            DefenseType = global::Moonwell.DefenseType.Fortified;", units);
            Assert.Contains("            Abilities.Set<global::Moonwell.Objects.Human.Abilities.Defend, global::Sample.Objects.BigStorm>();", units);
            Assert.Contains("    [Replaces]\n    [BeyondLimits(\"MaxLife\")]\n    public sealed class Footman : global::Moonwell.Objects.Human.Units.Footman", units);
            var abilities = File.ReadAllText(Path.Combine(project, "Objects", "Abilities.cs"));
            Assert.Contains("            Set(\"Hbz2\", 2, 45.5f);", abilities);
            Assert.Contains("            Buffs.Clear();", abilities);

            Build(project);

            var built = Path.Combine(project, "build", "Sample");
            var captainBuilt = Entry(built, Kind.Unit, "h000");
            Assert.Equal(new FieldValue.Int(900), captainBuilt.Find("uhpm", 0)!.Value);
            Assert.Equal(new FieldValue.Text("fort"), captainBuilt.Find("udty", 0)!.Value);
            Assert.Equal(new FieldValue.Text("Adef,A000"), captainBuilt.Find("uabi", 0)!.Value);
            Assert.Equal(new FieldValue.Int(1000000), Entry(built, Kind.Unit, "hfoo").Find("uhpm", 0)!.Value);
            Assert.Equal(new FieldValue.Unreal(45.5f), Entry(built, Kind.Ability, "A000").Find("Hbz2", 2)!.Value);
        }

        [Fact]
        public void an_object_with_a_field_the_classes_cannot_say_stays_in_the_files_with_one_naming_it()
        {
            var project = Project();
            var map = Path.Combine(project, "map");
            Add(map, Kind.Unit, file =>
            {
                file.AddCustom("hfoo", "h000").Set("umvt", 0, 0, new FieldValue.Text(string.Empty));
                file.AddCustom("hfoo", "h001").Set("uhpm", 0, 0, new FieldValue.Int(500));
            });
            Add(map, Kind.Upgrade, file => file.AddCustom("Rhme", "R000").Set("greq", 1, 0, new FieldValue.Text("h000")));
            var log = new StringWriter();

            ObjectImport.Run(project, log);

            Assert.Contains("moonwell: kept h000 in the object files: its field umvt holds \"\"", log.ToString());
            Assert.Contains("moonwell: kept R000 in the object files", log.ToString());
            Assert.Contains("moonwell: 1 of 3 objects are C# classes in Objects", log.ToString());
            Assert.NotNull(Entry(map, Kind.Unit, "h000"));
            Assert.NotNull(Entry(map, Kind.Upgrade, "R000"));
            Assert.Null(EntryOrNull(map, Kind.Unit, "h001"));

            Build(project);
        }

        // The Sample without its own object classes, so the ids and names are the test's alone.
        private string Project()
        {
            var project = folders.TemporaryCopyOf("Sample");
            File.Delete(Path.Combine(project, "Objects.cs"));
            return project;
        }

        private static void Add(string map, Kind kind, Action<ObjectFile> fill)
        {
            var path = Path.Combine(map, Kinds.MainName(kind));
            var table = SourceObjectFile.Read(path, kind);
            fill(table.File);
            table.Write(path);
        }

        private static ObjectEntry Entry(string folder, Kind kind, string id) => EntryOrNull(folder, kind, id) ?? throw new InvalidOperationException($"no {id} in {folder}");

        private static ObjectEntry? EntryOrNull(string folder, Kind kind, string id)
        {
            var path = Path.Combine(folder, Kinds.MainName(kind));
            if (!File.Exists(path))
            {
                return null;
            }

            var file = ObjectFile.ReadLenient(kind, File.ReadAllBytes(path));
            return file.Custom.Concat(file.Original).FirstOrDefault(entry => SourceObjectFile.IdOf(entry) == id);
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
