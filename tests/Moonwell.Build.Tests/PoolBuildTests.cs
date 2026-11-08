using Moonwell.Tool;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A [Pooled] unit class is marked pooled in the registry; [Pooled] on a hero fails the build (decision 220).</summary>
    [Collection("Sample")]
    public sealed class PoolBuildTests : IDisposable
    {
        private readonly TestFolders folders;

        public PoolBuildTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void pooled_unit_class_is_marked()
        {
            var project = folders.TemporaryCopyOfSample();
            File.WriteAllText(
                Path.Combine(project, "Wave.cs"),
                "namespace Sample\n{\n    [Moonwell.Objects.ObjectId(\"h0PL\")]\n    [Moonwell.Pooled]\n    public sealed class WaveGrunt : Moonwell.Objects.Orc.Units.Grunt\n    {\n    }\n}\n");

            var (exitCode, text) = Build(project);

            Assert.True(exitCode == 0, text);
            var registry = File.ReadAllText(Path.Combine(project, "build", "generated", "ObjectRegistry.g.cs"));
            Assert.Contains("global::Moonwell.ObjectTypes.Pooled(global::Moonwell.ObjectTypes.FourCC(\"h0PL\"));", registry);
        }

        [Fact]
        public void pooled_hero_fails_the_build()
        {
            var project = folders.TemporaryCopyOfSample();
            File.WriteAllText(
                Path.Combine(project, "Wave.cs"),
                "namespace Sample\n{\n    [Moonwell.Objects.ObjectId(\"H0PL\")]\n    [Moonwell.Pooled]\n    public sealed class WaveHero : Moonwell.Objects.Human.Units.Paladin\n    {\n    }\n}\n");

            var (exitCode, text) = Build(project);

            Assert.NotEqual(0, exitCode);
            Assert.Contains("[Pooled], which is for unit classes", text);
        }

        private static (int ExitCode, string Text) Build(string project)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));
            return (exitCode, error.ToString() + output);
        }
    }
}
