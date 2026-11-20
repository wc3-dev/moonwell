using System;

namespace Moonwell
{
    /// <summary>What the effects read of a stat, and the dice they roll, kept cheap for a unit that never had a stat set.</summary>
    internal static class Reading
    {
        /// <summary>A stat's total on a unit. A unit with no sheet of stats, and no base from its class or type, has the stat's own base, held to its limits as a total is, which is what Get would work out: nothing is made for it.</summary>
        internal static float Of(Unit unit, Stat stat)
        {
            if (unit.stats != null || stat.HasFormulas || stat.Cap != null || stat.TypeBaseOf(unit, out var typed))
            {
                return unit.Get(stat);
            }

            return Plain(stat);
        }

        /// <summary>A player's stat: its total, or the stat's own base held to its limits for a player with no sheet of stats.</summary>
        internal static float Of(Player player, Stat stat)
        {
            return player.stats != null ? player.Get(stat) : Plain(stat);
        }

        /// <summary>A stat's own base, held to its limits.</summary>
        private static float Plain(Stat stat)
        {
            var value = stat.Base;
            if (stat.Least != null && value < stat.Least.Value)
            {
                value = stat.Least.Value;
            }

            if (stat.Most != null && value > stat.Most.Value)
            {
                value = stat.Most.Value;
            }

            return stat.Whole ? MathF.Floor(value + 0.5f) : value;
        }

        /// <summary>Whether a roll at a chance in percent points succeeds: never at 0 or less and always at 100 or more, drawing nothing for either, and otherwise from the game's own generator, the same on every machine.</summary>
        internal static bool Roll(float percent)
        {
            if (percent <= 0.0f)
            {
                return false;
            }

            return percent >= 100.0f || Game.Random.Chance(percent / 100.0f);
        }
    }
}
