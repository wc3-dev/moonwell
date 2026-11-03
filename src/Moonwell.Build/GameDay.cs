using System.Globalization;

namespace Moonwell.Build
{
    /// <summary>The day's hours from the gameplay constants, which the runtime's <c>Game.IsDay</c> and <c>Game.TimeOfDay</c> read.</summary>
    /// <param name="Dawn">The hour the day starts at (<c>Dawn</c>).</param>
    /// <param name="Dusk">The hour the night starts at (<c>Dusk</c>).</param>
    /// <param name="Hours">The hours in a day (<c>DayHours</c>).</param>
    public sealed record GameDay(float Dawn, float Dusk, float Hours)
    {
        /// <summary>The standard constants, which the runtime has: <c>units/miscdata.txt</c> (read in the game's files, 11-02).</summary>
        public static GameDay Standard { get; } = new GameDay(6f, 18f, 24f);

        /// <summary>The build's warning for a day longer than 24 hours, whose time of day the game reads as 24 past 24 (game facts, 11-02); null for any other day.</summary>
        internal string? Warning => Hours > 24f
            ? "warning: the map's gameplay constants give a day of " + Number(Hours) + " hours, but the game reads no time of day past 24: Game.TimeOfDay reads 24 for the day's last " + Number(Hours - 24f) + " hours"
            : null;

        /// <summary>The call in the map's Main that installs these constants.</summary>
        internal string Install()
        {
            return "global::Moonwell.Runtime.InstallDay(" + Literal(Dawn) + ", " + Literal(Dusk) + ", " + Literal(Hours) + ");";
        }

        private static string Literal(float value)
        {
            return Number(value) + "f";
        }

        private static string Number(float value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
