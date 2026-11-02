namespace Moonwell.Build.Tests
{
    public sealed class MapMiscTests : IDisposable
    {
        private static readonly HeroBonus Standard = new HeroBonus(25f, 15f);

        private readonly TestFolders folders;

        public MapMiscTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        [Fact]
        public void a_maps_misc_file_overrides_the_hero_bonus()
        {
            var folder = folders.TemporaryFolderOutsideRepository();
            File.WriteAllText(Path.Combine(folder, "war3mapMisc.txt"), "[Misc]\r\nStrHitPointBonus=30\r\n");

            var bonus = MapMisc.Read(folder, Standard);

            Assert.Equal(new HeroBonus(30f, 15f), bonus);
        }

        [Fact]
        public void both_keys_are_read_as_decimals_in_any_case_with_other_sections_and_keys_ignored()
        {
            var folder = folders.TemporaryFolderOutsideRepository();
            File.WriteAllText(
                Path.Combine(folder, "war3mapMisc.txt"),
                "[Other]\nStrHitPointBonus=99\n\n[Misc]\n// a comment\nOtherKey=7\nintmanabonus = 12.5\nStrHitPointBonus=22.5\n");

            var bonus = MapMisc.Read(folder, Standard);

            Assert.Equal(new HeroBonus(22.5f, 12.5f), bonus);
        }

        [Fact]
        public void no_file_gives_the_standard()
        {
            var folder = folders.TemporaryFolderOutsideRepository();

            Assert.Equal(Standard, MapMisc.Read(folder, Standard));
        }

        [Fact]
        public void a_value_that_is_no_number_fails_naming_the_file_and_key()
        {
            var folder = folders.TemporaryFolderOutsideRepository();
            File.WriteAllText(Path.Combine(folder, "war3mapMisc.txt"), "[Misc]\nIntManaBonus=lots\n");

            var error = Assert.Throws<BuildException>(() => MapMisc.Read(folder, Standard));

            Assert.Equal($"{Path.Combine(folder, "war3mapMisc.txt")}: IntManaBonus is 'lots', not a number", error.Message);
        }

        [Fact]
        public void a_maps_misc_file_sets_the_experience_constants_it_names_and_keeps_the_others()
        {
            var folder = folders.TemporaryFolderOutsideRepository();
            File.WriteAllText(Path.Combine(folder, "war3mapMisc.txt"), "[Misc]\r\nMaxHeroLevel=15\r\nNeedHeroXP=100, 300\r\nneedheroxpformulaa=1.5\r\n");

            var experience = MapMisc.ReadExperience(folder, HeroExperience.Standard);

            Assert.Equal(15, experience.MaxLevel);
            Assert.Equal(new[] { 100f, 300f }, experience.Listed);
            Assert.Equal(1.5f, experience.PreviousFactor);
            Assert.Equal(100f, experience.LevelFactor);
            Assert.Equal(0f, experience.Constant);
        }

        [Fact]
        public void no_file_gives_the_standard_experience()
        {
            var folder = folders.TemporaryFolderOutsideRepository();

            Assert.Same(HeroExperience.Standard, MapMisc.ReadExperience(folder, HeroExperience.Standard));
        }

        [Fact]
        public void a_top_level_that_is_no_whole_number_or_a_list_entry_that_is_no_number_fails_naming_the_file_and_key()
        {
            var folder = folders.TemporaryFolderOutsideRepository();
            var path = Path.Combine(folder, "war3mapMisc.txt");
            File.WriteAllText(path, "[Misc]\nMaxHeroLevel=12.5\n");

            var level = Assert.Throws<BuildException>(() => MapMisc.ReadExperience(folder, HeroExperience.Standard));

            Assert.Equal($"{path}: MaxHeroLevel is '12.5', not a whole number", level.Message);
            File.WriteAllText(path, "[Misc]\nNeedHeroXP=100,lots\n");
            var list = Assert.Throws<BuildException>(() => MapMisc.ReadExperience(folder, HeroExperience.Standard));
            Assert.Equal($"{path}: NeedHeroXP is '100,lots', not a list of numbers", list.Message);
        }

        public void Dispose()
        {
            folders.Dispose();
        }
    }
}
