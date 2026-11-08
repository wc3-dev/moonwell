using Moonwell.Tool;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A map whose code reads a unit's summons listens for the game's summons from the start; one that doesn't is not made to (decision 227).</summary>
    [Collection("Sample")]
    public sealed class SummonsBuildTests : IDisposable
    {
        private readonly SampleBuilds builds;
        private readonly TestFolders folders;

        public SummonsBuildTests(SampleBuilds builds, ITestOutputHelper output)
        {
            this.builds = builds;
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void map_reading_summons_listens()
        {
            var project = folders.TemporaryCopyOfSample();
            File.WriteAllText(
                Path.Combine(project, "Pack.cs"),
                "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class Pack : GameComponent\n    {\n        public int Count(Unit unit)\n        {\n            return unit.Summons.Count;\n        }\n    }\n}\n");

            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));
            Assert.True(exitCode == 0, error.ToString() + output);

            Assert.Contains("global::Moonwell.Runtime.ListenForSummons();", File.ReadAllText(Path.Combine(project, "build", "generated", "MapMain.g.cs")));
        }

        [Fact]
        public void map_without_summons_does_not_listen()
        {
            Assert.Equal(0, builds.Development.ExitCode);
            Assert.DoesNotContain("ListenForSummons", File.ReadAllText(Path.Combine(builds.Development.BuildFolder, "generated", "MapMain.g.cs")));
        }
    }
}
