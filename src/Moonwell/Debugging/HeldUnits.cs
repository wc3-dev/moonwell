#if DEBUG
using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>
    /// The units a halt holds: every unit in the world, the ones Moonwell never took in too, but no dummy. It pauses only
    /// units not already paused, and lets go only those, as PauseUnit is a flag and not a count, so a unit the map paused
    /// itself stays paused. While a unit is held, a pause the map sets on it is kept here and given to it as the halt lets go.
    /// </summary>
    internal sealed class HeldUnits
    {
        /// <summary>The debug world that pauses units.</summary>
        private readonly IDebugWorld world;

        /// <summary>The units this has paused, in the order they were held.</summary>
        private readonly List<unit> held = new List<unit>();

        /// <summary>For each held unit, whether the map has asked for it to be paused since it was held.</summary>
        private readonly Dictionary<unit, bool> wanted = new Dictionary<unit, bool>();

        /// <summary>For each held unit, its object when Moonwell held one, to tell one it removed since.</summary>
        private readonly Dictionary<unit, Unit?> objects = new Dictionary<unit, Unit?>();

        /// <summary>Makes the held units of a debug world, none held.</summary>
        internal HeldUnits(IDebugWorld world)
        {
            this.world = world;
        }

        /// <summary>Pauses every live unit not already paused, and remembers those.</summary>
        internal void Hold()
        {
            var live = KnownUnits.Live;
            for (var i = 0; i < live.Count; i++)
            {
                Add(live[i]);
            }

            var all = Runtime.World.UnitsInWorld();
            for (var i = 0; i < all.Count; i++)
            {
                AddHandle(all[i], KnownUnits.Find(all[i]));
            }
        }

        /// <summary>Pauses one unit when it is live and not already paused, and remembers it.</summary>
        internal void Add(Unit unit)
        {
            if (!unit.HasEnded && !unit.IsGone)
            {
                AddHandle(unit.Handle, unit);
            }
        }

        /// <summary>Lets go of the units this paused that are still in the game, but not one the map paused while it was held, and forgets them all.</summary>
        internal void LetGo()
        {
            for (var i = 0; i < held.Count; i++)
            {
                var handle = held[i];
                var unit = objects[handle];
                var gone = unit != null ? unit.HasEnded || unit.IsGone : Runtime.World.UnitTypeId(handle) == 0;
                if (!wanted[handle] && !gone)
                {
                    world.Pause(handle, false);
                }
            }

            held.Clear();
            wanted.Clear();
            objects.Clear();
        }

        /// <summary>Whether a unit is held, and then the pause the map last asked for on it while it was.</summary>
        internal bool Holds(unit handle, out bool paused)
        {
            return wanted.TryGetValue(handle, out paused);
        }

        /// <summary>Keeps the pause the map asks for on a held unit, to give it as the halt lets go; false for a unit not held.</summary>
        internal bool Want(unit handle, bool paused)
        {
            if (!wanted.ContainsKey(handle))
            {
                return false;
            }

            wanted[handle] = paused;
            return true;
        }

        private void AddHandle(unit handle, Unit? unit)
        {
            if (wanted.ContainsKey(handle) || Dummies.IsDummy(handle) || Runtime.World.UnitTypeId(handle) == 0 || world.IsPaused(handle))
            {
                return;
            }

            world.Pause(handle, true);
            held.Add(handle);
            wanted[handle] = false;
            objects[handle] = unit;
        }
    }
}
#endif
