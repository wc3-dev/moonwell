namespace Moonwell
{
    /// <summary>A weather effect the runtime asked the fake world for.</summary>
    public sealed class FakeWeather
    {
        /// <summary>Its four-letter id, its rect, whether it is on at this machine, and whether it was removed.</summary>
        public string Kind = "";
        public float Left, Bottom, Right, Top;
        public bool Shown;
        public bool Ended;
    }

    /// <summary>A lightning the runtime asked the fake world for, as it was last placed.</summary>
    public sealed class FakeBolt
    {
        /// <summary>Its code, its two ends with absolute heights, how many times it was moved, and whether it was removed.</summary>
        public string Kind = "";
        public float X1, Y1, Z1, X2, Y2, Z2;
        public int Moves;
        public bool Ended;
    }

    /// <summary>A fog modifier the runtime asked the fake world for: a circle, or a rect when Radius is 0.</summary>
    public sealed class FakeVision
    {
        /// <summary>The player, the state (0 visible, 1 fogged, 2 masked), the place, whether it beats units' sight, and whether it ended.</summary>
        public player Player = null!;
        public int State;
        public float X, Y, Radius;
        public float Left, Bottom, Right, Top;
        public bool OverUnits;
        public bool Ended;
    }

    /// <summary>A minimap ping this machine showed.</summary>
    public sealed class FakePing
    {
        /// <summary>The place, the seconds, the colour's parts and whether it was flashy.</summary>
        public float X, Y, Seconds;
        public int Red, Green, Blue;
        public bool Flashy;
    }

    /// <summary>A minimap icon the runtime asked the fake world for.</summary>
    public sealed class FakeIcon
    {
        /// <summary>The model, the place or the unit it follows, the colour's parts, whether it shows at this machine, and whether it was removed.</summary>
        public string Path = "";
        public float X, Y;
        public unit? Unit;
        public int Red, Green, Blue;
        public bool Shown = true;
        public bool Ended;
    }
}
