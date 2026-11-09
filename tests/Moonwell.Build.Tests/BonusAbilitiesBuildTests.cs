using System.Text;
using Moonwell.MapFiles;
using Moonwell.Tool;
using War3Net.Build.Extensions;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A map whose code reads Stat.Damage, Stat.Armor or Stat.AttackSpeed gets Moonwell's three bonus abilities, which give nothing until the runtime sets them (decision 221).</summary>
    [Collection("Sample")]
    public sealed class BonusAbilitiesBuildTests : IDisposable
    {
        private const string UsesArmor = "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class Settings : GameComponent\n    {\n        public override void OnGameStart()\n        {\n            Game.Print(Stat.Armor.Name);\n        }\n    }\n}\n";

        private readonly SampleBuilds builds;
        private readonly TestFolders folders;

        public BonusAbilitiesBuildTests(SampleBuilds builds, ITestOutputHelper output)
        {
            this.builds = builds;
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void map_reading_a_bonus_stat_gets_the_three_abilities_giving_nothing()
        {
            var project = folders.TemporaryCopyOf("Sample");
            File.WriteAllText(Path.Combine(project, "Settings.cs"), UsesArmor);

            Build(project);

            foreach (var (id, root, field) in new[] { ("AMWd", "AItg", "Iatt"), ("AMWa", "AId1", "Idef"), ("AMWs", "AIsx", "Isx1") })
            {
                var bonus = AbilityIn(Path.Combine(project, "build"), id);
                Assert.NotNull(bonus);
                Assert.Equal(Rawcodes.IdOf(root), bonus!.OldId);
                var value = Assert.Single(bonus.Modifications, one => one.Id == Rawcodes.IdOf(field)).Value;
                Assert.Equal(0.0, Convert.ToDouble(value));
            }
        }

        [Fact]
        public void map_with_only_its_own_stats_gets_none()
        {
            var project = folders.TemporaryCopyOf("Sample");
            File.WriteAllText(Path.Combine(project, "Settings.cs"), UsesArmor.Replace("Stat.Armor.Name", "new Stat(\"Crit\").Name"));

            Build(project);

            Assert.Null(AbilityIn(Path.Combine(project, "build"), "AMWa"));
            Assert.Null(AbilityIn(builds.Development.BuildFolder, "AMWa"));
        }

        [Fact]
        public void map_reading_a_bonus_stat_without_objects_stops()
        {
            var project = folders.TemporaryCopyOf("Sample");
            File.Delete(Path.Combine(project, "Objects.cs"));
            var csproj = Path.Combine(project, "Sample.csproj");
            File.WriteAllText(csproj, string.Join("\n", File.ReadAllLines(csproj).Where(line => !line.Contains("Moonwell.Objects"))));
            File.WriteAllText(Path.Combine(project, "Settings.cs"), UsesArmor);

            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));

            Assert.True(exitCode == 1, "the build should fail: " + output);
            Assert.Contains("Stat.Damage, Stat.Armor and Stat.AttackSpeed work through abilities Moonwell declares", error.ToString());
        }

        private static War3Net.Build.Object.LevelObjectModification? AbilityIn(string buildFolder, string id)
        {
            var path = Path.Combine(buildFolder, "Sample", "war3map.w3a");
            if (!File.Exists(path))
            {
                return null;
            }

            using var reader = new BinaryReader(File.OpenRead(path), Encoding.UTF8);
            return reader.ReadAbilityObjectData().NewAbilities.SingleOrDefault(one => one.NewId == Rawcodes.IdOf(id));
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
