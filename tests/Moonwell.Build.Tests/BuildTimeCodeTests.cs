
using Moonwell.BuildTime;
using War3Net.Build.Environment;
using War3Net.Build.Extensions;
using War3Net.Build.Widget;
using War3Net.Common.Extensions;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>
    /// A map's build code (decisions 371 to 376), run as the build runs it before the compile, on copies of the BuildTime fixture:
    /// its two steps are the two uses that show the system is general, a district probe and a tower defense's checks.
    /// </summary>
    public sealed class BuildTimeCodeTests : IDisposable
    {
        private readonly TestFolders folders;

        public BuildTimeCodeTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void what_build_code_gives_is_written_into_data_by_name()
        {
            var folder = folders.TemporaryCopyOf("BuildTime");
            var (project, log) = Run(folder);
            var data = Data(project);

            // The fixture's map is flat, dry ground: every cell of its two regions can be built on, and it has no water or trees.
            Assert.Contains("        /// <summary>What the build code's Give(\"Districts\") holds.</summary>\n", data);
            Assert.Contains(Lines(
                "            public static class Shop",
                "            {",
                "                public static readonly int BuildableCells = 256;",
                "                public static readonly int FootprintColumns = 16;",
                "                public static readonly int FootprintRows = 16;",
                "                public static readonly bool TouchesWater = false;",
                "                public static readonly float ToWater = -1f;",
                "            }"), data);
            Assert.Contains("                public static readonly int BuildableCells = 1024;\n", data);
            Assert.Contains(Lines(
                "        public static class Trees",
                "        {",
                "            public static class Shop",
                "            {",
                "                public static readonly int Count = 0;"), data);
            Assert.Equal("note: the build code's 2 reports are in build\\reports\\" + Environment.NewLine, log);
        }

        [Fact]
        public void the_first_use_a_district_probe_follows_the_maps_pathing()
        {
            var folder = folders.TemporaryCopyOf("BuildTime");
            Pathing(folder, pathing =>
            {
                // Four columns of the Shop cannot be built on, and deep water lies along the Arena's right side.
                Set(pathing, -512f, -512f, -384f, 0f, 0x48);
                Set(pathing, 1536f, 512f, 1664f, 1536f, 0x0A);
            });

            var (project, _) = Run(folder);
            var data = Data(project);

            Assert.Contains(Lines(
                "            public static class Shop",
                "            {",
                "                public static readonly int BuildableCells = 192;",
                "                public static readonly int FootprintColumns = 12;",
                "                public static readonly int FootprintRows = 16;",
                "                public static readonly bool TouchesWater = false;",
                "                public static readonly float ToWater = 1659."), data.Replace("1659.", "1659.\n"));
            Assert.Contains(Lines(
                "            public static class Arena",
                "            {",
                "                public static readonly int BuildableCells = 1024;",
                "                public static readonly int FootprintColumns = 32;",
                "                public static readonly int FootprintRows = 32;",
                "                public static readonly bool TouchesWater = true;",
                "                public static readonly float ToWater = 32f;"), data);
        }

        [Fact]
        public void the_second_use_a_tower_defense_fails_the_build_when_the_creeps_are_walled_in()
        {
            var folder = folders.TemporaryCopyOf("BuildTime");

            // A wall one cell thick across the whole map, between the Shop and the Arena.
            Pathing(folder, pathing => Set(pathing, -4096f, 256f, 4096f, 288f, 0xCE));

            var failure = Assert.Throws<BuildException>(() => Run(folder));

            Assert.Equal("the creeps cannot walk from the Shop to the Arena: something blocks every way between them", failure.Message);
            Assert.Empty(BuildTimeCode.GiveFiles(MapProject.Find(folder)));
        }

        [Fact]
        public void the_second_use_counts_the_trees_in_each_region()
        {
            var folder = folders.TemporaryCopyOf("BuildTime");
            Doodads(folder, ("LTlt", 600f, 600f), ("ATtr", 700f, 900f), ("LTlt", 1500f, 1500f), ("LTlt", -100f, -100f), ("LTbr", 800f, 800f), ("LTlt", 3000f, 3000f));

            var (project, _) = Run(folder);
            var data = Data(project);

            // Three trees in the Arena, with a barrel that is no tree; one in the Shop; one in no region.
            Assert.Contains(Lines(
                "            public static class Shop",
                "            {",
                "                public static readonly int Count = 1;",
                "            }",
                "            public static class Arena",
                "            {",
                "                public static readonly int Count = 3;"), data);
            Assert.Equal("The creeps reach 43264 cells from the Shop.\n", File.ReadAllText(Path.Combine(folder, "build", "reports", "creep-path.txt")));
        }

        [Fact]
        public void a_report_and_a_picture_go_to_the_build_folder_only()
        {
            var folder = folders.TemporaryCopyOf("BuildTime");
            Run(folder);

            var reports = Path.Combine(folder, "build", "reports");
            Assert.Equal(new[] { "creep-path.txt", "districts.png" }, Directory.GetFiles(reports).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, File.ReadAllBytes(Path.Combine(reports, "districts.png")).Take(4));

            // Nothing was written into the map or the assets.
            Assert.False(Directory.Exists(Path.Combine(folder, "assets")));
            Assert.Equal(Directory.GetFiles(TestFolders.Fixture(Path.Combine("BuildTime", "map"))).Length, Directory.GetFiles(Path.Combine(folder, "map")).Length);
        }

        [Fact]
        public void nothing_runs_again_while_nothing_it_could_read_has_changed()
        {
            var folder = folders.TemporaryCopyOf("BuildTime");
            File.WriteAllText(Path.Combine(folder, "buildtime", "Counter.cs"), """
                using System.IO;
                using Moonwell.BuildTime;

                public sealed class Counter : BuildStep
                {
                    public override void Run()
                    {
                        File.AppendAllText(Path.Combine(Path.GetTempPath(), "RUNS"), "run\n");
                        Warn("the counter ran");
                    }
                }
                """.Replace("RUNS", "moonwell-runs-" + Path.GetFileName(folder)));
            var runs = Path.Combine(Path.GetTempPath(), "moonwell-runs-" + Path.GetFileName(folder));
            try
            {
                int Count() => File.ReadAllLines(runs).Length;

                var (_, first) = Run(folder);
                Assert.Equal(1, Count());
                Assert.Contains("warning: the counter ran", first);

                // The same again: nothing runs, and the warning is printed again from what was kept.
                var (project, second) = Run(folder);
                Assert.Equal(1, Count());
                Assert.Equal(first, second);
                Assert.Equal(2, BuildTimeCode.GiveFiles(project).Count);

                // A changed map file, a changed file of the build code, a deleted report and the other kind of build each run it again.
                File.SetLastWriteTimeUtc(Path.Combine(folder, "map", "war3map.w3r"), DateTime.UtcNow.AddMinutes(1));
                Run(folder);
                Assert.Equal(2, Count());

                File.SetLastWriteTimeUtc(Path.Combine(folder, "buildtime", "Districts.cs"), DateTime.UtcNow.AddMinutes(2));
                Run(folder);
                Assert.Equal(3, Count());

                File.Delete(Path.Combine(folder, "build", "reports", "districts.png"));
                Run(folder);
                Assert.Equal(4, Count());

                Run(folder, release: true);
                Assert.Equal(5, Count());
                Run(folder, release: true);
                Assert.Equal(5, Count());
            }
            finally
            {
                File.Delete(runs);
            }
        }

        [Fact]
        public void a_failed_run_is_never_kept()
        {
            var folder = folders.TemporaryCopyOf("BuildTime");
            Run(folder);
            Pathing(folder, pathing => Set(pathing, -4096f, 256f, 4096f, 288f, 0xCE));

            Assert.Throws<BuildException>(() => Run(folder));
            Assert.Throws<BuildException>(() => Run(folder));

            // What the earlier, good run left is gone, so the game cannot be built with its old numbers.
            var project = MapProject.Find(folder);
            Assert.Empty(BuildTimeCode.GiveFiles(project));
            Assert.False(Directory.Exists(Path.Combine(folder, "build", "reports")));
        }

        [Fact]
        public void a_map_without_build_code_runs_nothing_and_what_an_earlier_build_left_is_removed()
        {
            var folder = folders.TemporaryCopyOf("BuildTime");
            var steps = new List<string>();
            var project = MapProject.Find(folder);
            BuildTimeCode.Run(project, new BuildOptions { Step = steps.Add }, new StringWriter());
            Assert.Equal(new[] { "Run build code" }, steps);
            Assert.Contains("Districts", Data(project));

            Directory.Delete(Path.Combine(folder, "buildtime"), true);
            steps.Clear();
            BuildTimeCode.Run(project, new BuildOptions { Step = steps.Add }, new StringWriter());

            Assert.Empty(steps);
            Assert.Empty(BuildTimeCode.GiveFiles(project));
            Assert.False(Directory.Exists(Path.Combine(folder, "build", "reports")));
            DataWriter.Write(project);
            Assert.False(File.Exists(Path.Combine(project.GeneratedFolder, "Data.g.cs")));
        }

        [Fact]
        public void a_give_keeps_each_numbers_type_and_names_come_from_the_values()
        {
            var folder = Only("""
                using System.Collections.Generic;
                using Moonwell.BuildTime;

                public enum Kind { Land, Sea }

                public sealed record Camp(string Name, int Level, float Share, Kind Kind);

                public sealed class Gives : BuildStep
                {
                    public override void Run()
                    {
                        Give("Numbers", new { Whole = 3, Decimal = 3f, Half = 0.5, Text = "three", Yes = true });
                        Give("Camps", new List<Camp> { new Camp("North Gate", 2, 1f, Kind.Land), new Camp("Dock", 5, 0.25f, Kind.Sea) });
                        Give("ByRegion", new Dictionary<string, int[]> { ["Old Town"] = new[] { 1, 2 }, ["harbor"] = new int[0] });
                        Give("Empty", new { Camps = new List<Camp>(), Shares = new float[0], Names = new HashSet<string>(), Grid = new List<int[]>() });
                    }
                }
                """);

            var (project, log) = Run(folder);
            var data = Data(project);

            Assert.Equal(string.Empty, log);
            Assert.Contains("            public static readonly int Whole = 3;\n", data);
            Assert.Contains("            public static readonly float Decimal = 3f;\n", data);
            Assert.Contains("            public static readonly float Half = 0.5f;\n", data);
            Assert.Contains("            public static readonly string Text = \"three\";\n", data);
            Assert.Contains("            public static readonly bool Yes = true;\n", data);
            Assert.Contains("new Item(\"North Gate\", 2, 1f, \"Land\"), new Item(\"Dock\", 5, 0.25f, \"Sea\")", data);
            Assert.Contains("        public static class ByRegion\n", data);
            Assert.Contains("            public static readonly int[] OldTown = new int[] { 1, 2 };\n", data);
            Assert.Contains("            public static readonly int[] Harbor = new int[0];\n", data);

            // An empty list keeps its items' type, so the game code that reads it compiles whatever the map holds.
            Assert.Contains("            public static readonly CampsItem[] Camps = new CampsItem[0];\n", data);
            Assert.Contains("                public CampsItem(string p0, int p1, float p2, string p3)\n", data);
            Assert.Contains("            public static readonly float[] Shares = new float[0];\n", data);
            Assert.Contains("            public static readonly string[] Names = new string[0];\n", data);
            Assert.Contains("            public static readonly int[][] Grid = new int[][0];\n", data);
        }

        [Fact]
        public void a_give_and_a_data_file_of_one_name_is_an_error_naming_both()
        {
            var folder = folders.TemporaryCopyOf("BuildTime");
            Directory.CreateDirectory(Path.Combine(folder, "data"));
            File.WriteAllText(Path.Combine(folder, "data", "trees.json"), "{ \"Oak\": 1 }");
            var (project, _) = Run(folder);

            var failure = Assert.Throws<BuildException>(() => DataWriter.Write(project));

            Assert.Equal("data\\trees.json and the build code's Give(\"Trees\") both make Data.Trees; rename one", failure.Message);
        }

        [Theory]
        [InlineData("Give(\"Trees\", 1); Give(\"Trees\", 2);", "Give(\"Trees\") is called twice; give each name once")]
        [InlineData("Give(\"my trees\", 1);", "Give(\"my trees\"): the name becomes a class in Data, so it is letters, digits and _, starting with a letter")]
        [InlineData("Give(\"Trees\", null!);", "Give(\"Trees\"): there is no value to give")]
        [InlineData("Report(\"notes/trees.txt\", \"x\");", "Report(\"notes/trees.txt\"): a report is named by a plain file name, with no folder")]
        [InlineData("Report(\"a.txt\", \"x\"); Report(\"A.TXT\", \"y\");", "Report(\"A.TXT\") is called twice; name each report once")]
        [InlineData("var region = Map.Region(\"Sho\");", "the map has no region named Sho: it has Arena, Shop")]
        [InlineData("var bytes = Map.Bytes(\"war3map.nope\");", "the map has no file war3map.nope")]
        [InlineData("Fail(\"region Harbor has no buildable ground\");", "region Harbor has no buildable ground")]
        [InlineData("throw new BuildFailure(\"from a helper\");", "from a helper")]
        public void a_steps_own_error_is_the_line_the_build_prints(string body, string message)
        {
            var folder = Only("using Moonwell.BuildTime;\npublic sealed class Step : BuildStep\n{\n    public override void Run()\n    {\n        " + body + "\n    }\n}\n");

            Assert.Equal(message, Assert.Throws<BuildException>(() => Run(folder)).Message);
        }

        [Fact]
        public void any_other_exception_names_the_step_and_its_line()
        {
            var folder = Only("""
                using System;
                using Moonwell.BuildTime;

                namespace Checks
                {
                    public sealed class Boom : BuildStep
                    {
                        public override void Run()
                        {
                            Helper.Go();
                        }
                    }

                    public static class Helper
                    {
                        public static void Go()
                        {
                            throw new InvalidOperationException("the cake fell");
                        }
                    }
                }
                """);

            Assert.Equal("buildtime/Only.cs:18: Checks.Boom threw: the cake fell", Assert.Throws<BuildException>(() => Run(folder)).Message);
        }

        [Fact]
        public void a_compile_error_is_printed_as_the_compilers_and_stops_the_build()
        {
            var folder = Only("""
                using Moonwell.BuildTime;

                public sealed class Broken : BuildStep
                {
                    public override void Run()
                    {
                        int trees = "many";
                    }
                }
                """);

            var failure = Assert.Throws<BuildException>(() => Run(folder));

            Assert.Equal(Path.Combine(folder, "buildtime", "Only.cs") + "(7,21): error CS0029: Cannot implicitly convert type 'string' to 'int'", failure.Message);
        }

        [Fact]
        public void build_code_cannot_see_game_code()
        {
            // Moonwell's game classes and the map's own, Data and Placed among them, are not there when build code compiles.
            var folder = Only("""
                using Moonwell.BuildTime;

                public sealed class Crossing : BuildStep
                {
                    public override void Run()
                    {
                        Moonwell.Unit unit = null!;
                        var map = new BuildTimeFixture.BuildTimeMap();
                    }
                }
                """);

            var failure = Assert.Throws<BuildException>(() => Run(folder));

            Assert.Contains("(7,18): error CS0234: The type or namespace name 'Unit' does not exist in the namespace 'Moonwell'", failure.Message);
            Assert.Contains("(8,23): error CS0246: The type or namespace name 'BuildTimeFixture' could not be found", failure.Message);
        }

        [Fact]
        public void a_warning_of_the_compiler_and_of_a_step_is_printed_and_a_folder_with_no_step_says_so()
        {
            var folder = Only("""
                using Moonwell.BuildTime;

                public sealed class Careless : BuildStep
                {
                    public override void Run()
                    {
                        string? name = null;
                        Warn("the map has " + name.Length + " regions");
                    }
                }
                """);
            var failure = Assert.Throws<BuildException>(() => Run(folder));
            Assert.StartsWith("buildtime/Only.cs:8: Careless threw: ", failure.Message);

            File.WriteAllText(Path.Combine(folder, "buildtime", "Only.cs"), "public static class NoStep { }\n");
            var (_, log) = Run(folder);
            Assert.Equal("warning: buildtime\\ has no class that inherits BuildStep, so no build code ran" + Environment.NewLine, log);

            File.WriteAllText(Path.Combine(folder, "buildtime", "Only.cs"), """
                using Moonwell.BuildTime;

                public sealed class Tidy : BuildStep
                {
                    public override void Run()
                    {
                        var unused = 1;
                        Warn($"the map has {Map.Regions.Regions.Count} regions");
                    }
                }
                """);
            (_, log) = Run(folder);
            Assert.Equal(new[]
            {
                Path.Combine(folder, "buildtime", "Only.cs") + "(7,13): warning CS0219: The variable 'unused' is assigned but its value is never used",
                "warning: the map has 2 regions",
            }, log.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        }

        [Fact]
        public void steps_run_in_the_order_of_their_names_each_once()
        {
            var folder = Only("""
                using System.Collections.Generic;
                using Moonwell.BuildTime;

                public abstract class Named : BuildStep
                {
                    protected static readonly List<string> Ran = new List<string>();

                    public override void Run()
                    {
                        Ran.Add(GetType().Name);
                    }
                }

                public sealed class Zebra : Named
                {
                    public override void Run()
                    {
                        base.Run();
                        Give("Order", Ran);
                    }
                }

                public sealed class Apple : Named { }

                namespace Later
                {
                    public sealed class Mango : Named { }
                }
                """);

            var (project, _) = Run(folder);

            Assert.Contains("public static readonly string[] All = new string[] { \"Apple\", \"Mango\", \"Zebra\" };", Data(project));
        }

        [Fact]
        public void the_reader_hands_out_each_file_as_war3net_parsed_it()
        {
            var folder = folders.TemporaryCopyOf("BuildTime");
            Directory.CreateDirectory(Path.Combine(folder, "assets", "Notes"));
            File.WriteAllText(Path.Combine(folder, "assets", "Notes", "rules.txt"), "from assets");
            File.WriteAllText(Path.Combine(folder, "assets", "war3map.wts"), "STRING 7\r\n{\r\nHello\r\n}\r\n");
            File.Delete(Path.Combine(folder, "map", "war3map.w3c"));
            var map = new MapReader(Path.Combine(folder, "map"), Path.Combine(folder, "assets"));

            Assert.Equal(64u, map.Environment.Width);
            Assert.Equal((256u, 256u), (map.PathingMap.Width, map.PathingMap.Height));
            Assert.Equal(65536, map.Cells.Count);
            Assert.Same(map.PathingMap, map.PathingMap);
            Assert.NotNull(map.Info.MapName);
            Assert.Equal(new[] { "Shop", "Arena" }, map.Regions.Regions.Select(region => region.Name));
            Assert.Equal(-512f, map.Region("Shop").Left);
            Assert.Equal(7, map.Units.Units.Count);
            Assert.Empty(map.Doodads.Doodads);

            // A file the map does not have reads as empty, and a file in assets stands in for the map's.
            Assert.Empty(map.Cameras.Cameras);
            Assert.Equal("Hello", map.TriggerStrings.Strings.Single(text => text.Key == 7).Value);
            Assert.Equal("from assets", System.Text.Encoding.UTF8.GetString(map.Bytes(@"Notes\rules.txt")));
            Assert.True(map.Has("war3map.w3e"));
            Assert.False(map.Has("war3map.w3c"));
            Assert.Contains(@"Notes\rules.txt", map.Files);
            Assert.Contains("war3map.wpm", map.Files);
            Assert.Single(map.Files, name => name == "war3map.wts");

            File.Delete(Path.Combine(folder, "map", "war3map.wpm"));
            var without = new MapReader(Path.Combine(folder, "map"));
            Assert.Equal("the map has no war3map.wpm: save the map in World Editor", Assert.Throws<BuildFailure>(() => without.PathingMap).Message);

            File.WriteAllBytes(Path.Combine(folder, "map", "war3map.w3r"), new byte[] { 1, 2, 3 });
            Assert.StartsWith("cannot read war3map.w3r: ", Assert.Throws<BuildFailure>(() => without.Regions).Message);
        }

        private static (MapProject Project, string Log) Run(string folder, bool release = false)
        {
            var project = MapProject.Find(folder);
            var log = new StringWriter();
            BuildTimeCode.Run(project, new BuildOptions { Release = release }, log);
            return (project, log.ToString());
        }

        private static string Data(MapProject project)
        {
            DataWriter.Write(project);
            return File.ReadAllText(Path.Combine(project.GeneratedFolder, "Data.g.cs"));
        }

        private static string Lines(params string[] lines)
        {
            return string.Join("\n", lines) + "\n";
        }

        // A copy of the fixture whose build code is one file, Only.cs, of the text given.
        private string Only(string source)
        {
            var folder = folders.TemporaryCopyOf("BuildTime");
            Directory.Delete(Path.Combine(folder, "buildtime"), true);
            Directory.CreateDirectory(Path.Combine(folder, "buildtime"));
            File.WriteAllText(Path.Combine(folder, "buildtime", "Only.cs"), source);
            return folder;
        }

        private static void Pathing(string folder, Action<MapPathingMap> change)
        {
            var path = Path.Combine(folder, "map", "war3map.wpm");
            MapPathingMap pathing;
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                pathing = reader.ReadMapPathingMap();
            }

            change(pathing);
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(pathing);
        }

        // Sets the byte of every cell of a rectangle of the fixture's map, whose bottom left corner is at (-4096, -4096).
        private static void Set(MapPathingMap pathing, float left, float bottom, float right, float top, byte flags)
        {
            for (var row = (int)((bottom + 4096f) / 32f); row < (int)((top + 4096f) / 32f); row++)
            {
                for (var column = (int)((left + 4096f) / 32f); column < (int)((right + 4096f) / 32f); column++)
                {
                    pathing.Cells[(row * (int)pathing.Width) + column] = (PathingType)flags;
                }
            }
        }

        private static void Doodads(string folder, params (string Type, float X, float Y)[] placed)
        {
            var path = Path.Combine(folder, "map", "war3map.doo");
            MapDoodads doodads;
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                doodads = reader.ReadMapDoodads();
            }

            foreach (var (type, x, y) in placed)
            {
                doodads.Doodads.Add(new DoodadData { TypeId = type.FromRawcode(), Position = new System.Numerics.Vector3(x, y, 0f), Scale = System.Numerics.Vector3.One, Life = 100, CreationNumber = doodads.Doodads.Count + 1 });
            }

            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(doodads);
        }
    }
}
