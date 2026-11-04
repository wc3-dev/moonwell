using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>
    /// A map's dotnet build runs in one MSBuild process and compiles on dotnet's compiler server: a map is one project, or a few
    /// that build in turn, and a worker process MSBuild starts, or a compiler started cold, costs more than the build it helps with
    /// (a test run makes about 175 builds). The tests' builds compile on the test run's own server.
    /// </summary>
    [Collection("Sample")]
    public sealed class DotnetBuildTests : IClassFixture<DotnetBuildTests.ShownProperties>
    {
        private readonly ShownProperties shown;

        public DotnetBuildTests(ShownProperties shown)
        {
            this.shown = shown;
        }

        [Fact]
        public void a_build_uses_one_msbuild_node()
        {
            Assert.Equal("1", shown.Of("MSBuildNodeCount"));
        }

        [Fact]
        public void a_build_compiles_on_a_compiler_server()
        {
            Assert.Equal("true", shown.Of("UseSharedCompilation"));
        }

        [Fact]
        public void the_tests_builds_compile_on_the_test_runs_own_compiler_server()
        {
            Assert.StartsWith("moonwell-tests-", CompilerServer.Name);
            Assert.Equal(CompilerServer.Name, shown.Of("SharedCompilationId"));
        }

        /// <summary>A copy of the Sample built by DotnetBuild.Run, with a target that prints the properties its build ran with.</summary>
        public sealed class ShownProperties : IDisposable
        {
            private static readonly string[] Names = { "MSBuildNodeCount", "UseSharedCompilation", "SharedCompilationId" };

            private readonly TestFolders folders = new TestFolders();

            private readonly Dictionary<string, string> values = new Dictionary<string, string>();

            public ShownProperties()
            {
                var copy = folders.TemporaryCopyOfSample();
                var csproj = Path.Combine(copy, "Sample.csproj");
                var messages = string.Concat(Names.Select(name => $"    <Message Importance=\"high\" Text=\"property {name}: $({name})\" />\n"));
                File.WriteAllText(csproj, File.ReadAllText(csproj).Replace(
                    "</Project>",
                    "  <Target Name=\"ShowProperties\" AfterTargets=\"Build\">\n" + messages + "  </Target>\n\n</Project>"));
                var log = new StringWriter();

                DotnetBuild.Run(MapProject.Find(copy), false, log);

                foreach (var line in log.ToString().Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith("property ")))
                {
                    var colon = line.IndexOf(':');
                    values[line.Substring("property ".Length, colon - "property ".Length)] = line.Substring(colon + 1).Trim();
                }
            }

            /// <summary>What the build printed for a property; the test fails, naming it, if it printed nothing.</summary>
            public string Of(string name)
            {
                Assert.True(values.ContainsKey(name), $"the build printed no line for {name}");
                return values[name];
            }

            public void Dispose()
            {
                folders.Dispose();
            }
        }
    }
}
