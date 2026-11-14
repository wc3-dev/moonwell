namespace Moonwell
{
    /// <summary>A unit picking a target by itself, which the game tells only by its acquire event: no order comes (game facts, 11-14).</summary>
    internal static partial class Inbound
    {
        /// <summary>Has the world deliver the unit's acquire event, once in the unit's life.</summary>
        internal static void WatchAcquire(Unit watched)
        {
            // shortcut: a pooled unit made again is a new Unit on the same handle, so it is registered once more and each pick is heard twice; key it by handle if that matters.
            if (watched.AcquireWatched)
            {
                return;
            }

            watched.AcquireWatched = true;
            Runtime.World.WatchAcquire(watched.Handle);
        }

        /// <summary>A watched unit picks a target by itself: the AI package's listener hears it, unless the unit is ending.</summary>
        internal static void Acquired(unit handle)
        {
            var listener = Events.AcquireListener;
            var acquiring = KnownUnits.Find(handle);
            if (listener != null && acquiring != null && !acquiring.ending && !acquiring.HasEnded)
            {
                listener(acquiring);
            }
        }
    }
}
