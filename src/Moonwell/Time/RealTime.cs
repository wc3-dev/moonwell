using System;
using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>
    /// A real-world date and time, in seconds since 1970 (decisions 173 to 175): <c>RealTime.Now</c> is the same on every
    /// machine, in UTC, and <c>RealTime.LocalNow</c> is this machine's clock in its own zone. An <c>int</c> of seconds lasts
    /// to January 2038, as the game's own clock does. .NET's <c>DateTime</c> cannot work in the game, whose integers are 32 bits.
    /// </summary>
    public readonly struct RealTime : IEquatable<RealTime>, IComparable<RealTime>
    {
        private const int SecondsADay = 86400;

        /// <summary>A time from its seconds since 1970-01-01 00:00.</summary>
        public RealTime(int seconds)
        {
            Seconds = seconds;
        }

        /// <summary>The seconds since 1970-01-01 00:00.</summary>
        [LocalSafe]
        public int Seconds { get; }

        /// <summary>
        /// The date and time now, the same on every machine and in UTC: the first person's clock at the start, and the game's
        /// time since. A long pause makes it run behind the wall clock, by as long as the pause.
        /// </summary>
        [LocalSafe]
        public static RealTime Now => RealClock.Now();

        /// <summary>This machine's own date and time, in its own zone; it differs between machines, so it is read inside Local.For.</summary>
        [MachineLocal]
        public static RealTime LocalNow => new RealTime(Runtime.World.ClockSeconds() + Runtime.World.ZoneOffset());

        /// <summary>The year, such as 2026.</summary>
        [LocalSafe]
        public int Year => Civil(out _, out _);

        /// <summary>The month, 1 to 12.</summary>
        [LocalSafe]
        public int Month
        {
            get
            {
                Civil(out var month, out _);
                return month;
            }
        }

        /// <summary>The day of the month, 1 to 31.</summary>
        [LocalSafe]
        public int Day
        {
            get
            {
                Civil(out _, out var day);
                return day;
            }
        }

        /// <summary>The hour, 0 to 23.</summary>
        [LocalSafe]
        public int Hour => SecondOfDay / 3600;

        /// <summary>The minute, 0 to 59.</summary>
        [LocalSafe]
        public int Minute => SecondOfDay / 60 % 60;

        /// <summary>The second, 0 to 59.</summary>
        [LocalSafe]
        public int Second => SecondOfDay % 60;

        /// <summary>The day of the week.</summary>
        [LocalSafe]
        public DayOfWeek DayOfWeek => (DayOfWeek)(((Days + 4) % 7 + 7) % 7);

        // Whole days since 1970-01-01, rounded down, so a time before it falls in the day before.
        private int Days => Seconds / SecondsADay - (Seconds % SecondsADay < 0 ? 1 : 0);

        private int SecondOfDay => Seconds - Days * SecondsADay;

        /// <summary>This time and some days.</summary>
        public RealTime AddDays(int days) => new RealTime(Seconds + days * SecondsADay);

        /// <summary>This time and some hours.</summary>
        public RealTime AddHours(int hours) => new RealTime(Seconds + hours * 3600);

        /// <summary>This time and some minutes.</summary>
        public RealTime AddMinutes(int minutes) => new RealTime(Seconds + minutes * 60);

        /// <summary>This time and some seconds.</summary>
        public RealTime AddSeconds(int seconds) => new RealTime(Seconds + seconds);

        /// <summary>The time as text: "2026-11-03 14:05".</summary>
        public override string ToString()
        {
            var year = Civil(out var month, out var day);
            return year + "-" + Two(month) + "-" + Two(day) + " " + Two(Hour) + ":" + Two(Minute);
        }

        /// <summary>Whether two times are the same second.</summary>
        public bool Equals(RealTime other) => Seconds == other.Seconds;

        /// <summary>Whether an object is a time of the same second.</summary>
        public override bool Equals(object? obj) => obj is RealTime other && Equals(other);

        /// <summary>The seconds, as a hash.</summary>
        public override int GetHashCode() => Seconds;

        /// <summary>Below 0 for an earlier time, 0 for the same, above 0 for a later one.</summary>
        public int CompareTo(RealTime other) => Seconds < other.Seconds ? -1 : Seconds > other.Seconds ? 1 : 0;

        /// <summary>Whether two times are the same second.</summary>
        public static bool operator ==(RealTime a, RealTime b) => a.Seconds == b.Seconds;

        /// <summary>Whether two times differ.</summary>
        public static bool operator !=(RealTime a, RealTime b) => a.Seconds != b.Seconds;

        /// <summary>Whether a time is earlier.</summary>
        public static bool operator <(RealTime a, RealTime b) => a.Seconds < b.Seconds;

        /// <summary>Whether a time is later.</summary>
        public static bool operator >(RealTime a, RealTime b) => a.Seconds > b.Seconds;

        /// <summary>Whether a time is earlier or the same.</summary>
        public static bool operator <=(RealTime a, RealTime b) => a.Seconds <= b.Seconds;

        /// <summary>Whether a time is later or the same.</summary>
        public static bool operator >=(RealTime a, RealTime b) => a.Seconds >= b.Seconds;

        // The year, month and day of the days since 1970, by Howard Hinnant's civil_from_days; no step passes 2^31.
        private int Civil(out int month, out int day)
        {
            var z = Days + 719468;
            var era = (z >= 0 ? z : z - 146096) / 146097;
            var dayOfEra = z - era * 146097;
            var yearOfEra = (dayOfEra - dayOfEra / 1460 + dayOfEra / 36524 - dayOfEra / 146096) / 365;
            var dayOfYear = dayOfEra - (365 * yearOfEra + yearOfEra / 4 - yearOfEra / 100);
            var shifted = (5 * dayOfYear + 2) / 153;
            day = dayOfYear - (153 * shifted + 2) / 5 + 1;
            month = shifted < 10 ? shifted + 3 : shifted - 9;
            return yearOfEra + era * 400 + (month <= 2 ? 1 : 0);
        }

        private static string Two(int value) => value < 10 ? "0" + value : value.ToString();
    }

    /// <summary>
    /// The shared date: the first person's clock, shared with every machine while the map loads, and again when a saved game
    /// loads (decisions 173 and 174). The game's start waits for it.
    /// </summary>
    internal static class RealClock
    {
        private static int start;
        private static float arrivedAt;
        private static Action? waitingStart;

        /// <summary>
        /// Whether the map shares the date: set while the map loads, before the runtime starts, so the runtime's reset keeps
        /// it.
        /// </summary>
        internal static bool Wanted { get; set; }

        /// <summary>Whether the shared date has arrived.</summary>
        internal static bool HasArrived { get; private set; }

        /// <summary>Forgets the date; the runtime's reset calls it.</summary>
        internal static void Reset()
        {
            start = 0;
            arrivedAt = 0.0f;
            HasArrived = false;
            waitingStart = null;
        }

        /// <summary>Asks the first person in slot order for their clock; if they leave before it arrives, the next one.</summary>
        internal static void Share()
        {
            Events.Listen(GameEvent.Loaded);
            var people = Player.Users;
            if (people.Count == 0)
            {
                return;
            }

            Sharing.Request(Game.Instance, people[0], () => Runtime.World.ClockSeconds().ToString(), text =>
            {
                if (text == null)
                {
                    Share();
                }
                else if (int.TryParse(text, out var seconds))
                {
                    Arrived(seconds);
                }
            });
        }

        /// <summary>A saved game loaded: the date in Lua is the save's, so it is shared again.</summary>
        internal static void Loaded()
        {
            if (Wanted)
            {
                Share();
            }
        }

        /// <summary>Runs the game's start now, or once the date is in when the map shares it.</summary>
        internal static void WhenReady(Action startGame)
        {
            if (!Wanted || HasArrived)
            {
                startGame();
                return;
            }

            waitingStart = startGame;
        }

        /// <summary>The shared date arrived, as seconds since 1970, now; a start that waited for it runs.</summary>
        internal static void Arrived(int seconds)
        {
            start = seconds;
            arrivedAt = Runtime.World.Now();
            HasArrived = true;
            var startGame = waitingStart;
            waitingStart = null;
            startGame?.Invoke();
        }

        /// <summary>The shared date now; 1970 before it has arrived, reported in development builds.</summary>
        internal static RealTime Now()
        {
            if (!HasArrived)
            {
                Dev.Report("RealTime.Now", "the shared date has not arrived yet; it is ready from OnGameStart");
                return new RealTime(0);
            }

            return new RealTime(start + (int)(Runtime.World.Now() - arrivedAt));
        }
    }
}
