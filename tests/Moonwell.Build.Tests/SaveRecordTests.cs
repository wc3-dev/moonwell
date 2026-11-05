using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>The record file stops a build whose saves old saves would not survive, naming the line to delete (decision 180).</summary>
    public sealed class SaveRecordTests : IDisposable
    {
        private const string Key = "the key";
        private const string Folder = "Rpg";

        private readonly TestFolders folders;
        private readonly MapProject project;

        public SaveRecordTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
            project = MapProject.Find(folders.TemporaryCopyOf("Sample"));
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        private string RecordPath => Path.Combine(project.Folder, SaveRecord.FileName);

        [Fact]
        public void The_first_build_writes_the_record()
        {
            SaveRecord.Keep(project, Classes(("Level", 1, "int"), ("Name", 2, "string")), Key, Folder);

            var lines = Lines();
            Assert.Contains("key " + SaveRecord.Fingerprint(Key), lines);
            Assert.Contains("folder Rpg", lines);
            Assert.Contains("- 1 int", lines);
            Assert.Contains("- 2 string", lines);
            Assert.Contains("part:Sample.Gear 1 int", lines);
            Assert.DoesNotContain(Key, File.ReadAllText(RecordPath));
        }

        [Fact]
        public void A_removed_field_is_kept_as_removed_and_its_number_stops_the_build_when_used_again()
        {
            SaveRecord.Keep(project, Classes(("Level", 1, "int"), ("Name", 2, "string")), Key, Folder);
            SaveRecord.Keep(project, Classes(("Level", 1, "int")), Key, Folder);
            Assert.Contains("- 2 string removed", Lines());

            var stop = Assert.Throws<BuildException>(() => SaveRecord.Keep(project, Classes(("Level", 1, "int"), ("Title", 2, "string")), Key, Folder));
            var line = Lines().IndexOf("- 2 string removed") + 1;
            Assert.Contains($"{SaveRecord.FileName} line {line}:", stop.Message);
            Assert.Contains("Sample.RpgSave.Title", stop.Message);
            Assert.Contains($"delete line {line}", stop.Message);

            DeleteLine(line);
            SaveRecord.Keep(project, Classes(("Level", 1, "int"), ("Title", 2, "string")), Key, Folder);
            Assert.Contains("- 2 string", Lines());
        }

        [Fact]
        public void A_field_whose_type_changed_stops_the_build()
        {
            SaveRecord.Keep(project, Classes(("Level", 1, "int")), Key, Folder);

            var stop = Assert.Throws<BuildException>(() => SaveRecord.Keep(project, Classes(("Level", 1, "int-list")), Key, Folder));
            Assert.Contains("saved as int-list", stop.Message);
            Assert.Contains($"line {Lines().IndexOf("- 1 int") + 1}", stop.Message);
        }

        [Fact]
        public void A_changed_key_or_folder_stops_the_build_until_its_line_is_deleted()
        {
            SaveRecord.Keep(project, Classes(("Level", 1, "int")), Key, Folder);

            var key = Assert.Throws<BuildException>(() => SaveRecord.Keep(project, Classes(("Level", 1, "int")), "another key", Folder));
            Assert.Contains("SaveKey has changed", key.Message);
            var folder = Assert.Throws<BuildException>(() => SaveRecord.Keep(project, Classes(("Level", 1, "int")), Key, "Other"));
            Assert.Contains("SaveFolder = \"Rpg\"", folder.Message);

            DeleteLine(Lines().IndexOf("key " + SaveRecord.Fingerprint(Key)) + 1);
            SaveRecord.Keep(project, Classes(("Level", 1, "int")), "another key", Folder);
            Assert.Contains("key " + SaveRecord.Fingerprint("another key"), Lines());
        }

        [Fact]
        public void A_lost_record_is_written_anew()
        {
            SaveRecord.Keep(project, Classes(("Level", 1, "int"), ("Name", 2, "string")), Key, Folder);
            File.Delete(RecordPath);

            SaveRecord.Keep(project, Classes(("Level", 1, "int"), ("Title", 2, "int")), Key, Folder);

            Assert.Contains("- 2 int", Lines());
        }

        [Fact]
        public void A_part_or_an_enum_swapped_for_another_stops_the_build()
        {
            SaveRecord.Keep(project, Holding(new SavedType("part", false, false, "Sample.Gear"), new SavedType("int", false, false, "Sample.Kind")), Key, Folder);
            Assert.Contains("- 3 part:Sample.Gear", Lines());

            var part = Assert.Throws<BuildException>(() => SaveRecord.Keep(project, Holding(new SavedType("part", false, false, "Sample.Armor"), new SavedType("int", false, false, "Sample.Kind")), Key, Folder));
            Assert.Contains("saved as part:Sample.Armor", part.Message);
            var choice = Assert.Throws<BuildException>(() => SaveRecord.Keep(project, Holding(new SavedType("part", false, false, "Sample.Gear"), new SavedType("int", false, false, "Sample.Rank")), Key, Folder));
            Assert.Contains("saved as int:Sample.Rank", choice.Message);
        }

        [Fact]
        public void A_record_from_before_names_and_an_int_made_an_enum_are_kept()
        {
            File.WriteAllLines(RecordPath, new[] { "key " + SaveRecord.Fingerprint(Key), "folder Rpg", "- 3 part", "- 4 int" });

            SaveRecord.Keep(project, Holding(new SavedType("part", false, false, "Sample.Gear"), new SavedType("int", false, false, "Sample.Kind")), Key, Folder);

            Assert.Contains("- 3 part:Sample.Gear", Lines());
            Assert.Contains("- 4 int:Sample.Kind", Lines());
        }

        [Fact]
        public void A_changed_save_id_stops_the_build_until_its_line_is_deleted()
        {
            SaveRecord.Keep(project, Classes(("Level", 1, "int")), Key, Folder);
            Assert.Contains("id Sample.RpgSave -", Lines());

            var renamed = new[] { new SaveClass("Sample.RpgSave", false, "rpg", new[] { new SavedMember("Level", 1, Type("int")) }) };
            var stop = Assert.Throws<BuildException>(() => SaveRecord.Keep(project, renamed, Key, Folder));
            var line = Lines().IndexOf("id Sample.RpgSave -") + 1;
            Assert.Contains("Sample.RpgSave's [SaveId] has changed from none to \"rpg\"", stop.Message);
            Assert.Contains($"delete line {line}", stop.Message);

            DeleteLine(line);
            SaveRecord.Keep(project, renamed, Key, Folder);
            Assert.Contains("id Sample.RpgSave rpg", Lines());
        }

        // A save that holds a part as number 3 and an enum as number 4.
        private static IReadOnlyList<SaveClass> Holding(SavedType part, SavedType choice)
        {
            return new[] { new SaveClass("Sample.RpgSave", false, string.Empty, new[] { new SavedMember("Weapon", 3, part), new SavedMember("Kind", 4, choice) }) };
        }

        private static IReadOnlyList<SaveClass> Classes(params (string Name, int Number, string Stored)[] members)
        {
            var saved = members.Select(one => new SavedMember(one.Name, one.Number, Type(one.Stored))).ToList();
            return new[]
            {
                new SaveClass("Sample.RpgSave", false, string.Empty, saved),
                new SaveClass("Sample.Gear", true, string.Empty, new[] { new SavedMember("Id", 1, Type("int")) }),
            };
        }

        private static SavedType Type(string stored)
        {
            return stored.EndsWith("-list", StringComparison.Ordinal)
                ? new SavedType(stored.Substring(0, stored.Length - 5), true, false, null)
                : new SavedType(stored, false, false, null);
        }

        private List<string> Lines()
        {
            return File.ReadAllLines(RecordPath).ToList();
        }

        private void DeleteLine(int line)
        {
            var lines = Lines();
            lines.RemoveAt(line - 1);
            File.WriteAllLines(RecordPath, lines);
        }
    }
}
