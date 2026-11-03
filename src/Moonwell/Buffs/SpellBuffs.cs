using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>The watch for the buffs the game puts on (decision 152): the map's buff classes whose game buff an ability lists, which the build registers, and which abilities list each.</summary>
    internal static class SpellBuffs
    {
        /// <summary>The game buffs watched, in the order registered.</summary>
        private static List<int> watched = new List<int>();

        /// <summary>The maker of each watched class, by its game buff.</summary>
        private static Dictionary<int, Func<Buff>> makers = new Dictionary<int, Func<Buff>>();

        /// <summary>The abilities that list each watched buff, by the game buff, in the order registered.</summary>
        private static Dictionary<int, List<int>> listers = new Dictionary<int, List<int>>();

        /// <summary>The abilities that are auras and list a watched buff, in the order registered.</summary>
        private static List<int> auras = new List<int>();

        /// <summary>Whether any buff is watched.</summary>
        internal static bool Any => watched.Count > 0;

        /// <summary>Registers a watched class by its game buff.</summary>
        internal static void Watch(int buffId, Func<Buff> make)
        {
            if (makers.ContainsKey(buffId))
            {
                throw new MoonwellException("ObjectTypes.WatchedBuff: buff " + buffId + " is registered twice");
            }

            makers[buffId] = make;
            watched.Add(buffId);
            listers[buffId] = new List<int>();
        }

        /// <summary>Registers an ability that lists a watched buff, and whether it is an aura.</summary>
        internal static void List(int abilityId, int buffId, bool aura)
        {
            if (!listers.TryGetValue(buffId, out var abilities))
            {
                throw new MoonwellException("ObjectTypes.ListsBuff: buff " + buffId + " is not watched");
            }

            if (!abilities.Contains(abilityId))
            {
                abilities.Add(abilityId);
            }

            if (aura && !auras.Contains(abilityId))
            {
                auras.Add(abilityId);
            }
        }

        /// <summary>Forgets every registration; Runtime.Reset calls it through ObjectTypes.</summary>
        internal static void Reset()
        {
            watched = new List<int>();
            makers = new Dictionary<int, Func<Buff>>();
            listers = new Dictionary<int, List<int>>();
            auras = new List<int>();
        }
    }
}
