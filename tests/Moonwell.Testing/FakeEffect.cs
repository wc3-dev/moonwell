using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>A special effect the runtime asked the fake world for, and everything it set on it since.</summary>
    public sealed class FakeEffect
    {
        /// <summary>The model the game was given; empty for a player who is not to see it.</summary>
        public string Model = "";

        /// <summary>The unit it was put on; null for one at a point.</summary>
        public unit? Unit;

        /// <summary>The attachment point it was put on; empty for one at a point.</summary>
        public string Point = "";

        /// <summary>Its place, the height absolute, as the game was last given it.</summary>
        public float X, Y, Z;

        /// <summary>How many times the runtime moved it.</summary>
        public int Places;

        /// <summary>Its orientation in radians, as the game was last given it.</summary>
        public float Yaw, Pitch, Roll;

        /// <summary>Its scale; 1 until set.</summary>
        public float Scale = 1.0f;

        /// <summary>Its animation speed; 1 until set.</summary>
        public float Speed = 1.0f;

        /// <summary>The animations it was given, in order; a queued one reads "queue " and its name, one by kind "type " and the game's number.</summary>
        public List<string> Animations = new List<string>();

        /// <summary>The sub-animations it has, by the game's numbers, in the order added.</summary>
        public List<int> SubAnimations = new List<int>();

        /// <summary>Its red, green, blue and alpha; null until tinted.</summary>
        public int[]? Tint;

        /// <summary>The player whose colour its team-coloured parts are drawn in; null until set.</summary>
        public player? TeamColor;

        /// <summary>How many times the runtime ended it.</summary>
        public int Ends;

        /// <summary>Whether the runtime ended it.</summary>
        public bool Ended;

        /// <summary>Whether it was ended with its death (DestroyEffect), not at once.</summary>
        public bool Death;
    }
}
