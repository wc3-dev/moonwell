using System.Text.RegularExpressions;
using Moonwell.GameData;
using Moonwell.MapFiles;
using Moonwell.Tool;
using War3Net.IO.Mpq;
using Xunit;

namespace Moonwell.Build.Tests
{
    [Collection("Sample")]
    public sealed class CliTests : IDisposable
    {
        private const string Usage =
            "usage: moonwell build [folder] [--release]\n" +
            "       moonwell run [folder] [--release] [--game <exe>]\n" +
            "       moonwell watch [folder] [--release] [--run] [--game <exe>]\n" +
            "       moonwell new <name>\n" +
            "       moonwell import <map> <name>\n" +
            "       moonwell clean [folder]\n" +
            "       moonwell objects [folder] [--to-map]\n" +
            "       moonwell game find <path> [--mode sd|hd|de] [--game <folder>]\n" +
            "       moonwell game extract <path> [--out <path>] [--mode sd|hd|de] [--game <folder>]";

        private readonly SampleBuilds builds;
        private readonly TestFolders folders;
        private readonly List<string> temporaryFiles = new List<string>();

        public CliTests(SampleBuilds builds, ITestOutputHelper output)
        {
            this.builds = builds;
            folders = new TestFolders(output);
        }

        [Fact]
        public void build_writes_archive()
        {
            Assert.Equal(0, builds.Development.ExitCode);
            Assert.Equal(0, builds.Release.ExitCode);
            Assert.EndsWith(@"moonwell: built build\Sample.w3x", builds.Development.Output.TrimEnd());
            Assert.EndsWith(@"moonwell: built build\Sample.w3x", builds.Release.Output.TrimEnd());
            Assert.StartsWith("moonwell: Sample (development build)", builds.Development.Output);
            Assert.StartsWith("moonwell: Sample (release build)", builds.Release.Output);
            Assert.True(File.Exists(builds.Development.Archive));
            Assert.True(File.Exists(builds.Release.Archive));

            var development = ReadScript(builds.Development.Archive);
            var release = ReadScript(builds.Release.Archive);
            Assert.Contains("sample: started (development)", development);
            Assert.DoesNotContain("sample: started (release)", development);
            Assert.Contains("sample: started (release)", release);
            Assert.DoesNotContain("sample: started (development)", release);
        }

        [Fact]
        public void build_error_exits_1()
        {
            var (exitCode, _, error, _) = Run("build", TestFolders.Fixture("NoMap"));

            Assert.Equal(1, exitCode);
            Assert.Equal(@"moonwell: no World Editor map: map\war3map.w3i is missing", error.Trim());
        }

        [Fact]
        public void unexpected_error_exits_1()
        {
            var game = Path.Combine(Path.GetTempPath(), "moonwell-game-" + Guid.NewGuid().ToString("N") + ".exe");
            File.WriteAllText(game, string.Empty);
            temporaryFiles.Add(game);
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = Cli.Run(
                new[] { "run", TestFolders.Fixture("Sample"), "--game", game },
                output,
                error,
                (_, _) => throw new InvalidOperationException("starter failed"));

            Assert.Equal(1, exitCode);
            Assert.StartsWith("moonwell: unexpected error: InvalidOperationException: starter failed", error.ToString());
            Assert.Contains(" at ", error.ToString());
        }

