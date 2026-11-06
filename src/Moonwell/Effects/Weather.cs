namespace Moonwell
{
    /// <summary>
    /// Weather over the whole map or a rect: rain, snow, wind, fog and the rest of WeatherKind. It is in the scope it was
    /// made in and ends when that scope ends, or by End. Given shownTo, only that player sees it: it is made on every
    /// machine and turned on at that player's alone. Every read gives what was made, the same on every machine.
    /// </summary>
    public sealed class Weather : Scoped
    {
        private readonly string kind;

        private readonly float left;

        private readonly float bottom;

        private readonly float right;

        private readonly float top;

        private readonly Player? shownTo;

        private weathereffect? handle;

        private Weather(string kind, float left, float bottom, float right, float top, Player? shownTo)
        {
            this.kind = kind;
            this.left = left;
            this.bottom = bottom;
            this.right = right;
            this.top = top;
            this.shownTo = shownTo;
        }

        /// <summary>The weather's four-letter id, as WeatherKind names it.</summary>
        [LocalSafe]
        public string Kind => kind;

        /// <summary>The left edge of the rect it covers.</summary>
        [LocalSafe]
        public float Left => left;

        /// <summary>The bottom edge of the rect it covers.</summary>
        [LocalSafe]
        public float Bottom => bottom;

        /// <summary>The right edge of the rect it covers.</summary>
        [LocalSafe]
        public float Right => right;

        /// <summary>The top edge of the rect it covers.</summary>
        [LocalSafe]
        public float Top => top;

        /// <summary>The game's weather effect, for anything Moonwell does not do; null for weather that ended before the game made it.</summary>
        [LocalSafe]
        public weathereffect? Handle => handle;

        /// <summary>Makes weather over the whole map in a scope, which ends it when it ends, and returns it. Given shownTo, only that player sees it.</summary>
        public static Weather Create(string kind, Scope scope, Player? shownTo = null)
        {
            var world = Runtime.World.WorldBounds();
            return Create(kind, world[0], world[1], world[2], world[3], scope, shownTo);
        }

        /// <summary>Makes weather over a rect, by its left, bottom, right and top edges, in a scope, which ends it when it ends, and returns it. Given shownTo, only that player sees it. In a scope that has ended it ends at once, and the game is not asked to make it.</summary>
        public static Weather Create(string kind, float left, float bottom, float right, float top, Scope scope, Player? shownTo = null)
        {
            var weather = new Weather(kind, left, bottom, right, top, shownTo);
            scope.Adopt(weather);
            if (!weather.HasEnded)
            {
                weather.handle = Runtime.World.MakeWeather(kind, left, bottom, right, top);
                Runtime.World.ShowWeather(weather.handle, shownTo == null || Runtime.World.IsLocal(shownTo.Handle));
            }

            return weather;
        }

        /// <summary>Whether a player sees the weather: the one it was shown to, or everyone. The same on every machine.</summary>
        [LocalSafe]
        public bool IsShownTo(Player player)
        {
            return shownTo == null || shownTo == player;
        }

        /// <summary>Removes the game's weather.</summary>
        internal override void Finish()
        {
            if (handle != null)
            {
                Runtime.World.EndWeather(handle);
            }
        }
    }
}
