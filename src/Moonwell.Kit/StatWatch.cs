using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>The HUD parts that show a unit's stats (a bar following a stat, a bar's overlay, a StatList), redrawn through the engine's change reaction instead of the 20-a-second poll. A game component, added once, and only when a part first asks to be told.</summary>
    internal sealed class StatWatch : GameComponent
    {
        private static readonly List<Func<Unit, Stat, bool>> watchers = new List<Func<Unit, Stat, bool>>();

        /// <summary>The game the component is on; a new game (a test's) starts afresh.</summary>
        private static Game? game;

        /// <summary>Adds a watcher, run on each change of a stat of a unit until it answers false, and adds the component when it is the first.</summary>
        internal static void Join(Func<Unit, Stat, bool> watcher)
        {
            if (game != Game.Instance)
            {
                game = Game.Instance;
                watchers.Clear();
                game.Add(new StatWatch());
            }

            watchers.Add(watcher);
        }

        /// <summary>Tells each watcher, in the order they joined, and drops those done.</summary>
        public override void OnUnitStatChanged(Unit unit, Stat stat, float before)
        {
            var kept = 0;
            var count = watchers.Count;
            for (var i = 0; i < count; i++)
            {
                if (watchers[i](unit, stat))
                {
                    watchers[kept] = watchers[i];
                    kept++;
                }
            }

            // A watcher that joined during the loop is after count.
            for (var i = count; i < watchers.Count; i++)
            {
                watchers[kept] = watchers[i];
                kept++;
            }

            watchers.RemoveRange(kept, watchers.Count - kept);
        }
    }
}
