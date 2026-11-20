using Moonwell.Tool;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A map that reads a ready-made stat whose effect is the stats package's stops without it; one that references the package has it installed after the damage package, whatever its code names of it (decision 390).</summary>
    [Collection("Sample")]
    public sealed class StatsBuildTests : IDisposable
    {
        private const string Reads = "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class Settings : GameComponent\n    {\n        public override void OnGameStart()\n        {\n            Game.Print(READ);\n        }\n    }\n}\n";

        private readonly TestFolders folders;

        public StatsBuildTests(SampleBuilds builds, ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Theory]
        [InlineData("Stat.CritChance.Name", "Stat.CritChance")]
        [InlineData("Stat.Resistance(DamageKind.Fire).Name", "Stat.Resistance(...)")]
        [InlineData("Stat.CastSpeed.Name", "Stat.CastSpeed")]
        public void map_reading_a_stat_of_the_package_without_it_stops(string read, string named)
        {
            var project = folders.TemporaryCopyOf("Sample");
            File.WriteAllText(Path.Combine(project, "Settings.cs"), Reads.Replace("READ", read));

            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));

            Assert.True(exitCode == 1, "the build should fail: " + output);
            Assert.Contains(named + " has no effect without the Moonwell.Stats package", error.ToString());
        }

        [Fact]
        public void map_reading_only_stats_of_the_core_builds()
        {
            var project = folders.TemporaryCopyOf("Sample");
            File.WriteAllText(Path.Combine(project, "Settings.cs"), Reads.Replace("READ", "new Stat(\"Rage\").Name + Stat.LootBonus.Name + Stat.CrowdControlReduction.Name"));

            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));

            Assert.True(exitCode == 0, error.ToString() + output);
        }

        [Fact]
        public void map_referencing_the_package_installs_it_after_the_damage_package()
        {
            var project = folders.TemporaryCopyOf("Sample");
            var csproj = Path.Combine(project, "Sample.csproj");
            File.WriteAllText(csproj, File.ReadAllText(csproj).Replace(
                "</ItemGroup>\n\n</Project>",
                "  <ProjectReference Include=\"..\\..\\..\\..\\src\\Moonwell.Stats\\Moonwell.Stats.csproj\" />\n  </ItemGroup>\n\n</Project>"));
            File.WriteAllText(Path.Combine(project, "Settings.cs"), Reads.Replace("READ", "Stat.CritChance.Name"));

            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));

            Assert.True(exitCode == 0, error.ToString() + output);
            var main = File.ReadAllText(Path.Combine(project, "build", "generated", "MapMain.g.cs"));
            var damage = main.IndexOf("global::Moonwell.DamageSystem.Install();", StringComparison.Ordinal);
            var stats = main.IndexOf("global::Moonwell.StatsSystem.Install();", StringComparison.Ordinal);
            Assert.True(damage >= 0 && stats > damage, "the damage package is installed, then the stats package:\n" + main);
            var script = File.ReadAllText(Path.Combine(project, "build", "Sample", "war3map.lua"));
            Assert.Contains("StatsSystem.Install()", script);
        }

        [Fact]
        public void sample_without_the_package_carries_none_of_it()
        {
            var project = folders.TemporaryCopyOf("Sample");
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));

            Assert.True(exitCode == 0, error.ToString() + output);
            Assert.DoesNotContain("StatsSystem", File.ReadAllText(Path.Combine(project, "build", "generated", "MapMain.g.cs")));
            Assert.DoesNotContain("StatEffects", File.ReadAllText(Path.Combine(project, "build", "Sample", "war3map.lua")));
        }
    }
}
