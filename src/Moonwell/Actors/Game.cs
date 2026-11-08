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

        private static Dice? random;

        /// <summary>Random numbers from the game's own generator, the same on every machine: Game.Random.Int(1, 6). Not inside Local.For, where only one machine would draw.</summary>
        [LocalSafe]
        public static Dice Random => random ??= Dice.OfGame();

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

        /// <summary>The time of day in hours, from 0 up to the day's 24 (or the map's own number): 6.5 is 6:30. A time set outside them wraps into them. A time set across dawn or dusk runs the game components' OnDawn or OnDusk.</summary>
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
                DayChange.Check();
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

        /// <summary>
        /// Ends the game: the team's players in the game win and every other player in the game loses, as Win and Lose do, the
        /// losers first. A player who has won or lost already is left as they are (decision 197).
        /// </summary>
        public static void Over(Team winners, bool dialog = true, bool scores = true)
        {
            Finish(null, winners, dialog, scores);
        }

        /// <summary>Ends the game with one winner, as in a free-for-all: every other player in the game loses, as Over with a team does.</summary>
        public static void Over(Player winner, bool dialog = true, bool scores = true)
        {
            Finish(winner, null, dialog, scores);
        }

        // Loses every player in the game who is not a winner, then wins the winners; one with a result already is left as it is.
        private static void Finish(Player? winner, Team? winners, bool dialog, bool scores)
        {
            var playing = Player.Playing;
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < playing.Count; i++)
                {
                    var player = playing[i];
                    var wins = player == winner || (winners != null && player.Team == winners);
                    if (player.HasResult || wins != (pass == 1))
                    {
                        continue;
                    }

                    if (wins)
                    {
                        player.Win(dialog, scores);
                    }
                    else
                    {
                        player.Lose();
                    }
                }
            }
        }

        /// <summary>Whether the fog of war is on, for every player; true unless set. Off, every player sees all the ground they have explored; the black mask still covers the rest.</summary>
        [LocalSafe]
        public static bool Fog
        {
            get { return Runtime.World.IsFogOn(); }
            set { Runtime.World.SetFogOn(value); }
        }

        /// <summary>Whether the black mask covers the ground a player has not explored, for every player; true unless set. Off, that ground shows as fog.</summary>
        [LocalSafe]
        public static bool BlackMask
        {
            get { return Runtime.World.IsBlackMaskOn(); }
            set { Runtime.World.SetBlackMaskOn(value); }
        }

        /// <summary>Pings the minimap for every player, in white, for the seconds; it changes nothing in the game. PingStyle.Attack is the game's red warning ping.</summary>
        [LocalSafe]
        public static void Ping(float x, float y, float seconds = 3.0f, PingStyle style = PingStyle.Simple)
        {
            Ping(x, y, Color.White, seconds, style);
        }

        /// <summary>Pings the minimap for every player in a colour, as Ping does. A pure red (255, 0, 0) is drawn as 254, 0, 0, as Blizzard.j does, as the game would make it the warning ping; PingStyle.Attack is that ping, whatever the colour.</summary>
        [LocalSafe]
        public static void Ping(float x, float y, Color color, float seconds = 3.0f, PingStyle style = PingStyle.Simple)
        {
            if (style == PingStyle.Attack)
            {
                Runtime.World.Ping(x, y, seconds, 255, 0, 0, false);
                return;
            }

            var red = color.R == 255 && color.G == 0 && color.B == 0 ? 254 : color.R;
            Runtime.World.Ping(x, y, seconds, red, color.G, color.B, style == PingStyle.Flashy);
        }

        /// <summary>Adds a component to the game, and returns it; held by a scope, it also ends when that scope ends (decision 164).</summary>
        public T Add<T>(T component, Scope? heldBy = null) where T : GameComponent => AddComponent(component, heldBy);
    }
}
