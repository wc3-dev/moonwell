using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>Finds the units, items and destructables the game has removed, which fire no event, and ends them. A timer runs it every 0.25 seconds.</summary>
    internal static class Sweep
    {
        /// <summary>How many destructables a run checks; past this many they take turns. Each is a native call, so a run over all of 18,596 stopped the game for 12 ms, four times a second, and at this limit it takes 2.3 ms (game facts, 10-31 and 11-01).</summary>
        internal const int MostDestructables = 2000;

        /// <summary>Ends each live unit and item, and each destructable it comes to, whose type id reads 0, which means the game has removed it, and forgets the handles of the units the runtime removed once theirs reads 0.</summary>
        internal static void Run()
        {
            KnownUnits.ForgetRemoved();
            var units = KnownUnits.Live.Snapshot();
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (Runtime.World.UnitTypeId(unit.Handle) == 0)
                {
                    unit.IsGone = true;
                    unit.End();
                }
            }

            var items = KnownItems.Live.Snapshot();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (Runtime.World.ItemTypeId(item.Handle) == 0)
                {
                    item.IsGone = true;
                    item.End();
                }
            }

            CheckDestructables();
#if DEBUG
            Leaks.Swept();
#endif
        }

        /// <summary>Checks the destructables after the last one checked, at most MostDestructables, going round to the first: up to that many, every run checks them all, from the first. The run's destructables are picked before any ends, so one whose OnLeave removes others makes it miss none.</summary>
        private static void CheckDestructables()
        {
            var live = KnownDestructables.Live;
            var count = live.Count;
            var checks = count < MostDestructables ? count : MostDestructables;
            var start = count <= MostDestructables || KnownDestructables.NextToCheck >= count ? 0 : KnownDestructables.NextToCheck;
            var picked = new List<Destructable>();
            for (var i = 0; i < checks; i++)
            {
                picked.Add(live[(start + i) % count]);
            }

            for (var i = 0; i < picked.Count; i++)
            {
                var destructable = picked[i];
                if (!destructable.HasEnded && !destructable.ending && Runtime.World.DestructableTypeId(destructable.Handle) == 0)
                {
                    destructable.IsGone = true;
                    destructable.End();
                }
            }

            KnownDestructables.NextToCheck = count == 0 ? 0 : (start + checks) % count;
        }
    }
}