        [Fact]
        public void locked_archive_is_reported()
        {
            var folder = folders.TemporaryCopyOfSample();
            var archive = Path.Combine(folder, "build", "Sample.w3x");
            Directory.CreateDirectory(Path.GetDirectoryName(archive)!);

            int exitCode;
            string error;
            using (new FileStream(archive, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                (exitCode, _, error, _) = Run("build", folder);
            }

            Assert.Equal(1, exitCode);
            Assert.StartsWith(@"moonwell: cannot write build\Sample.w3x", error);
            Assert.Contains("(is the game or World Editor using it?)", error);
        }

        [Theory]
        [InlineData]
        [InlineData("deploy")]
        [InlineData("build", "--fast")]
        [InlineData("build", "one", "two")]
        [InlineData("build", "--game", "game.exe")]
        [InlineData("run", "--game")]
        [InlineData("new")]
        [InlineData("new", "One", "Two")]
        [InlineData("new", "One", "--release")]
        [InlineData("import")]
        [InlineData("import", "old.w3x")]
        [InlineData("import", "old.w3x", "One", "Two")]
        [InlineData("clean", "--release")]
        [InlineData("objects", "--run")]
        [InlineData("build", "--to-map")]
        [InlineData("watch", "--game", "game.exe")]
        [InlineData("game")]
        [InlineData("game", "list", "x")]
        [InlineData("game", "find")]
        [InlineData("game", "find", "one", "two")]
        [InlineData("game", "find", "x", "--mode", "ultra")]
        [InlineData("game", "find", "x", "--out", "here")]
        [InlineData("game", "extract", "x", "--out")]
        public void bad_arguments_exit_2(params string[] args)
        {
            var (exitCode, output, error, started) = Run(args);

            Assert.Equal(2, exitCode);
            Assert.Equal(string.Empty, output);
            Assert.Equal(Usage, error.Trim());
            Assert.Empty(started);
        }

        [Fact]
        public void version_prints_the_tools_version()
        {
            var props = File.ReadAllText(Path.Combine(TestFolders.Fixture("."), "..", "..", "..", "Directory.Build.props"));
            var version = Regex.Match(props, "<Version>([^<]+)</Version>").Groups[1].Value;

            var (exitCode, output, error, started) = Run("--version");

            Assert.Equal(0, exitCode);
            Assert.Equal(version, output.Trim());
            Assert.Equal(string.Empty, error);
            Assert.Empty(started);
        }

        [Fact]
        public void run_starts_the_game()
        {
            var game = Path.Combine(Path.GetTempPath(), "moonwell-game-" + Guid.NewGuid().ToString("N") + ".exe");
            File.WriteAllText(game, string.Empty);
            temporaryFiles.Add(game);
            var sample = TestFolders.Fixture("Sample");

            var (exitCode, _, _, started) = Run("run", sample, "--game", game);

            Assert.Equal(0, exitCode);
            var call = Assert.Single(started);
            Assert.Equal(game, call.Exe);
            var archive = Path.Combine(sample, "build", "Sample.w3x");
            Assert.Equal($"-launch -windowmode windowed -loadfile \"{archive}\"", call.Arguments);
        }

        [Fact]
        public void run_without_game_exits_1()
        {
            var game = Path.Combine(Path.GetTempPath(), "moonwell-missing-" + Guid.NewGuid().ToString("N") + ".exe");

            var (exitCode, output, error, started) = Run("run", TestFolders.Fixture("Sample"), "--game", game);

            Assert.Equal(1, exitCode);
            Assert.Equal($"moonwell: the game is not at {game}", error.Trim());
            Assert.Empty(started);
            Assert.Equal(string.Empty, output);
        }

        [Fact]
        public void new_writes_the_template_under_the_maps_name()
        {
            var parent = folders.TemporaryFolderOutsideRepository();
            var template = Path.Combine(TestFolders.Fixture("."), "..", "..", "..", "template", "MoonwellMap");

            var folder = NewMap.Write("MyMap", parent, "1.2.3");

            Assert.Equal(Path.Combine(parent, "MyMap"), folder);
            var project = File.ReadAllText(Path.Combine(folder, "MyMap.csproj"));
            Assert.Contains("Include=\"Moonwell.Objects\" Version=\"1.2.3\"", project);
            Assert.DoesNotContain("MOONWELL_VERSION", project);
            Assert.Contains("namespace MyMap.Heroes", File.ReadAllText(Path.Combine(folder, "Heroes", "Champion.cs")));
            Assert.DoesNotContain("MoonwellMap", File.ReadAllText(Path.Combine(folder, "Map.cs")));
            Assert.Equal(File.ReadAllBytes(Path.Combine(template, ".gitignore")), File.ReadAllBytes(Path.Combine(folder, ".gitignore")));
            Assert.Equal(File.ReadAllBytes(Path.Combine(template, "map", "war3map.w3e")), File.ReadAllBytes(Path.Combine(folder, "map", "war3map.w3e")));
            Assert.False(Directory.Exists(Path.Combine(folder, ".template.config")));
            Assert.Equal(Directory.GetFiles(Path.Combine(template, "map")).Length, Directory.GetFiles(Path.Combine(folder, "map")).Length);
        }

        [Theory]
        [InlineData("My-Map")]
        [InlineData("2Maps")]
        public void new_refuses_a_name_that_is_no_namespace(string name)
        {
            var error = Assert.Throws<BuildException>(() => NewMap.Write(name, folders.TemporaryFolderOutsideRepository(), "1.2.3"));

            Assert.Equal($"\"{name}\" cannot be a map's name: it is the code's namespace too, so use letters, digits and _ only, not starting with a digit", error.Message);
        }

        [Fact]
        public void new_refuses_a_folder_with_files()
        {
            var parent = folders.TemporaryFolderOutsideRepository();
            Directory.CreateDirectory(Path.Combine(parent, "MyMap"));
            File.WriteAllText(Path.Combine(parent, "MyMap", "notes.txt"), "mine");

            var error = Assert.Throws<BuildException>(() => NewMap.Write("MyMap", parent, "1.2.3"));

            Assert.Equal($"{Path.Combine(parent, "MyMap")} is not empty; pick another name or folder", error.Message);
            Assert.Equal("mine", File.ReadAllText(Path.Combine(parent, "MyMap", "notes.txt")));
        }

        [Fact]
        public void clean_deletes_the_build_folder()
        {
            var folder = folders.TemporaryCopyOfSample();
            Directory.CreateDirectory(Path.Combine(folder, "build", "Sample"));
            File.WriteAllText(Path.Combine(folder, "build", "Sample.w3x"), "x");

            var (exitCode, output, error, _) = Run("clean", folder);

            Assert.Equal(0, exitCode);
            Assert.Equal(@"moonwell: deleted build\", output.Trim());
            Assert.Equal(string.Empty, error);
            Assert.False(Directory.Exists(Path.Combine(folder, "build")));
            Assert.True(Directory.Exists(Path.Combine(folder, "map")));
            Assert.Equal(@"moonwell: no build\ to delete", Run("clean", folder).Output.Trim());
        }

        [Fact]
        public void objects_lists_the_maps_classes()
        {
            var (exitCode, output, error, _) = Run("objects", folders.TemporaryCopyOfSample());

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, error);
            Assert.Equal("H001  hero  Sample.Arthas  based on Hpal", output.Trim());
        }

        [Fact]
        public void objects_to_map_writes_the_object_files_the_assets_and_their_list_and_the_next_build_lets_code_win()
        {
            var folder = folders.TemporaryCopyOfSample();
            var map = Path.Combine(folder, "map");
            var staged = Path.Combine(folder, "build", "Sample");
            var before = Directory.GetFiles(map, "*", SearchOption.AllDirectories).ToDictionary(file => file, File.ReadAllBytes);

            var (exitCode, output, error, started) = Run("objects", folder, "--to-map");

            Assert.True(exitCode == 0, error + output);
            Assert.Empty(started);
            var added = Directory.GetFiles(map, "*", SearchOption.AllDirectories).Where(file => !before.ContainsKey(file)).Select(file => Path.GetRelativePath(map, file)).Order(StringComparer.Ordinal).ToList();
            Assert.Equal(new[] { @"Moonwell\hello.txt", "war3map.imp", "war3map.w3u", "war3mapSkin.w3u" }, added);
            Assert.EndsWith(@"moonwell: wrote war3map.w3u, war3mapSkin.w3u, Moonwell\hello.txt, war3map.imp into map\", output.TrimEnd());
            Assert.Equal(new[] { @"Moonwell\hello.txt" }, ImportList.Paths(Path.Combine(map, "war3map.imp")));
            foreach (var (file, bytes) in before)
            {
                Assert.Equal(bytes, File.ReadAllBytes(file));
            }

            var built = added.ToDictionary(name => name!, name => File.ReadAllBytes(Path.Combine(staged, name!)));
            foreach (var (name, bytes) in built)
            {
                Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(map, name)));
            }

            // Run again, the build takes the files back and the command finds nothing to write.
            var again = Run("objects", folder, "--to-map");
            Assert.True(again.ExitCode == 0, again.Error + again.Output);
            Assert.EndsWith(@"moonwell: map\ already holds the built objects and assets", again.Output.TrimEnd());
            Assert.DoesNotContain("replaces the map's file", again.Output);

            // A change made to the class's object in World Editor: the next build writes the class's own value, and leaves the source map be.
            var skinPath = Path.Combine(map, "war3mapSkin.w3u");
            var skin = ObjectFile.ReadLenient(Kind.Unit, File.ReadAllBytes(skinPath));
            Assert.Equal("Arthas", ((FieldValue.Text)skin.Custom.Single(entry => entry.Id == "H001").Find("unam", 0)!.Value).Value);
            skin.Custom.Single(entry => entry.Id == "H001").Set("unam", 0, 0, new FieldValue.Text("Edited"));
            var edited = skin.ToBytes();
            File.WriteAllBytes(skinPath, edited);

            var next = Run("build", folder);

            Assert.True(next.ExitCode == 0, next.Error + next.Output);
            foreach (var (name, bytes) in built)
            {
                Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(staged, name)));
            }

