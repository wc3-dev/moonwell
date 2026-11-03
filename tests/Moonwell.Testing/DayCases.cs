namespace Moonwell.Testing
{
    /// <summary>Cases for the game clock and the time of day.</summary>
    public static class DayCases
    {
        /// <summary>Game.Time is the seconds since the runtime started, on Moonwell's clock.</summary>
        public static void GameTime(FakeWorld world)
        {
            Expect.Near(0.0f, Game.Time, "the time at the start");
            world.Advance(2.5f);
            Expect.Near(2.5f, Game.Time, "the time 2.5 s later");
        }

        /// <summary>The time of day is the game's, read and set in hours.</summary>
        public static void TimeOfDay(FakeWorld world)
        {
            Game.TimeOfDay = 6.5f;
            Expect.Near(6.5f, world.HourOfDay(), "the game's time of day");
            Expect.Near(6.5f, Game.TimeOfDay, "the time of day read back");
        }

        /// <summary>A time outside the day's hours wraps into them, where the game would read 0.</summary>
        public static void TimeOfDayWraps(FakeWorld world)
        {
            Game.TimeOfDay = 26.0f;
            Expect.Near(2.0f, Game.TimeOfDay, "26 wraps to 2");
            Game.TimeOfDay = -1.0f;
            Expect.Near(23.0f, Game.TimeOfDay, "-1 wraps to 23");
            Game.TimeOfDay = 24.0f;
            Expect.Near(0.0f, Game.TimeOfDay, "24 wraps to 0");
            Game.TimeOfDay = 49.5f;
            Expect.Near(1.5f, Game.TimeOfDay, "49.5 wraps to 1.5");
        }

        /// <summary>It is day from dawn (6:00) up to dusk (18:00).</summary>
        public static void IsDay(FakeWorld world)
        {
            Game.TimeOfDay = 5.99f;
            Expect.Equal(false, Game.IsDay, "5.99 is night");
            Game.TimeOfDay = 6.0f;
            Expect.Equal(true, Game.IsDay, "dawn is day");
            Game.TimeOfDay = 17.99f;
            Expect.Equal(true, Game.IsDay, "17.99 is day");
            Game.TimeOfDay = 18.0f;
            Expect.Equal(false, Game.IsDay, "dusk is night");
        }

        /// <summary>A map's own dawn, dusk and hours, which the build installs, set when it is day and where a time wraps.</summary>
        public static void MapsOwnDay(FakeWorld world)
        {
            Runtime.InstallDay(5.0f, 20.0f, 30.0f);
            Game.TimeOfDay = 19.5f;
            Expect.Equal(true, Game.IsDay, "19.5 is day before a dusk of 20");
            Game.TimeOfDay = 5.0f;
            Expect.Equal(true, Game.IsDay, "a dawn of 5 is day");
            Game.TimeOfDay = 26.0f;
            Expect.Near(26.0f, Game.TimeOfDay, "26 is in a day of 30 hours");
            Game.TimeOfDay = 31.0f;
            Expect.Near(1.0f, Game.TimeOfDay, "31 wraps to 1 in a day of 30 hours");
        }

        /// <summary>A dawn later than dusk makes the day run over midnight.</summary>
        public static void DayOverMidnight(FakeWorld world)
        {
            Runtime.InstallDay(20.0f, 4.0f, 24.0f);
            Game.TimeOfDay = 22.0f;
            Expect.Equal(true, Game.IsDay, "22 is day from a dawn of 20");
            Game.TimeOfDay = 2.0f;
            Expect.Equal(true, Game.IsDay, "2 is day up to a dusk of 4");
            Game.TimeOfDay = 12.0f;
            Expect.Equal(false, Game.IsDay, "12 is night");
        }

        /// <summary>The day's speed is the game's: 1 is its own, 2 twice as fast.</summary>
        public static void DaySpeed(FakeWorld world)
        {
            Expect.Near(1.0f, Game.DaySpeed, "the speed at the start");
            Game.DaySpeed = 2.0f;
            Expect.Near(2.0f, world.DayScale(), "the game's speed");
            Expect.Near(2.0f, Game.DaySpeed, "the speed read back");
        }

        /// <summary>A stopped day keeps its speed, and goes on at it.</summary>
        public static void DayStopped(FakeWorld world)
        {
            Expect.Equal(false, Game.DayStopped, "not stopped at the start");
            Game.DaySpeed = 2.0f;
            Game.DayStopped = true;
            Expect.Equal(true, world.IsDayStopped(), "the game's day stopped");
            Expect.Equal(true, Game.DayStopped, "stopped read back");
            Expect.Near(2.0f, Game.DaySpeed, "the speed kept");
            Game.DayStopped = false;
            Expect.Equal(false, world.IsDayStopped(), "the game's day goes on");
        }
    }
}
