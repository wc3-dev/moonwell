namespace Moonwell.Testing
{
    /// <summary>A frame the package asked the fake world for, and everything it set on it since.</summary>
    public sealed class FakeFrame
    {
        /// <summary>The game frame it was made under, by name, for the two roots: "ConsoleUIBackdrop" or "GameUI".</summary>
        public string Under = "";

        /// <summary>The frame type it was made by, when made by type.</summary>
        public string Type = "";

        /// <summary>The template it was made from; empty when made by type.</summary>
        public string Template = "";

        /// <summary>The frame it was made under; null for the two roots.</summary>
        public FakeFrame? Parent;

        /// <summary>Whether it is shown on this machine; true until hidden.</summary>
        public bool Shown = true;

        /// <summary>Its points, in the order given since they were last cleared.</summary>
        public System.Collections.Generic.List<FakePoint> Points = new System.Collections.Generic.List<FakePoint>();

        /// <summary>The frame it was set to cover, all points; null when none.</summary>
        public FakeFrame? Filled;

        /// <summary>Its size; 0 until set.</summary>
        public float Width, Height;

        /// <summary>Its scale; 1 until set.</summary>
        public float Scale = 1.0f;

        /// <summary>Whether it takes the mouse; true until set.</summary>
        public bool Enabled = true;

        /// <summary>Its opacity, 0 to 255; 255 until set.</summary>
        public int Alpha = 255;

        /// <summary>Its drawing level among its siblings; 0 until set.</summary>
        public int Level;

        /// <summary>How many times the package asked the game anything about it.</summary>
        public int Calls;

        /// <summary>Its text, as the game was last given it.</summary>
        public string Text = "";

        /// <summary>Its text's alignment as the game numbers it: vertical 0 top to 2 bottom, horizontal 3 left to 5 right; -1 until set.</summary>
        public int Vertical = -1, Horizontal = -1;

        /// <summary>Its texture; empty until set.</summary>
        public string Texture = "";

        /// <summary>Where a root's corners sit on the screen, set by absolute points: the bottom left at Left, Bottom and the top right at Right, Top; the 4:3 middle until the package spans it.</summary>
        public float Left, Bottom, Right = 0.8f, Top = 0.6f;
    }
}
