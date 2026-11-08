using Moonwell.Tool;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A map whose code reads a unit's transport or cargo listens for loads and unloads from the start; one that doesn't is not made to (decision 219).</summary>
    [Collection("Sample")]
    public sealed class TransportBuildTests : IDisposable
    {
        private readonly SampleBuilds builds;
        private readonly TestFolders folders;

        public TransportBuildTests(SampleBuilds builds, ITestOutputHelper output)
        {
            this.builds = builds;
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void map_reading_transports_listens()
        {
            var project = folders.TemporaryCopyOfSample();
            File.WriteAllText(
                Path.Combine(project, "Aboard.cs"),
                "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class Aboard : GameComponent\n    {\n        public int Count(Unit unit)\n        {\n            return unit.Cargo.Count;\n        }\n    }\n}\n");

            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));
            Assert.True(exitCode == 0, error.ToString() + output);

            Assert.Contains("global::Moonwell.Runtime.ListenForTransports();", File.ReadAllText(Path.Combine(project, "build", "generated", "MapMain.g.cs")));
        }

        [Fact]
        public void map_without_transports_does_not_listen()
        {
            Assert.Equal(0, builds.Development.ExitCode);
            Assert.DoesNotContain("ListenForTransports", File.ReadAllText(Path.Combine(builds.Development.BuildFolder, "generated", "MapMain.g.cs")));
        }
    }
}
