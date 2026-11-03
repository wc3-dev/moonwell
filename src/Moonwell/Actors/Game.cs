namespace Moonwell
{
    /// <summary>The game: the actor of the components that react to the map as a whole, and the scope of what lasts the whole game.</summary>
    public sealed class Game : Actor
    {
        /// <summary>Makes the game of a runtime; Runtime.Reset does.</summary>
        internal Game()
        {
        }

        /// <summary>The game of this runtime; set by Runtime.Reset.</summary>
        [LocalSafe]
        public static Game Instance { get; internal set; } = null!;

        /// <summary>Shows a line to every player, for the seconds, or for the game's own time without them; it changes nothing in the game.</summary>
        [LocalSafe]
        public static void Print(string text, float seconds = -1.0f)
        {
            Runtime.World.PrintToAll(text, seconds);
        }

        /// <summary>Writes a line to the log, Documents\Warcraft III\CustomMapData\Moonwell\log.txt, with Moonwell's errors; it changes nothing in the game.</summary>
        [LocalSafe]
        public static void Log(string text)
        {
            Runtime.World.Log(text);
        }

        /// <summary>The seconds since the game started, on Moonwell's clock: in a development build it halts and changes speed with the debug mode, as timers do.</summary>
        [LocalSafe]
        public static float Time => Runtime.World.Now();

        /// <summary>The time of day in hours, from 0 up to the day's 24 (or the map's own number): 6.5 is 6:30. A time set outside them wraps into them.</summary>
        [LocalSafe]
        public static float TimeOfDay
        {
            get
            {
                return Runtime.World.HourOfDay();
            }

            set
            {
                var hours = value % DayConstants.Hours;
                if (hours < 0.0f)
                {
                    hours += DayConstants.Hours;
                }

                Runtime.World.SetHourOfDay(hours);
            }
        }

        /// <summary>Whether it is day: from dawn (6:00) up to dusk (18:00), or the map's own hours.</summary>
        [LocalSafe]
        public static bool IsDay
        {
            get
            {
                var hours = TimeOfDay;
                var dawn = DayConstants.Dawn;
                var dusk = DayConstants.Dusk;
                return dawn <= dusk ? hours >= dawn && hours < dusk : hours >= dawn || hours < dusk;
            }
        }

        /// <summary>How fast the time of day goes: 1 is the game's own, an hour every 20 s; 2 is twice as fast, 0 stops it and below 0 runs it backward.</summary>
        [LocalSafe]
        public static float DaySpeed
        {
            get { return Runtime.World.DayScale(); }
            set { Runtime.World.SetDayScale(value); }
        }

        /// <summary>Whether the time of day stands still; it goes on at its speed when set back to false.</summary>
        [LocalSafe]
        public static bool DayStopped
        {
            get { return Runtime.World.IsDayStopped(); }
            set { Runtime.World.StopDay(value); }
        }

        /// <summary>Adds a component to the game, and returns it.</summary>
        public T Add<T>(T component) where T : GameComponent => AddComponent(component);
    }
}
