namespace Moonwell.Testing
{
    /// <summary>A point of a frame tied to a point of another, as the package gave it to the fake world.</summary>
    public sealed class FakePoint
    {
        /// <summary>The frame's point, as the game numbers them: 0 top left to 8 bottom right.</summary>
        public int Point;

        /// <summary>The frame it is tied to.</summary>
        public FakeFrame Other = null!;

        /// <summary>The other frame's point.</summary>
        public int OtherPoint;

        /// <summary>The offset from the other's point.</summary>
        public float X, Y;
    }
}
