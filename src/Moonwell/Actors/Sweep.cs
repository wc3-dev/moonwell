namespace Moonwell
{
    /// <summary>Finds the units, items and destructables the game has removed, which fire no event, and ends them. A timer runs it every 0.25 seconds.</summary>
    internal static class Sweep
    {
        /// <summary>How many destructables a run checks; past this many they take turns. Each is a native call, about 0.6 us with its loop, so a run over 18,596 stopped the game for 12 ms, four times a second (game facts, 10-31).</summary>
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
        }

        /// <summary>Checks the destructables after the last one checked, at most MostDestructables, going round to the first: up to that many, every run checks them all, from the first.</summary>
        private static void CheckDestructables()
        {
            var live = KnownDestructables.Live;
            var checks = live.Count < MostDestructables ? live.Count : MostDestructables;
            var index = live.Count <= MostDestructables ? 0 : KnownDestructables.NextToCheck;
            for (var i = 0; i < checks && live.Count > 0; i++)
            {
                index = index < live.Count ? index : 0;
                var destructable = live[index];
                if (Runtime.World.DestructableTypeId(destructable.Handle) == 0)
                {
                    destructable.IsGone = true;
                    destructable.End();
                }

                // One that ended has left the list, and the next is in its place.
                if (index < live.Count && live[index] == destructable)
                {
                    index++;
                }
            }

            KnownDestructables.NextToCheck = index;
        }
    }
}
