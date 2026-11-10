using System.Text;
using Moonwell.MapFiles;
using War3Net.Build.Extensions;
using War3Net.Build.Object;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>The tooltips <c>Describe</c> writes (decision 258), built through CSharp.lua with the kind bases' stubs.</summary>
    [Collection("Sample")]
    public sealed class TooltipBuildTests : IDisposable
    {
        private const string Gold = "|cffffcc00";

        private readonly TestFolders folders;

        public TooltipBuildTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void describe_writes_the_tooltips_of_an_ability_an_item_and_an_upgrade()
        {
            var copy = folders.TemporaryCopyOfSample();
            File.WriteAllText(Path.Combine(copy, "Objects.cs"), """
                using Moonwell.Objects;

                namespace Sample
                {
                    [ObjectId("A001")]
                    public sealed class Smite : Moonwell.Objects.Human.Abilities.StormBolt
                    {
                        public Smite()
                        {
                            Damage = Levels(75f, 150f, 225f);
                            Describe("Smite", tip => $"Deals {tip[Damage]} damage.", TipLines.Cooldown);
                        }
                    }

                    [ObjectId("I001")]
                    public sealed class Tonic : Moonwell.Objects.Items.PotionOfHealing
                    {
                        public Tonic()
                        {
                            Describe("Tonic", tip => $"Heals {tip.Number(250)} life.");
                        }
                    }

                    [ObjectId("R001")]
                    public sealed class Sharp : Moonwell.Objects.Human.Upgrades.IronForgedSwords
                    {
                        public Sharp()
                        {
                            Describe("Sharp Swords", tip => $"Rank {tip.Level}.");
                        }
                    }
                }
                """);

            MapBuild.Run(copy, new BuildOptions(), TextWriter.Null);
            var staged = Path.Combine(copy, "build", "Sample");

            var abilities = ReadLevels(staged, "war3mapSkin.w3a", reader => reader.ReadAbilityObjectData().NewAbilities);
            Assert.Equal(
                new[] { $"1=Smite - [{Gold}Level 1|r]", $"2=Smite - [{Gold}Level 2|r]", $"3=Smite - [{Gold}Level 3|r]" },
                LevelsOf(abilities, "A001", "atp1"));
            Assert.Equal(
                $"2=Deals {Gold}150|r damage.|n|nCooldown: {Gold}9|r seconds",
                LevelsOf(abilities, "A001", "aub1")[1]);
            Assert.Equal(new[] { $"0=Learn Smite - [{Gold}Level %d|r]" }, LevelsOf(abilities, "A001", "aret"));
            Assert.Equal(new[] { "0=Deals 75/150/225 damage.|n|nCooldown: 9 seconds" }, LevelsOf(abilities, "A001", "arut"));

            var items = ReadSimple(staged, "war3mapSkin.w3t");
            Assert.Equal(new[] { "Purchase Tonic" }, ValuesOf(items, "I001", "utip"));
            Assert.Equal(new[] { $"Heals {Gold}250|r life." }, ValuesOf(items, "I001", "utub"));

            var upgrades = ReadLevels(staged, "war3mapSkin.w3q", reader => reader.ReadUpgradeObjectData().NewUpgrades);
            Assert.Equal(new[] { "1=Upgrade to Sharp Swords", "2=Upgrade to Sharp Swords", "3=Upgrade to Sharp Swords" }, LevelsOf(upgrades, "R001", "gtp1"));
            Assert.Equal(new[] { "1=Rank 1.", "2=Rank 2.", "3=Rank 3." }, LevelsOf(upgrades, "R001", "gub1"));
        }

        // Each level's value of a field of an object, as "level=value", by level.
        private static string[] LevelsOf(IEnumerable<LevelObjectModification> objects, string id, string field)
        {
            var one = Assert.Single(objects, candidate => candidate.NewId == Rawcodes.IdOf(id));
            return one.Modifications
                .Where(modification => modification.Id == Rawcodes.IdOf(field))
                .OrderBy(modification => modification.Level)
                .Select(modification => $"{modification.Level}={modification.Value}")
                .ToArray();
        }

        private static string[] ValuesOf(IEnumerable<SimpleObjectModification> objects, string id, string field)
        {
            var one = Assert.Single(objects, candidate => candidate.NewId == Rawcodes.IdOf(id));
            return one.Modifications.Where(modification => modification.Id == Rawcodes.IdOf(field)).Select(modification => (string)modification.Value).ToArray();
        }

        private static IReadOnlyList<LevelObjectModification> ReadLevels(string staged, string file, Func<BinaryReader, IReadOnlyList<LevelObjectModification>> read)
        {
            using var reader = new BinaryReader(File.OpenRead(Path.Combine(staged, file)), Encoding.UTF8);
            return read(reader);
        }

        private static IReadOnlyList<SimpleObjectModification> ReadSimple(string staged, string file)
        {
            using var reader = new BinaryReader(File.OpenRead(Path.Combine(staged, file)), Encoding.UTF8);
            return reader.ReadItemObjectData().NewItems;
        }
    }
}
