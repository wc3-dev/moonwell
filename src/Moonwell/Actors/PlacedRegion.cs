namespace Moonwell
{
    /// <summary>A region an editor placed in the map: its name and rect, from <c>Placed.Regions</c>. Where the maths package is used it converts to <c>Bounds</c>.</summary>
    public readonly struct PlacedRegion
    {
        /// <summary>Its name in the editor.</summary>
        public readonly string Name;

        /// <summary>The x of its left edge.</summary>
        public readonly float Left;

        /// <summary>The y of its bottom edge.</summary>
        public readonly float Bottom;

        /// <summary>The x of its right edge.</summary>
        public readonly float Right;

        /// <summary>The y of its top edge.</summary>
        public readonly float Top;

        /// <summary>A region with its name and rect, as the build writes it.</summary>
        [LocalSafe]
        public PlacedRegion(string name, float left, float bottom, float right, float top)
        {
            Name = name;
            Left = left;
            Bottom = bottom;
            Right = right;
            Top = top;
        }

        /// <summary>The x of its centre.</summary>
        [LocalSafe]
        public float CenterX => (Left + Right) / 2.0f;

        /// <summary>The y of its centre.</summary>
        [LocalSafe]
        public float CenterY => (Bottom + Top) / 2.0f;
    }
}
