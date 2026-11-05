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
