namespace Moonwell.Testing
{
    /// <summary>A floating text the runtime asked the fake world for, and everything it set on it since.</summary>
    public sealed class FakeText
    {
        /// <summary>Its words and height in the game's units, as last written.</summary>
        public string Words = "";

        /// <summary>Its height in the game's units, as last written.</summary>
        public float LetterHeight;

        /// <summary>The unit it was last placed over; null for one at a place.</summary>
        public unit? Over;

        /// <summary>Its place and height, as last given.</summary>
        public float X, Y, Height;

        /// <summary>How many times the runtime placed it.</summary>
        public int Places;

        /// <summary>Its red, green, blue and alpha; null until coloured.</summary>
        public int[]? Color;

        /// <summary>How many times the runtime coloured it.</summary>
        public int Colors;

        /// <summary>Its velocity in the game's units.</summary>
        public float XVelocity, YVelocity;

        /// <summary>Whether it shows on this machine; true until hidden.</summary>
        public bool Shown = true;

        /// <summary>Whether the runtime ended it.</summary>
        public bool Ended;
    }
}
