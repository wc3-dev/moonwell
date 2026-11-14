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

        /// <summary>The corners across a change over a grid; 0 for a round change or a rect.</summary>
        internal readonly int Across;

        /// <summary>The corners up a change over a grid.</summary>
        internal readonly int Up;

        /// <summary>The column, in the map's grid, of a grid change's first corner; it may be outside the map.</summary>
        internal readonly int FirstColumn;

        /// <summary>The row, in the map's grid, of a grid change's first corner.</summary>
        internal readonly int FirstRow;

        /// <summary>The corners between a grid raise's craters, 1 for a crater a corner; its craters' radius is <c>Radius</c>.</summary>
        internal readonly int Spacing;

        /// <summary>How far a grid raise moves each corner, or each crater when its craters stand apart, row by row from the bottom left; null for another change.</summary>
        internal readonly float[]? Amounts;

        /// <summary>A grid change's tile or pathing kinds for each corner, row by row from the bottom left, 0 for a corner it leaves; null for another change.</summary>
        internal readonly int[]? Values;

        /// <summary>The game's craters of a raise; null while the game holds none: before it is made, while a save is written, and once it has ended.</summary>
        internal object[]? Craters;

        /// <summary>Whether the change was made in the game, so its end has something to take back.</summary>
        internal bool Applied;

        /// <summary>
        /// A change over a grid of corners 128 apart, the first at a column and row of the map's grid, whose corner 0, 0 is
        /// at (left, bottom). Its box is the 128 squares around its corners: a pathing change holds the cells whose centres
        /// are in a corner's square. A grid raise's craters stand <paramref name="spacing"/> corners apart, each of the radius.
        /// </summary>
        internal TerrainChange(int kind, float left, float bottom, int firstColumn, int firstRow, int across, int up, float[]? amounts, int[]? values, int spacing = 1, float radius = -1f)
        {
            Kind = kind;
            X = left + firstColumn * 128f;
            Y = bottom + firstRow * 128f;
            Radius = radius;
            Spacing = spacing;
            MinX = X - 64f;
            MinY = Y - 64f;
            MaxX = X + (across - 1) * spacing * 128f + 64f;
            MaxY = Y + (up - 1) * spacing * 128f + 64f;
            FirstColumn = firstColumn;
            FirstRow = firstRow;
            Across = across;
            Up = up;
            Amounts = amounts;
            Values = values;
        }

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

        /// <summary>The entry of a grid change whose 128 square holds a point, or -1 for a point outside the grid.</summary>
        internal int Index(float x, float y)
        {
            var column = (x - MinX) / 128f;
            var row = (y - MinY) / 128f;
            if (!(column >= 0f && row >= 0f))
            {
                return -1;
            }

            var i = Slots.Whole(column);
            var j = Slots.Whole(row);
            return i < Across && j < Up ? j * Across + i : -1;
        }

        /// <summary>The tile or pathing kinds the change gives a point it holds: a grid's own for each corner.</summary>
        internal int ValueAt(float x, float y)
        {
            return Across > 0 ? Values![Index(x, y)] : Value;
        }

        /// <summary>Whether a point is in the change's circle or rect, or in the square of a grid's corner that the grid changes.</summary>
        internal bool Contains(float x, float y)
        {
            if (Across > 0)
            {
                var at = Index(x, y);
                return at >= 0 && Values![at] != 0;
            }

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
