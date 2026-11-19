using System.Text;
using Moonwell.Tool;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A map with build code built through the command: its results are in the game, and none of the build code is (decisions 371 to 374).</summary>
    [Collection("Sample")]
    public sealed class BuildTimeBuildTests : IDisposable
    {
        private readonly FixtureBuilds fixtures;
        private readonly TestFolders folders;

        public BuildTimeBuildTests(FixtureBuilds fixtures, ITestOutputHelper output)
        {
            this.fixtures = fixtures;
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void the_game_reads_what_the_build_code_gave_and_holds_none_of_the_build_code()
        {
            var built = fixtures.Of("BuildTime");

            // The step is in the build's checklist, before the compile.
            Assert.True(built.Output.IndexOf("Run build code", StringComparison.Ordinal) is var ran && ran >= 0 && ran < built.Output.IndexOf("Compile C#", StringComparison.Ordinal), built.Output);

            // The game code's reads of Data.Districts and Data.Trees compiled, with the numbers the build code worked out.
            var script = File.ReadAllText(Path.Combine(built.Folder, "build", "BuildTime", "war3map.lua"));
            Assert.Contains("BuildableCells = 256", script);
            Assert.Contains("FootprintColumns = 32", script);
            Assert.Contains("The shop has room for ", script);

            // Build code is in neither the Lua, the files CSharp.lua compiled, nor the game's assembly, and its libraries are not beside it.
            foreach (var name in new[] { "CreepPath", "BuildStep", "LargestRectangle", "FloodFrom", "PathingMap" })
            {
                Assert.DoesNotContain(name, script);
            }

            Assert.False(Directory.Exists(Path.Combine(built.Folder, "build", "marked", "buildtime")));
            var assemblies = Directory.GetFiles(Path.Combine(built.Folder, "bin"), "*.dll", SearchOption.AllDirectories);
            var game = Assert.Single(assemblies, path => Path.GetFileName(path) == "BuildTime.dll");
            Assert.DoesNotContain("CreepPath", Encoding.ASCII.GetString(File.ReadAllBytes(game)));
            Assert.Contains("Lumberjack", Encoding.ASCII.GetString(File.ReadAllBytes(game)));
            Assert.DoesNotContain(assemblies, path => Path.GetFileName(path).StartsWith("War3Net", StringComparison.Ordinal) || Path.GetFileName(path) == "Moonwell.BuildTime.dll");

            // The reports are beside the built map, not in it.
            Assert.True(File.Exists(Path.Combine(built.Folder, "build", "reports", "districts.png")));
            Assert.Empty(Directory.GetFiles(Path.Combine(built.Folder, "build", "BuildTime"), "*.png", SearchOption.AllDirectories));
        }

        [Fact]
        public void game_code_that_names_build_code_does_not_compile()
        {
            var folder = folders.TemporaryCopyOf("BuildTime");
            File.WriteAllText(Path.Combine(folder, "Crossing.cs"), "namespace BuildTimeFixture\n{\n    public static class Crossing\n    {\n        public static object Step() => new BuildTimeFixture.BuildTime.Districts();\n    }\n}\n");
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = Cli.Run(new[] { "build", folder }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));

            Assert.Equal(1, exitCode);
            // The folder is not in the game's compile, so its namespace is not there.
            Assert.Contains("Crossing.cs(5,", error.ToString() + output);
            Assert.Contains("CS0234", error.ToString() + output);
        }
    }
}
