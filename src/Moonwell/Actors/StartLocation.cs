namespace Moonwell
{
    /// <summary>A slot's start in the map, set on its Slot: <c>new Slot(...) { Start = new(64, 5120) }</c>.</summary>
    public readonly struct StartLocation
    {
        /// <summary>Its x.</summary>
        public readonly float X;

        /// <summary>Its y.</summary>
        public readonly float Y;

        /// <summary>A start at x and y.</summary>
        [LocalSafe]
        public StartLocation(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
