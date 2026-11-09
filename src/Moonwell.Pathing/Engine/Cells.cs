using System.Collections.Generic;

namespace Moonwell.Engine
{
    /// <summary>
    /// The game's 32 cells as the path search sees them, and a unit's square on them (probe 11-09): below 16 collision one
    /// cell, 16 to 31 two, 32 to 47 three, 48 and up four; with <c>c = floor(x / 32)</c> the square runs from cell
    /// <c>c - cells / 2</c> for <c>cells</c> cells, along x and y alike. A cell is open for walking where the terrain's grid
    /// says walkable and an item lies there (the item trick sees destructables and buildings, not units or pathing set by
    /// code, which the terrain's grid sees); for floating where it is water and the item lies. The item's answer is kept,
    /// as it costs a call to the game; <c>Refresh</c> drops it.
    /// </summary>
    internal static class Cells
    {
        /// <summary>The width of a cell.</summary>
        internal const float Size = 32f;

        /// <summary>Offset that keeps a cell's column and row positive in a key: maps are at most 1920 cells across from the centre.</summary>
        private const int Offset = 2048;

        /// <summary>Where an item lies in each cell asked: true where it lies at the cell's middle.</summary>
        private static Dictionary<int, bool> items = new Dictionary<int, bool>();

        /// <summary>The cells across a unit's square for a collision size.</summary>
        internal static int Across(float collision)
        {
            return collision < 16f ? 1 : collision < 32f ? 2 : collision < 48f ? 3 : 4;
        }

        /// <summary>The cell column or row that holds a coordinate.</summary>
        internal static int Of(float coordinate)
        {
            return (int)System.MathF.Floor(coordinate / Size);
        }

        /// <summary>The middle of a cell column or row.</summary>
        internal static float Middle(int cell)
        {
            return cell * Size + Size / 2f;
        }

        /// <summary>A cell's key.</summary>
        internal static int Key(int col, int row)
        {
            return (col + Offset) * (Offset * 2) + row + Offset;
        }

        /// <summary>The column of a key.</summary>
        internal static int ColOf(int key)
        {
            return key / (Offset * 2) - Offset;
        }

        /// <summary>The row of a key.</summary>
        internal static int RowOf(int key)
        {
            return key % (Offset * 2) - Offset;
        }

        /// <summary>Whether a square of so many cells, placed by the cell (col, row) its centre is in, is open for a movement: every cell under it is.</summary>
        internal static bool Fits(int col, int row, int cells, Movement movement)
        {
            var first = cells / 2;
            for (var i = col - first; i < col - first + cells; i++)
            {
                for (var j = row - first; j < row - first + cells; j++)
                {
                    if (!Open(i, j, movement))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Whether one cell is open for a movement, inside the playable area.</summary>
        internal static bool Open(int col, int row, Movement movement)
        {
            var x = Middle(col);
            var y = Middle(row);
            var playable = Terrain.Playable;
            if (x < playable.Min.X || x > playable.Max.X || y < playable.Min.Y || y > playable.Max.Y)
            {
                return false;
            }

            var walk = movement != Movement.Float && Terrain.IsWalkable(x, y);
            var swim = movement != Movement.Ground && Terrain.IsWater(x, y);
            return (walk || swim) && ItemLies(col, row);
        }

        /// <summary>Drops the item answers of every cell an area touches, so the next search asks the game again.</summary>
        internal static void Refresh(Bounds area)
        {
            for (var col = Of(area.Min.X); col <= Of(area.Max.X); col++)
            {
                for (var row = Of(area.Min.Y); row <= Of(area.Max.Y); row++)
                {
                    items.Remove(Key(col, row));
                }
            }
        }

        /// <summary>Drops every item answer, for a new install.</summary>
        internal static void Reset()
        {
            items = new Dictionary<int, bool>();
        }

        private static bool ItemLies(int col, int row)
        {
            var key = Key(col, row);
            if (!items.TryGetValue(key, out var lies))
            {
                lies = Terrain.ItemLands(Middle(col), Middle(row));
                items[key] = lies;
            }

            return lies;
        }
    }
}
