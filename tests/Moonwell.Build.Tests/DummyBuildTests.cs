using System.Text;
using Moonwell.Tool;
using War3Net.Build.Extensions;

namespace Moonwell.Build.Tests
{
    /// <summary>A map whose code casts with a dummy gets the dummy type and each spell's order (B13); one that doesn't gets neither.</summary>
    [Collection("Sample")]
    public sealed class DummyBuildTests : IDisposable
    {
        private readonly SampleBuilds builds;
        private readonly TestFolders folders;

        public DummyBuildTests(SampleBuilds builds, ITestOutputHelper output)
        {
            this.builds = builds;
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void map_that_casts_with_a_dummy_gets_the_dummy_type()
        {
            var project = folders.TemporaryCopyOfSample();
            File.WriteAllText(
                Path.Combine(project, "Thunder.cs"),
                "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class Thunder : GameComponent\n    {\n        public void Strike(Unit from, Unit to)\n        {\n            from.CastOn<Moonwell.Objects.Human.Abilities.StormBolt>(to, Game.Instance);\n        }\n    }\n}\n");

            Build(project);

            var registry = File.ReadAllText(Path.Combine(project, "build", "generated", "ObjectRegistry.g.cs"));
            Assert.Contains("global::Moonwell.ObjectTypes.DummyType(global::Moonwell.ObjectTypes.FourCC(\"hMWd\"));", registry);
            Assert.Contains("global::Moonwell.ObjectTypes.CastOrder(global::Moonwell.ObjectTypes.FourCC(\"AHtb\"), \"thunderbolt\", false);", registry);
            Assert.Contains(IdOf(DummyType.Id), NewUnitIds(Path.Combine(project, "build")));
        }

        [Fact]
        public void map_that_does_not_cast_gets_no_dummy()
        {
            Assert.Equal(0, builds.Development.ExitCode);
            Assert.DoesNotContain("DummyType(", File.ReadAllText(Path.Combine(builds.Development.BuildFolder, "generated", "ObjectRegistry.g.cs")));
            Assert.DoesNotContain(IdOf(DummyType.Id), NewUnitIds(builds.Development.BuildFolder));
        }

        // The ids of the new units in the staged map's unit file, in a build folder; none when it has no such file.
        private static List<int> NewUnitIds(string buildFolder)
        {
            var path = Path.Combine(buildFolder, "Sample", "war3map.w3u");
            if (!File.Exists(path))
            {
                return new List<int>();
            }

            using var reader = new BinaryReader(File.OpenRead(path), Encoding.UTF8);
            return reader.ReadUnitObjectData().NewUnits.Select(one => one.NewId).ToList();
        }

        private static int IdOf(string text)
        {
            return BitConverter.ToInt32(Encoding.ASCII.GetBytes(text));
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
