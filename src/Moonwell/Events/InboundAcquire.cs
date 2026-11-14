namespace Moonwell
{
    /// <summary>A unit picking a target by itself, which the game tells only by its acquire event: no order comes (game facts, 11-14).</summary>
    internal static partial class Inbound
    {
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
