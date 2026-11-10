using System.Text;
using Moonwell.MapFiles;
using War3Net.Build.Extensions;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>The C# the build writes from the files in data\ (decision 259), and a map built with it.</summary>
    [Collection("Sample")]
    public sealed class DataWriterTests : IDisposable
    {
        private readonly TestFolders folders;

        public DataWriterTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void a_csv_is_a_table_of_typed_columns_with_its_rows_named_by_the_first()
        {
            var written = Write(("abilities.csv", "Id,Damage,Cooldown:float,Note\nSmite,75;150;225,9,\"Hits, hard\"\nBig Bash,10,5.5,\n"));

            Assert.Contains("    public static class Data\n", written);
            Assert.Contains("        public static class Abilities\n", written);
            Assert.Contains("                public readonly string Id;\n", written);
            Assert.Contains("                public readonly int[] Damage;\n", written);
            Assert.Contains("                public readonly float Cooldown;\n", written);
            Assert.Contains("                public readonly string Note;\n", written);
            Assert.Contains("            public static readonly Row Smite = new Row(\"Smite\", new int[] { 75, 150, 225 }, 9f, \"Hits, hard\");\n", written);
            Assert.Contains("            public static readonly Row BigBash = new Row(\"Big Bash\", new int[] { 10 }, 5.5f, \"\");\n", written);
            Assert.Contains("            public static readonly Row[] All = new Row[] { Smite, BigBash };\n", written);
        }

        [Fact]
        public void a_csv_whose_first_column_is_numbers_has_its_rows_in_all_only()
        {
            var written = Write(("waves.csv", "Wave,Unit,Count\r\n1,Footman,10\r\n2,Knight,4\r\n"));

            Assert.Contains("public static readonly Row[] All = new Row[] { new Row(1, \"Footman\", 10), new Row(2, \"Knight\", 4) };", written);
            Assert.DoesNotContain("public static readonly Row Footman", written);
        }

        [Fact]
        public void a_json_file_is_typed_from_its_shape()
        {
            var written = Write(("quests.json", """
                {
                  "Intro": {
                    "Title": "The Keep",
                    "Gold": 100,
                    "Steps": [ { "Text": "Go", "Count": 1 }, { "Text": "Fight", "Count": 2.5, "Boss": true } ]
                  },
                  "Rates": [1, 2.5]
                }
                """));

            Assert.Contains("        public static class Quests\n", written);
            Assert.Contains("            public static class Intro\n", written);
            Assert.Contains("                public static readonly string Title = \"The Keep\";\n", written);
            Assert.Contains("                public static readonly int Gold = 100;\n", written);
            Assert.Contains("                public sealed class StepsItem\n", written);
            Assert.Contains("                    public readonly float Count;\n", written);
            Assert.Contains("                    public readonly bool Boss;\n", written);
            Assert.Contains("                public static readonly StepsItem[] Steps = new StepsItem[] { new StepsItem(\"Go\", 1f, false), new StepsItem(\"Fight\", 2.5f, true) };\n", written);
            Assert.Contains("            public static readonly float[] Rates = new float[] { 1f, 2.5f };\n", written);
        }

        [Theory]
        [InlineData("bad.csv", "Id,A\nx,1,2\n", @"data\bad.csv:2: 3 values, and the header names 2 columns")]
        [InlineData("bad.csv", "Id,A:int\nx,1.5\n", @"data\bad.csv: the column A is int, and '1.5' is not a whole number")]
        [InlineData("bad.csv", "Id,A\nx,1\nx,2\n", @"data\bad.csv:3: the row's name X is taken by an earlier row")]
        [InlineData("bad.csv", "Id,A:number\nx,1\n", @"data\bad.csv:1: the column A:number asks for the type number; use int, float or text")]
        [InlineData("bad.csv", "Id,A\nÅsa,1\n", @"data\bad.csv:2: the row's name ('Åsa') has the letter Å, and the map's Lua takes names of the letters a to z, digits and _ only")]
        [InlineData("bad.json", "[1, \"a\"]", @"data\bad.json: All holds a number in one place and text in another; a list's items share one shape")]
        [InlineData("2nd.json", "{}", @"data\2nd.json: its file name makes no C# name ('2nd'); start it with a letter")]
        public void a_file_the_build_cannot_make_into_csharp_fails_it(string file, string text, string expected)
        {
            var error = Assert.Throws<BuildException>(() => Write((file, text)));

            Assert.Equal(expected, error.Message);
        }

        [Fact]
        public void without_data_files_an_earlier_builds_file_is_deleted()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            Directory.CreateDirectory(project.GeneratedFolder);
            var left = Path.Combine(project.GeneratedFolder, DataWriter.FileName);
            File.WriteAllText(left, "namespace Left { }");

            DataWriter.Write(project);

            Assert.False(File.Exists(left));
        }

        [Fact]
        public void a_map_reads_its_data_in_an_object_and_in_game_code()
        {
            var copy = folders.TemporaryCopyOfSample();
            var data = Path.Combine(copy, "data");
            Directory.CreateDirectory(data);
            File.WriteAllText(Path.Combine(data, "abilities.csv"), "Id,Damage:float\nSmite,75;150;225\n");

            // Three hundred rows: each is a constructor call, so the Lua's limit of 200 locals in a function is not reached.
            var waves = new StringBuilder("Wave,Unit,Count\n");
            for (var wave = 1; wave <= 300; wave++)
            {
                waves.Append(wave).Append(",Footman,").Append(wave % 7).Append('\n');
            }

            File.WriteAllText(Path.Combine(data, "waves.csv"), waves.ToString());
            File.WriteAllText(Path.Combine(copy, "Objects.cs"), """
                namespace Sample
                {
                    [Moonwell.Objects.ObjectId("A001")]
                    public sealed class Smite : Moonwell.Objects.Human.Abilities.StormBolt
                    {
                        public Smite()
                        {
                            Damage = Levels(Moonwell.Data.Abilities.Smite.Damage);
                        }
                    }

                    public static class Spawns
                    {
                        public static int Count()
                        {
                            var total = 0;
                            foreach (var wave in Moonwell.Data.Waves.All)
                            {
                                total += wave.Count;
                            }

                            return total;
                        }
                    }
                }
                """);

            MapBuild.Run(copy, new BuildOptions(), TextWriter.Null);

            var staged = Path.Combine(copy, "build", "Sample");
            using var reader = new BinaryReader(File.OpenRead(Path.Combine(staged, "war3map.w3a")), Encoding.UTF8);
            var smite = Assert.Single(reader.ReadAbilityObjectData().NewAbilities, one => one.NewId == Rawcodes.IdOf("A001"));
            Assert.Equal(
                new[] { 75f, 150f, 225f },
                smite.Modifications.Where(one => one.Id == Rawcodes.IdOf("Htb1")).OrderBy(one => one.Level).Select(one => Convert.ToSingle(one.Value)).ToArray());
            Assert.Contains("Waves", File.ReadAllText(Path.Combine(staged, "war3map.lua")));
        }

        private string Write(params (string Name, string Text)[] files)
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            Directory.CreateDirectory(project.DataFolder);
            foreach (var (name, text) in files)
            {
                File.WriteAllText(Path.Combine(project.DataFolder, name), text);
            }

            DataWriter.Write(project);
            return File.ReadAllText(Path.Combine(project.GeneratedFolder, DataWriter.FileName));
        }
    }
}
