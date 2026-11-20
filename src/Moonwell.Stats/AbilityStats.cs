using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// The ability stats' effect: cooldown reduction, mana cost reduction and cast range scale every ability of a unit through core's scales, and
    /// cast speed divides the unit's cast point and backswing. All take effect from the next cast; a cooldown already running is not touched.
    /// </summary>
    internal sealed class AbilityStats
    {
        /// <summary>The units whose abilities were scaled, so that they are put back to 1 when the stats return to no change.</summary>
        private readonly HashSet<Unit> scaled = new HashSet<Unit>();

        /// <summary>A unit's own cast point and backswing, kept from before the first change of its cast speed.</summary>
        private readonly Dictionary<Unit, float[]> ownCasting = new Dictionary<Unit, float[]>();

        /// <summary>Whether the abilities are being walked, so that the notice an ability's object first made sends does not start another walk.</summary>
        private bool walking;

        /// <summary>Puts the scales of every ability of the unit from its three stats; a unit whose stats are at no change, and never had one set, is not walked.</summary>
        internal void PutScales(Unit unit)
        {
            if (walking)
            {
                return;
            }

            var cooldown = Reading.Of(unit, Stat.CooldownReduction);
            var mana = Reading.Of(unit, Stat.ManaCostReduction);
            var range = Reading.Of(unit, Stat.CastRange);
            var none = cooldown == 0.0f && mana == 0.0f && range == 0.0f;
            if (none && !scaled.Contains(unit))
            {
                return;
            }

            if (unit.IsGone)
            {
                scaled.Remove(unit);
                return;
            }

            walking = true;
            try
            {
                var abilities = unit.LiveAbilities;
                for (var i = 0; i < abilities.Count; i++)
                {
                    abilities[i].CooldownScale = 1.0f - cooldown / 100.0f;
                    abilities[i].ManaCostScale = 1.0f - mana / 100.0f;
                    abilities[i].RangeScale = 1.0f + range / 100.0f;
                }
            }
            finally
            {
                walking = false;
            }

            if (none)
            {
                scaled.Remove(unit);
            }
            else
            {
                scaled.Add(unit);
            }
        }

        /// <summary>Puts the unit's cast point and backswing at its own numbers divided by 1 + cast speed / 100; the own numbers are read from the game at the first change and given back exactly when the cast speed returns to 0.</summary>
        internal void PutCastSpeed(Unit unit)
        {
            if (unit.IsGone)
            {
                ownCasting.Remove(unit);
                return;
            }

            var speed = 1.0f + Reading.Of(unit, Stat.CastSpeed) / 100.0f;
            var world = Runtime.World;
            if (!ownCasting.TryGetValue(unit, out var own))
            {
                if (speed == 1.0f)
                {
                    return;
                }

                own = new float[] { world.CastPointOf(unit.Handle), world.CastBackswingOf(unit.Handle) };
                ownCasting[unit] = own;
            }

            world.SetCastPoint(unit.Handle, own[0] / speed);
            world.SetCastBackswing(unit.Handle, own[1] / speed);
            if (speed == 1.0f)
            {
                ownCasting.Remove(unit);
            }
        }
    }
}