            Assert.Equal(edited, File.ReadAllBytes(skinPath));
        }

        [Fact]
        public void objects_to_map_leaves_out_moonwells_own_helper_objects()
        {
            var folder = folders.TemporaryCopyOfSample();
            File.WriteAllText(
                Path.Combine(folder, "Thunder.cs"),
                "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class Thunder : GameComponent\n    {\n        public void Strike(Unit from, Unit to)\n        {\n            from.CastOn<Moonwell.Objects.Human.Abilities.StormBolt>(to, Game.Instance);\n        }\n    }\n}\n");

            var (exitCode, output, error, _) = Run("objects", folder, "--to-map");

            Assert.True(exitCode == 0, error + output);
            Assert.Equal(new[] { "H001", DummyType.Id }, NewUnitIds(Path.Combine(folder, "build", "Sample", "war3map.w3u")));
            Assert.Equal(new[] { "H001" }, NewUnitIds(Path.Combine(folder, "map", "war3map.w3u")));
            Assert.Equal(new[] { "H001" }, NewUnitIds(Path.Combine(folder, "map", "war3mapSkin.w3u")));

            // The build refuses a source map that defines the dummy's id, so the next one passes only without it.
            var next = Run("build", folder);
            Assert.True(next.ExitCode == 0, next.Error + next.Output);
        }

        [Fact]
        public async Task watch_builds_again_after_a_change_and_starts_the_game_again()
        {
            var folder = folders.TemporaryCopyOfSample();
            var game = Path.Combine(Path.GetTempPath(), "moonwell-game-" + Guid.NewGuid().ToString("N") + ".exe");
            File.WriteAllText(game, string.Empty);
            temporaryFiles.Add(game);
            using var started = new SemaphoreSlim(0);
            var stopped = 0;
            using var cancel = new CancellationTokenSource();
            var error = TextWriter.Synchronized(new StringWriter());

            var watching = Task.Run(() => Cli.Run(
                new[] { "watch", folder, "--run", "--game", game },
                TextWriter.Synchronized(new StringWriter()),
                error,
                (_, _) => started.Release(),
                () => Interlocked.Increment(ref stopped),
                cancel.Token));

            Assert.True(await started.WaitAsync(TimeSpan.FromMinutes(5)), "the first build started no game: " + error);
            Assert.Equal(0, Volatile.Read(ref stopped));
            File.AppendAllText(Path.Combine(folder, "Hello.cs"), "\n// changed\n");
            Assert.True(await started.WaitAsync(TimeSpan.FromMinutes(5)), "the change built nothing: " + error);
            Assert.Equal(1, Volatile.Read(ref stopped));

            // The build writes only where the watch does not look, so nothing more is built while nothing changes.
            Assert.False(await started.WaitAsync(TimeSpan.FromSeconds(5)));
            await cancel.CancelAsync();
            Assert.Equal(0, await watching);
        }

        [InstallFact]
        public void game_find_names_the_storage_path_and_lists_a_folder()
        {
            var (exitCode, output, _, _) = Run("game", "find", @"ReplaceableTextures\CommandButtons\BTNPeasant.blp");
            Assert.Equal(0, exitCode);
            Assert.Equal("War3.w3mod:ReplaceableTextures/CommandButtons/BTNPeasant.dds", output.Trim(), ignoreCase: true);

            // --game takes the exe that run takes, and a mode reads its own tier first.
            var exe = Path.Combine(GameInstall.Find()!, "_retail_", "x86_64", "Warcraft III.exe");
            (exitCode, output, _, _) = Run("game", "find", "ReplaceableTextures/CommandButtons/BTNPeasant.blp", "--mode", "de", "--game", exe);
            Assert.Equal(0, exitCode);
            Assert.StartsWith("War3.w3mod:_DE.w3mod:", output, StringComparison.OrdinalIgnoreCase);

            foreach (var folder in new[] { @"Units\Human\HeroPaladin", "Units/Human/HeroPaladin/", "units/human/heropaladin/*what*.ogg" })
            {
                (exitCode, output, _, _) = Run("game", "find", folder);
                Assert.Equal(0, exitCode);
                Assert.Contains("Units/Human/HeroPaladin/HeroPaladinWhat1.ogg", output.Split('\n').Select(line => line.Trim()));
            }

            Assert.DoesNotContain("HeroPaladinDeath", output);
        }

        [InstallFact]
        public void game_find_of_a_missing_file_exits_1()
        {
            var (exitCode, output, error, _) = Run("game", "find", @"units\human\NoSuchUnit\NoSuchUnit.mdx");

            Assert.Equal(1, exitCode);
            Assert.Equal(string.Empty, output);
            Assert.Equal(@"moonwell: the game has no file units\human\NoSuchUnit\NoSuchUnit.mdx", error.Trim());
        }

        [Fact]
        public void game_find_without_an_install_exits_1()
        {
            var empty = folders.TemporaryFolderOutsideRepository();
            Directory.CreateDirectory(empty);

            var (exitCode, _, error, _) = Run("game", "find", "x.blp", "--game", empty);

            Assert.Equal(1, exitCode);
            Assert.StartsWith("moonwell: No Warcraft III install", error);
            Assert.DoesNotContain("unexpected error", error);
        }

        [InstallFact]
        public void game_extract_writes_the_file_or_a_folder_of_files()
        {
            var target = folders.TemporaryFolderOutsideRepository();
            Directory.CreateDirectory(target);

            var (exitCode, output, _, _) = Run("game", "extract", @"UI\SoundInfo\UnitAckSounds.slk", "--out", target);
            Assert.Equal(0, exitCode);
            Assert.Equal($"moonwell: wrote {Path.Combine(target, "UnitAckSounds.slk")}", output.Trim());
            Assert.StartsWith("ID;", File.ReadAllText(Path.Combine(target, "UnitAckSounds.slk")));

            var named = Path.Combine(target, "sub", "peasant.dds");
            Assert.Equal(0, Run("game", "extract", @"ReplaceableTextures\CommandButtons\BTNPeasant.blp", "--out", named).ExitCode);
            Assert.Equal("DDS ", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(named), 0, 4));

            (exitCode, output, _, _) = Run("game", "extract", "Units/Human/HeroPaladin/*What*.ogg", "--out", target);
            Assert.Equal(0, exitCode);
            Assert.Matches(@"^moonwell: wrote \d+ files into ", output.Trim());
            Assert.True(new FileInfo(Path.Combine(target, "Units", "Human", "HeroPaladin", "HeroPaladinWhat1.ogg")).Length > 0);
            Assert.False(File.Exists(Path.Combine(target, "Units", "Human", "HeroPaladin", "HeroPaladinDeath.ogg")));
        }

        public void Dispose()
        {
            folders.Dispose();
            foreach (var file in temporaryFiles)
            {
                File.Delete(file);
            }
        }

        private static (int ExitCode, string Output, string Error, List<(string Exe, string Arguments)> Started) Run(params string[] args)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var started = new List<(string Exe, string Arguments)>();
            var exitCode = Cli.Run(args, output, error, (exe, arguments) => started.Add((exe, arguments)));
            return (exitCode, output.ToString(), error.ToString(), started);
        }

        private static List<string> NewUnitIds(string path)
        {
            return ObjectFile.ReadLenient(Kind.Unit, File.ReadAllBytes(path)).Custom.Select(entry => entry.Id).Order(StringComparer.Ordinal).ToList();
        }

        private static string ReadScript(string archivePath)
        {
            using var archive = MpqArchive.Open(archivePath, true);
            using var stream = archive.OpenFile("war3map.lua");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
