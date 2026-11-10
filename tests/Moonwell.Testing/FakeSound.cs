namespace Moonwell.Testing
{
    /// <summary>A sound the runtime asked the fake world for, and everything it set on it since.</summary>
    public sealed class FakeSound
    {
        /// <summary>The file, and whether it loops and is 3D.</summary>
        public string Path = "";

        /// <summary>Whether it loops.</summary>
        public bool Looping;

        /// <summary>Whether it is 3D.</summary>
        public bool Is3D;

        /// <summary>Its place, as last given; its unit, when put on one.</summary>
        public float X, Y, Z;

        /// <summary>The unit it was put on; null for none.</summary>
        public unit? Unit;

        /// <summary>Its volume, 0 to 127, as last set; -1 until set.</summary>
        public int Volume = -1;

        /// <summary>The volumes it was given, in order.</summary>
        public System.Collections.Generic.List<int> Volumes = new System.Collections.Generic.List<int>();

        /// <summary>Whether it was started, stopped, and left to the game to free when done.</summary>
        public bool Started, Stopped, FreedWhenDone;

        /// <summary>How many times it was started, and stopped without being freed, as a sound of the Sound Editor is.</summary>
        public int Starts, Halts;
    }
}
