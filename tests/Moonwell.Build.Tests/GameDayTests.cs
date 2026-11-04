namespace Moonwell.Build.Tests
{
    public sealed class GameDayTests : IDisposable
    {
        private readonly TestFolders folders = new TestFolders();

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void the_standard_day_is_the_games_own_constants()
        {
            // units/miscdata.txt: Dawn=6, Dusk=18, DayHours=24 (read in the game's files, 11-02).
            Assert.Equal(new GameDay(6f, 18f, 24f), GameDay.Standard);
        }

        [Fact]
        public void a_maps_misc_file_sets_its_own_dawn_dusk_and_hours()
        {
            var folder = folders.TemporaryFolderOutsideRepository();
            File.WriteAllText(Path.Combine(folder, "war3mapMisc.txt"), "[Misc]\nDawn=5.5\nDayHours=30\n");

            Assert.Equal(new GameDay(5.5f, 18f, 30f), MapMisc.ReadDay(folder, GameDay.Standard));
        }

        [Fact]
        public void a_map_without_a_misc_file_keeps_the_standard_day()
        {
            var folder = folders.TemporaryFolderOutsideRepository();

            Assert.Equal(GameDay.Standard, MapMisc.ReadDay(folder, GameDay.Standard));
        }

        [Fact]
        public void a_day_past_24_hours_is_warned_of_as_the_game_reads_no_time_past_24()
        {
            // Seen 11-02 with DayHours=30: set to 26 or 29.9, the time of day reads 24.
            Assert.Null(GameDay.Standard.Warning);
            Assert.Null(new GameDay(6f, 18f, 12f).Warning);
            Assert.Equal(
                "warning: the map's gameplay constants give a day of 30 hours, but the game reads no time of day past 24: Game.TimeOfDay reads 24 for the day's last 6 hours",
                new GameDay(6f, 18f, 30f).Warning);
        }

        [Fact]
        public void a_day_of_no_hours_stops_the_build()
        {
            var folder = folders.TemporaryFolderOutsideRepository();
            File.WriteAllText(Path.Combine(folder, "war3mapMisc.txt"), "[Misc]\nDayHours=0\n");

            var error = Assert.Throws<BuildException>(() => MapMisc.ReadDay(folder, GameDay.Standard));
            Assert.Contains("DayHours", error.Message);
        }
    }
}
