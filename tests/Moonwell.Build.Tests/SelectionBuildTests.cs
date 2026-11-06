using Moonwell.Tool;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A map whose code reads a player's selection listens for selections from the start; one that doesn't is not made to (decision 203).</summary>
    [Collection("Sample")]
    public sealed class SelectionBuildTests : IDisposable
    {
        private readonly SampleBuilds builds;
        private readonly TestFolders folders;

        public SelectionBuildTests(SampleBuilds builds, ITestOutputHelper output)
        {
            this.builds = builds;
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void map_reading_selection_listens()
        {
            var project = folders.TemporaryCopyOfSample();
            File.WriteAllText(
                Path.Combine(project, "Picked.cs"),
                "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class Picked : GameComponent\n    {\n        public int Count(Player player)\n        {\n            return player.Selection.Count;\n        }\n    }\n}\n");

            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));
            Assert.True(exitCode == 0, error.ToString() + output);

            Assert.Contains("global::Moonwell.Runtime.ListenForSelection();", File.ReadAllText(Path.Combine(project, "build", "generated", "MapMain.g.cs")));
        }

        [Fact]
        public void map_without_selection_does_not_listen()
        {
            Assert.Equal(0, builds.Development.ExitCode);
            Assert.DoesNotContain("ListenForSelection", File.ReadAllText(Path.Combine(builds.Development.BuildFolder, "generated", "MapMain.g.cs")));
        }
    }
}
