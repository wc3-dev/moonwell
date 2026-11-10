using System;

namespace Moonwell.Engine
{
    /// <summary>Where the pathing package hears that something changed what blocks paths: Moonwell's own destructable changes and the units it takes in and forgets (decision 247). Null while the package is not installed.</summary>
    internal static class PathingHook
    {
        /// <summary>Called with the place of a destructable that was made, killed, restored, opened, closed or removed.</summary>
        internal static Action<float, float>? Changed;

        /// <summary>Called with a unit Moonwell took in or forgot, so the package can tell when a building comes or goes.</summary>
        internal static Action<unit>? UnitChanged;

        /// <summary>Tells the pathing package, when it listens, that what blocks paths changed at a place.</summary>
        internal static void Tell(float x, float y)
        {
            Changed?.Invoke(x, y);
        }
    }
}
