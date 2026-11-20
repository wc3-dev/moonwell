using System.ComponentModel;

namespace Moonwell
{
    /// <summary>
    /// Starts the stats package, which gives the ready-made stats on <see cref="Stat"/> their effect on every hit and heal:
    /// a map's generated Main calls Install when the map references the package, after the damage package's.
    /// </summary>
    public static class StatsSystem
    {
        /// <summary>The tag of the hits and heals the package makes itself (a staggered hit's ticks, life steal's heals), which get none of the stats' effects, so nothing feeds itself.</summary>
        internal static readonly object Own = new object();

        /// <summary>Whether the package shows its texts: "miss" and "parry" (a missed hit's own text), "block" when a block leaves nothing, and a crit's amount with "!". A map that sets it false still has its misses, with no text. True by default; read at each hit, so it may change in play.</summary>
        public static bool ShowTexts = true;

        /// <summary>Installs the package on the game, after the damage package: one game component takes part in every hit and heal.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Install()
        {
            if (Game.Instance.Get<StatEffects>() == null)
            {
                Game.Instance.Add(new StatEffects());
            }
        }
    }
}
