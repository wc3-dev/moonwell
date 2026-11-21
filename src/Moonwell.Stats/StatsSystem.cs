using System.ComponentModel;

namespace Moonwell
{
    /// <summary>
    /// Starts the stats package, which gives the ready-made stats on <see cref="Stat"/> their effect on every hit and heal, on a unit's abilities and cast speed, and on the experience and gold of a kill:
    /// a map's generated Main calls Install when the map references the package, after the damage package's.
    /// </summary>
    public static class StatsSystem
    {
        /// <summary>The tag of the hits and heals the package makes itself (a staggered hit's ticks, life steal's heals), which get none of the stats' effects, so nothing feeds itself.</summary>
        internal static readonly object Own = new object();

        /// <summary>Whether the package shows its texts: "miss" and "parry" (a missed hit's own text) and a crit's amount with "!", over the attacker as the game's own, and "block" when a block leaves nothing, over the target. A map that sets it false still has its misses, with no text, and may draw its own from a damage reaction. True by default; read at each hit, so it may change in play.</summary>
        public static bool ShowTexts = true;

        /// <summary>Installs the package on the game, after the damage package: one game component takes part in every hit and heal.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Install()
        {
            var effects = Game.Instance.Get<StatEffects>();
            if (effects == null)
            {
                effects = new StatEffects();
                Game.Instance.Add(effects);
                Hooks.AddTakenIn(effects.TakenIn);
            }

            Events.AbilitiesListener = effects.Abilities.PutScales;

            // A hero learning a skill, by the game's own button too, has its scales put at once: no class of the map need react to it.
            Events.Need(Hook.OnHeroSkillLearned);
            Events.DeathListener = effects.Died;
            Pipeline.KillListener = effects.Killing;
        }
    }
}
