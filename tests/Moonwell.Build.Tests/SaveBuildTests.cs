using Moonwell.Tool;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A map with save classes gets the code that writes and reads each, a SaveKey checked, and its record file (decision 180).</summary>
    [Collection("Sample")]
    public sealed class SaveBuildTests : IDisposable
    {
        private const string Saves = """
            using System.Collections.Generic;
            using Moonwell;

            namespace Sample
            {
                public enum Kind { Warrior, Mage }

                public sealed class Gear
                {
                    [Saved(1)] public int Id;
                    [Saved(2)] public string Name = "";
                }

                public sealed class RpgSave : SaveGame
                {
                    [Saved(1)] public int Level;
                    [Saved(2)] public string Hero = "";
                    [Saved(3)] public Kind Kind;
                    [Saved(4)] public List<int> Items = new List<int>();
                    [Saved(5)] public string[] Titles = new string[0];
                    [Saved(6)] public RealTime Claimed;
                    [Saved(7)] public Gear? Weapon;
                    [Saved(8)] public List<Gear> Bag = new List<Gear>();
                    [Saved(9)] public Kind[] Past = new Kind[0];
                    [Saved(10)] public float Speed { get; set; }
                }

                [SaveId("settings")]
                public sealed class Settings : SaveGame
                {
                    [Saved(1)] public bool Music;
                }

                public static class SaveRoundTrip
                {
                    public static string Run()
                    {
                        var save = new RpgSave
                        {
                            Level = 7,
                            Hero = "Åsa",
                            Kind = Kind.Mage,
                            Items = new List<int> { 1, ObjectTypes.FourCC("I000") },
                            Titles = new[] { "a", "b" },
                            Claimed = new RealTime(1793664000),
                            Weapon = new Gear { Id = 3, Name = "axe" },
                            Bag = new List<Gear> { new Gear { Id = 4 } },
                            Past = new[] { Kind.Mage, Kind.Warrior },
                            Speed = 0.5f,
                        };
                        SaveGame.Save(Player.One, save);
                        var back = SaveGame.FromCode<RpgSave>(Player.One, save.Code);
                        if (back == null)
                        {
                            return "null";
                        }

                        return back.Level + " " + back.Hero + " " + (int)back.Kind + " " + (back.Items[1] == ObjectTypes.FourCC("I000")) + " " + back.Titles[1] + " "
                            + back.Claimed.Seconds + " " + back.Weapon!.Name + " " + back.Bag[0].Id + " " + (int)back.Past[0] + (int)back.Past[1] + " " + (back.Speed == 0.5f);
                    }
                }
            }
            """;

        private readonly SampleBuilds builds;
        private readonly TestFolders folders;

        public SaveBuildTests(SampleBuilds builds, ITestOutputHelper output)
        {
            this.builds = builds;
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void A_save_class_gets_its_code_and_it_round_trips_in_the_built_Lua()
        {
            var project = WithSaves("SaveKey = \"the tests' key\";");

            Build(project);

            var code = File.ReadAllText(Path.Combine(project, "build", "generated", SaveCodecWriter.FileName));
            Assert.Contains("global::Moonwell.SaveGame.Register<global::Sample.RpgSave>(\"\", () => new global::Sample.RpgSave(), Write0, Read0);", code);
            Assert.Contains("global::Moonwell.SaveGame.Register<global::Sample.Settings>(\"settings\", () => new global::Sample.Settings(), Write1, Read1);", code);
            Assert.DoesNotContain("Register<global::Sample.Gear>", code);
            Assert.Contains(SaveCodecWriter.Install, File.ReadAllText(Path.Combine(project, "build", "generated", "MapMain.g.cs")));
            Assert.Contains("- 7 part", File.ReadAllText(Path.Combine(project, SaveRecord.FileName)));

            var script = File.ReadAllText(Path.Combine(project, "build", "Sample", "war3map.lua"));
            using var lua = new NLua.Lua();
            lua.State.Encoding = System.Text.Encoding.UTF8;
            lua.DoString("print = function() end");
            lua.DoString("GetPlayerName = function() return 'Local Player' end");
            lua.DoString("setmetatable(_G, { __index = function(_, name) return function() return nil end end })");
            lua.DoString(script);
            lua.DoString("main()");
            lua.DoString("Moonwell.Generated.SaveData.Register()");
            Assert.Equal("7 Åsa 1 True b 1793664000 axe 4 10 True", (string)lua.DoString("return Sample.SaveRoundTrip.Run()")[0]);
        }

        [Fact]
        public void A_map_with_saves_needs_a_key()
        {
            var project = WithSaves(string.Empty);

            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));

            Assert.True(exitCode == 1, "the build should fail: " + output);
            Assert.Contains("needs a SaveKey", error.ToString());
            Assert.Contains("Map.cs", error.ToString());
        }

        [Fact]
        public void A_map_without_saves_writes_nothing()
        {
            Assert.Equal(0, builds.Development.ExitCode);
            Assert.False(File.Exists(Path.Combine(builds.Development.BuildFolder, "generated", SaveCodecWriter.FileName)));
            Assert.DoesNotContain("SaveData", File.ReadAllText(Path.Combine(builds.Development.BuildFolder, "generated", "MapMain.g.cs")));
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(builds.Development.BuildFolder)!, SaveRecord.FileName)));
        }

        private string WithSaves(string key)
        {
            var project = folders.TemporaryCopyOf("Sample");
            File.WriteAllText(Path.Combine(project, "Saves.cs"), Saves);
            var map = Path.Combine(project, "Map.cs");
            File.WriteAllText(map, File.ReadAllText(map).Replace("Name = \"Sample map\";", "Name = \"Sample map\";\n            " + key));
            return project;
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
