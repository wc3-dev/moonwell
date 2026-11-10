using System.Text.RegularExpressions;
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
            "       moonwell objects [folder]";

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
        [InlineData("watch", "--game", "game.exe")]
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

        private static string ReadScript(string archivePath)
        {
            using var archive = MpqArchive.Open(archivePath, true);
            using var stream = archive.OpenFile("war3map.lua");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
