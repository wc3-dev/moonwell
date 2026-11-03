using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>
    /// A map's dotnet build runs in one MSBuild process: a map is one project, or a few that build in turn, and a worker process
    /// MSBuild starts costs more than the build it helps with (a test run makes about 160 builds).
    /// </summary>
    [Collection("Sample")]
    public sealed class DotnetBuildTests : IDisposable
    {
        private readonly TestFolders folders;

        public DotnetBuildTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void a_build_uses_one_msbuild_node()
        {
            var copy = folders.TemporaryCopyOfSample();
            var csproj = Path.Combine(copy, "Sample.csproj");
            File.WriteAllText(csproj, File.ReadAllText(csproj).Replace(
                "</Project>",
                "  <Target Name=\"ShowNodeCount\" AfterTargets=\"Build\">\n    <Message Importance=\"high\" Text=\"node count: $(MSBuildNodeCount)\" />\n  </Target>\n\n</Project>"));
            var log = new StringWriter();

            DotnetBuild.Run(MapProject.Find(copy), false, log);

            var counts = log.ToString().Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith("node count: ")).ToList();
            Assert.Equal(new[] { "node count: 1" }, counts);
        }
    }
}
