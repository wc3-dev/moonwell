namespace Moonwell.Build.Tests
{
    public sealed class HeroExperienceTests
    {
        [Fact]
        public void the_standard_table_is_the_one_seen_in_the_game()
        {
            Assert.Equal(new[] { 0, 200, 500, 900, 1400, 2000, 2700, 3500, 4400, 5400 }, HeroExperience.Standard.Table());
        }

        [Fact]
        public void a_maps_own_constants_give_the_table_seen_in_the_game()
        {
            // The probe of 11-02: the listed totals, then 1.5 times the previous plus 10 a level plus 5, kept with its
            // fraction and truncated when read (797.5 reads 797, and the next is 1261).
            var experience = new HeroExperience(15, new[] { 100f, 300f }, 1.5f, 10f, 5f);

            Assert.Equal(
                new[] { 0, 100, 300, 495, 797, 1261, 1966, 3035, 4647, 7076, 10730, 16220, 24465, 36843, 55420 },
                experience.Table());
        }

        [Fact]
        public void the_list_stops_at_the_top_level_and_a_top_level_of_1_has_no_level_to_reach()
        {
            Assert.Equal(new[] { 0, 100 }, new HeroExperience(2, new[] { 100f, 300f, 600f }, 1f, 100f, 0f).Table());
            Assert.Equal(new[] { 0 }, new HeroExperience(1, new[] { 100f }, 1f, 100f, 0f).Table());
            Assert.Equal(new[] { 0 }, new HeroExperience(0, new[] { 100f }, 1f, 100f, 0f).Table());
        }

        [Fact]
        public void a_table_with_no_list_starts_the_formula_at_level_2()
        {
            Assert.Equal(new[] { 0, 205, 715 }, new HeroExperience(3, Array.Empty<float>(), 2f, 100f, 5f).Table());
        }

        [Fact]
        public void a_total_past_the_largest_whole_number_stops_there()
        {
            // 1000 doubling each level: level 23 is 1000 times 2 to the 21st, level 24 would pass int.MaxValue.
            var table = new HeroExperience(60, new[] { 1000f }, 2f, 0f, 0f).Table();

            Assert.Equal(2097152000, table[22]);
            Assert.Equal(int.MaxValue, table[23]);
            Assert.Equal(int.MaxValue, table[59]);
        }
    }
}
