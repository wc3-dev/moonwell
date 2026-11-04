using System;
using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases for RealTime: the calendar from seconds since 1970, the shared clock and this machine's.</summary>
    public static class RealTimeCases
    {
        /// <summary>Second 0 is 1970-01-01 00:00, a Thursday.</summary>
        public static void Epoch(FakeWorld world)
        {
            var time = new RealTime(0);
            ExpectDate(time, 1970, 1, 1, 0, 0, 0);
            Expect.True(time.DayOfWeek == DayOfWeek.Thursday, "a Thursday");
            Expect.Equal("1970-01-01 00:00", time.ToString(), "as text");
        }

        /// <summary>1793664000 is 2026-11-03 00:00 UTC, a Tuesday.</summary>
        public static void KnownDay(FakeWorld world)
        {
            var time = new RealTime(1793664000);
            ExpectDate(time, 2026, 11, 3, 0, 0, 0);
            Expect.True(time.DayOfWeek == DayOfWeek.Tuesday, "a Tuesday");
            Expect.Equal("2026-11-03 00:00", time.ToString(), "as text");
        }

        /// <summary>1709208000 is 2024-02-29 12:00, a leap day.</summary>
        public static void LeapDay(FakeWorld world)
        {
            ExpectDate(new RealTime(1709208000), 2024, 2, 29, 12, 0, 0);
        }

        /// <summary>The last second an int holds is 2038-01-19 03:14:07.</summary>
        public static void Last(FakeWorld world)
        {
            ExpectDate(new RealTime(int.MaxValue), 2038, 1, 19, 3, 14, 7);
        }

        /// <summary>A second before 1970 is 1969-12-31 23:59:59.</summary>
        public static void BeforeEpoch(FakeWorld world)
        {
            ExpectDate(new RealTime(-1), 1969, 12, 31, 23, 59, 59);
        }

        /// <summary>Adding days, hours, minutes and seconds crosses months and days.</summary>
        public static void AddAcross(FakeWorld world)
        {
            var day = new RealTime(1793664000);
            ExpectDate(day.AddDays(28), 2026, 12, 1, 0, 0, 0);
            ExpectDate(day.AddHours(-1), 2026, 11, 2, 23, 0, 0);
            ExpectDate(day.AddMinutes(90), 2026, 11, 3, 1, 30, 0);
            ExpectDate(day.AddSeconds(59), 2026, 11, 3, 0, 0, 59);
        }

        /// <summary>Times compare by their seconds.</summary>
        public static void Compare(FakeWorld world)
        {
            var early = new RealTime(100);
            var late = new RealTime(200);
            Expect.True(early < late && late > early && early <= new RealTime(100) && late >= early, "the order");
            Expect.True(early == new RealTime(100) && early != late, "equality");
            Expect.True(early.CompareTo(late) < 0 && early.Equals(new RealTime(100)) && !early.Equals(late), "CompareTo and Equals");
        }

        /// <summary>The shared clock is the date that arrived plus Moonwell's time since.</summary>
        public static void NowMoves(FakeWorld world)
        {
            RealClock.Arrived(1793664000);
            world.Advance(90.0f);
            ExpectDate(RealTime.Now, 2026, 11, 3, 0, 1, 30);
        }

#if DEBUG
        /// <summary>The shared clock read before the date arrived is reported in a development build, and gives 1970.</summary>
        public static void NowEarlyReported(FakeWorld world)
        {
            var before = world.Printed.Count;
            Expect.Equal(0, RealTime.Now.Seconds, "1970 before the date");
            Expect.Equal(before + 1, world.Printed.Count, "the lines printed");
            Expect.True(world.Printed[before].Contains("not arrived"), "the report says why: " + world.Printed[before]);
        }
#endif

        /// <summary>This machine's clock adds its zone's offset.</summary>
        public static void LocalNow(FakeWorld world)
        {
            world.RealSeconds = 1793664000;
            world.RealOffset = 3600;
            Local.For(Player.Get(world.LocalSlot), () => ExpectDate(RealTime.LocalNow, 2026, 11, 3, 1, 0, 0));
        }

        /// <summary>With the date shared, the game's start waits for it, and runs once it is in.</summary>
        public static void StartWaits(FakeWorld world)
        {
            People(world, 1);
            world.RealSeconds = 1793664000;
            var starts = new List<int>();
            Runtime.ShareStartDate();
            RealClock.Share();
            Runtime.Begin(() => Game.Instance.Add(new StartLogger(starts)));
            Expect.Equal(0, starts.Count, "the start waits for the date");
            world.Advance(0.1f);
            Expect.Equal(1, starts.Count, "the start ran once the date was in");
            Expect.Equal(1793664000, starts[0], "RealTime.Now in OnGameStart");
        }

        /// <summary>A map that does not share the date starts at once.</summary>
        public static void StartWithoutDate(FakeWorld world)
        {
            People(world, 1);
            var starts = new List<int>();
            Runtime.Begin(() => Game.Instance.Add(new StartTally(starts)));
            Expect.Equal(1, starts.Count, "the start ran at once");
        }

        /// <summary>The first person leaves before their date arrives: the next person's is used.</summary>
        public static void FirstPersonLeaves(FakeWorld world)
        {
            People(world, 2);
            world.LocalSlot = 1;
            world.RealSeconds = 1793664000;
            var starts = new List<int>();
            Runtime.ShareStartDate();
            RealClock.Share();
            Runtime.Begin(() => Game.Instance.Add(new StartLogger(starts)));
            world.Advance(0.1f);
            Expect.Equal(0, starts.Count, "waiting for player one's date");
            world.SetSlot(0, SlotState.Left, Controller.User);
            Inbound.Left(world.PlayerAt(0));
            world.Advance(0.1f);
            Expect.Equal(1, starts.Count, "player two's date started the game");
            Expect.Equal(1793664000, starts[0], "player two's date");
        }

        /// <summary>When a saved game loads, Lua has the date of the save; the date is shared again.</summary>
        public static void AfterLoad(FakeWorld world)
        {
            People(world, 1);
            world.RealSeconds = 1000;
            Runtime.ShareStartDate();
            RealClock.Share();
            world.Advance(0.1f);
            Expect.Equal(1000, RealTime.Now.Seconds, "the start's date");
            Expect.True(world.Listening.Contains(Moonwell.Hosting.GameEvent.Loaded), "a loaded game is listened for");
            world.RealSeconds = 90000;
            Inbound.GameLoaded();
            world.Advance(0.1f);
            Expect.Equal(90000, RealTime.Now.Seconds, "the date after the load");
        }

        private static void People(FakeWorld world, int count)
        {
            for (var slot = 0; slot < count; slot++)
            {
                world.SetSlot(slot, SlotState.Playing, Controller.User);
            }
        }

        private static void ExpectDate(RealTime time, int year, int month, int day, int hour, int minute, int second)
        {
            Expect.Equal(year, time.Year, "the year of " + time.Seconds);
            Expect.Equal(month, time.Month, "the month of " + time.Seconds);
            Expect.Equal(day, time.Day, "the day of " + time.Seconds);
            Expect.Equal(hour, time.Hour, "the hour of " + time.Seconds);
            Expect.Equal(minute, time.Minute, "the minute of " + time.Seconds);
            Expect.Equal(second, time.Second, "the second of " + time.Seconds);
        }
    }
}
