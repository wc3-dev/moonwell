using System.Collections.Generic;

namespace Moonwell.Engine
{
    /// <summary>
    /// The game's 32 cells as the path search sees them, and a unit's square on them (probe 11-09): below 16 collision one
    /// cell, 16 to 31 two, 32 to 47 three, 48 and up four; with <c>c = floor(x / 32)</c> the square runs from cell
    /// <c>c - cells / 2</c> for <c>cells</c> cells, along x and y alike. A cell is open for walking where the terrain's grid
    /// says walkable and an item lies there (the item trick sees destructables and buildings, not units or pathing set by
    /// code, which the terrain's grid sees); for floating where it is water and the item lies. The item's answer is kept,
    /// as it costs a call to the game, and dropped round a change Moonwell sees: its own destructable changes, a building
    /// it takes in or forgets, a building's death, and the death of a destructable near a cell found blocked (decision 247);
    /// <c>Refresh</c> drops it too.
    /// </summary>
    internal static class Cells
    {
        /// <summary>The width of a cell.</summary>
        internal const float Size = 32f;

        /// <summary>Offset that keeps a cell's column and row positive in a key: maps are at most 1920 cells across from the centre.</summary>
        private const int Offset = 2048;

        /// <summary>How far round a change's place the item answers are dropped: half the largest destructable's texture (a bridge, 32 cells) and a cell.</summary>
        private const float ChangeReach = 544f;

        /// <summary>Where an item lies in each cell asked: true where it lies at the cell's middle.</summary>
        private static Dictionary<int, bool> items = new Dictionary<int, bool>();

        /// <summary>The place of each living building Moonwell took in, so its cells are asked again when it goes.</summary>
        private static Dictionary<unit, Vector2> buildings = new Dictionary<unit, Vector2>();

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

        /// <summary>Drops the item answers round a place where something that blocks paths changed (decision 247).</summary>
        internal static void Changed(float x, float y)
        {
            Refresh(Bounds.FromCorners(new Vector2(x - ChangeReach, y - ChangeReach), new Vector2(x + ChangeReach, y + ChangeReach)));
        }

        /// <summary>A unit Moonwell took in or forgot: a living building is kept with its place when it comes, and its cells asked again when it comes and when it goes.</summary>
        internal static void UnitChanged(unit handle)
        {
            if (buildings.TryGetValue(handle, out var place))
            {
                buildings.Remove(handle);
                Changed(place.X, place.Y);
                return;
            }

            if (Paths.World.IsLiveBuilding(handle))
            {
                Paths.World.PlaceOf(handle, out var x, out var y);
                buildings[handle] = new Vector2(x, y);
                Changed(x, y);
            }
        }

        /// <summary>Drops every item answer and building, for a new install.</summary>
        internal static void Reset()
        {
            items = new Dictionary<int, bool>();
            buildings = new Dictionary<unit, Vector2>();
        }

        private static bool ItemLies(int col, int row)
        {
            var key = Key(col, row);
            if (!items.TryGetValue(key, out var lies))
            {
                lies = Terrain.ItemLands(Middle(col), Middle(row));
                items[key] = lies;
                if (!lies)
                {
                    Paths.World.WatchDeathsAround(Middle(col), Middle(row));
                }
            }

            return lies;
        }
    }
}
