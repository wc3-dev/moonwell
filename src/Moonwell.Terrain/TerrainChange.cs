using Moonwell.Engine;

namespace Moonwell
{
    /// <summary>
    /// A change a map made to the ground (a raise, pathing, tiles, blight or a water tint), held in a scope: when it or its
    /// scope ends, what was there before comes back. Where changes of a kind overlap, the newest still held wins, and
    /// ending one shows what is under it; raises add up, as the game's craters do.
    /// </summary>
    public sealed class TerrainChange : Scoped
    {
        internal const int Raise = 0;
        internal const int Paths = 1;
        internal const int Tiles = 2;
        internal const int Blights = 3;
        internal const int Water = 4;

        /// <summary>Which kind of change it is.</summary>
        internal readonly int Kind;

        /// <summary>The centre of a round change, or of a rect's.</summary>
        internal readonly float X;

        /// <summary>The centre's y.</summary>
        internal readonly float Y;

        /// <summary>The radius of a round change; below 0 for a rect.</summary>
        internal readonly float Radius;

        /// <summary>The box that holds the change.</summary>
        internal readonly float MinX;

        /// <summary>The box's bottom.</summary>
        internal readonly float MinY;

        /// <summary>The box's right.</summary>
        internal readonly float MaxX;

        /// <summary>The box's top.</summary>
        internal readonly float MaxY;

        /// <summary>The pathing kinds, the tile, or the tint packed as 0xRRGGBBAA.</summary>
        internal readonly int Value;

        /// <summary>Whether pathing is allowed or blight added; false for blocked or cleansed.</summary>
        internal readonly bool On;

        /// <summary>How far a raise moves the ground at its centre.</summary>
        internal readonly float By;

        /// <summary>Whether the change was made in the game, so its end has something to take back.</summary>
        internal bool Applied;

        internal TerrainChange(int kind, float x, float y, float radius, float minX, float minY, float maxX, float maxY, int value, bool on, float by)
        {
            Kind = kind;
            X = x;
            Y = y;
            Radius = radius;
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            Value = value;
            On = on;
            By = by;
        }

        /// <summary>Whether a point is in the change's circle or rect.</summary>
        internal bool Contains(float x, float y)
        {
            if (Radius < 0f)
            {
                return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
            }

            var dx = x - X;
            var dy = y - Y;
            return dx * dx + dy * dy <= Radius * Radius;
        }

        /// <summary>Takes the change back.</summary>
        protected override void OnEnd()
        {
            Changes.End(this);
        }
    }
}
