using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// The live units, items or destructables, in the order taken in: a set like OrderedSet, whose removal walks no list. A
    /// removed one waits in gone, and the next read in order takes them all out in one walk, so 500 deaths among 1,500 units
    /// cost one walk, not 500. Loop over Snapshot when the set may change.
    /// </summary>
    internal sealed class LiveSet<T> where T : notnull
    {
        private readonly List<T> items = new List<T>();
        private readonly HashSet<T> lookup = new HashSet<T>();
        private readonly HashSet<T> gone = new HashSet<T>();

        /// <summary>How many are in the set.</summary>
        internal int Count => lookup.Count;

        /// <summary>The one at an index, in the order added.</summary>
        internal T this[int index]
        {
            get
            {
                Compact();
                return items[index];
            }
        }

        /// <summary>Adds one at the end; false when it is in the set already. One removed and added again goes last.</summary>
        internal bool Add(T item)
        {
            if (!lookup.Add(item))
            {
                return false;
            }

            if (gone.Count > 0 && gone.Contains(item))
            {
                Compact();
            }

            items.Add(item);
            return true;
        }

        /// <summary>Removes one; false when it is not in the set.</summary>
        internal bool Remove(T item)
        {
            if (!lookup.Remove(item))
            {
                return false;
            }

            gone.Add(item);
            return true;
        }

        /// <summary>Whether one is in the set.</summary>
        internal bool Contains(T item)
        {
            return lookup.Contains(item);
        }

        /// <summary>Empties the set.</summary>
        internal void Clear()
        {
            items.Clear();
            lookup.Clear();
            gone.Clear();
        }

        /// <summary>A copy of the set in order, to loop over while it may change.</summary>
        internal List<T> Snapshot()
        {
            Compact();
            return new List<T>(items);
        }

        private void Compact()
        {
            if (gone.Count == 0)
            {
                return;
            }

            var kept = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (!gone.Contains(items[i]))
                {
                    items[kept] = items[i];
                    kept++;
                }
            }

            items.RemoveRange(kept, items.Count - kept);
            gone.Clear();
        }
    }
}
