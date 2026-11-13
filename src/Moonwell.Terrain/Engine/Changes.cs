using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Engine
{
    /// <summary>
    /// The terrain changes a map holds, and how each is made and taken back. Pathing and tiles keep what the ground was
    /// before any change touched a cell or corner, and work out each one again from that and the changes still held, oldest
    /// first, so the newest wins. Blight can't be read back cell by cell, so ending one takes its blight off and makes every
    /// blight change still held again, oldest first. A raise is the game's crater, taken back by stopping it. A saved game
    /// that holds craters loads wrong, so they are stopped when the game is about to save and made again after; a load
    /// shows the map's own tiles, so the tiles held are painted again then.
    /// </summary>
    internal static class Changes
    {
        /// <summary>The width of a tile, between corners.</summary>
        private const float TileSize = 128f;

        /// <summary>The white tint, packed: the water as it started.</summary>
        private const int White = -1;

        /// <summary>The changes held now, in the order they were made.</summary>
        private static readonly List<TerrainChange> held = new List<TerrainChange>();

        /// <summary>A cell's four flags before any change touched it, by the cell's number.</summary>
        private static readonly Dictionary<int, int> cellsBefore = new Dictionary<int, int>();

        /// <summary>A corner's tile before any change touched it, by the corner's number.</summary>
        private static readonly Dictionary<int, int> tilesBefore = new Dictionary<int, int>();

        /// <summary>A corner's variation before any change touched it, by the corner's number.</summary>
        private static readonly Dictionary<int, int> variationsBefore = new Dictionary<int, int>();

        /// <summary>Whether the craters are out of the game for a save, to be made again.</summary>
        private static bool stopped;

        /// <summary>
        /// The game is about to write a saved game. A save that holds lasting craters loads wrong: the first load of a game
        /// drops them, and a load of a save made in a loaded game puts them on top again (game facts, 11-11). So every
        /// raise's craters are stopped, which the save then holds, and made again in the next moment, after the save is
        /// written. Moonwell's own grid is left as it is: Height reads the raise all along.
        /// </summary>
        internal static void Saving()
        {
            for (var k = 0; k < held.Count; k++)
            {
                if (held[k].Kind == TerrainChange.Raise)
                {
                    Stop(held[k]);
                }
            }

            if (!stopped)
            {
                stopped = true;
                Game.Instance.After(0f, Again);
            }
        }

        /// <summary>
        /// A saved game was loaded: Moonwell's lists are as they were when the save was written, with the craters stopped,
        /// so they are made again; a crater made at the load stays. The game shows the map's own tiles again, so every tile
        /// change held paints its corners again. Pathing set by code lasts through a load. The first raise or tile change
        /// asks for the load to be heard, so a map with neither hears none for this.
        /// </summary>
        internal static void Loaded()
        {
            Again();
            for (var k = 0; k < held.Count; k++)
            {
                if (held[k].Kind == TerrainChange.Tiles)
                {
                    EachCorner(held[k]);
                }
            }
        }

        /// <summary>Makes the craters stopped for a save again, once: after the save in the game that goes on, and at the load in one loaded from it, whichever comes first there. A raise made since the save has its craters and is left.</summary>
        private static void Again()
        {
            if (!stopped)
            {
                return;
            }

            stopped = false;
            for (var k = 0; k < held.Count; k++)
            {
                if (held[k].Kind == TerrainChange.Raise && held[k].Craters == null)
                {
                    Craters(held[k]);
                }
            }
        }

        /// <summary>Forgets every change, at install.</summary>
        internal static void Reset()
        {
            held.Clear();
            cellsBefore.Clear();
            tilesBefore.Clear();
            variationsBefore.Clear();
            stopped = false;
        }

        /// <summary>Makes a change in a scope, unless the scope has ended, and gives it.</summary>
        internal static TerrainChange Make(TerrainChange change, Scope scope)
        {
            scope.Adopt(change);
            if (change.HasEnded)
            {
                return change;
            }

            change.Applied = true;
            switch (change.Kind)
            {
                case TerrainChange.Raise:
                    held.Add(change);
                    Events.Listen(GameEvent.Saving);
                    Events.Listen(GameEvent.Loaded);
                    Raise(change, 1f);
                    break;
                case TerrainChange.Paths:
                    held.Add(change);
                    EachCell(change);
                    break;
                case TerrainChange.Tiles:
                    held.Add(change);
                    Events.Listen(GameEvent.Loaded);
                    EachCorner(change);
                    break;
                case TerrainChange.Blights:
                    held.Add(change);
                    Blight(change, change.On);
                    if (!change.On && Terrain.World.IsBlighted(change.X, change.Y))
                    {
                        Dev.Report("Terrain.Cleanse", "the ground at its centre is still blighted: the game can't take off blight a building made");
                    }

                    break;
                default:
                    held.Add(change);
                    Tint(change.Value);
                    break;
            }

            return change;
        }

        /// <summary>Takes a change back, when it or its scope ends.</summary>
        internal static void End(TerrainChange change)
        {
            if (!change.Applied)
            {
                return;
            }

            change.Applied = false;
            held.Remove(change);
            if (change.Kind == TerrainChange.Raise)
            {
                Raise(change, -1f);
                return;
            }

            switch (change.Kind)
            {
                case TerrainChange.Paths:
                    EachCell(change);
                    break;
                case TerrainChange.Tiles:
                    EachCorner(change);
                    break;
                case TerrainChange.Blights:
                    if (change.On)
                    {
                        Blight(change, false);
                    }

                    for (var k = 0; k < held.Count; k++)
                    {
                        if (held[k].Kind == TerrainChange.Blights)
                        {
                            Blight(held[k], held[k].On);
                        }
                    }

                    break;
                default:
                    var tint = White;
                    for (var k = 0; k < held.Count; k++)
                    {
                        if (held[k].Kind == TerrainChange.Water)
                        {
                            tint = held[k].Value;
                        }
                    }

                    Tint(tint);
                    break;
            }
        }

        /// <summary>
        /// Asks the game for a raise's craters, and keeps them to stop them by: one for a round raise, and for a grid raise
        /// one of a tile's radius a corner, which moves the corner at its centre alone (the corners next to it are at the
        /// radius, where a crater moves nothing). A corner outside the map, or moved by 0, has none.
        /// </summary>
        private static void Craters(TerrainChange change)
        {
            var amounts = change.Amounts;
            if (amounts == null)
            {
                change.Craters = new[] { Terrain.World.Crater(change.X, change.Y, change.Radius, -change.By) };
                return;
            }

            var grid = Terrain.Grid;
            var made = new List<object>();
            for (var j = 0; j < change.Up; j++)
            {
                var row = change.FirstRow + j;
                if (row < 0 || row >= grid.Rows)
                {
                    continue;
                }

                for (var i = 0; i < change.Across; i++)
                {
                    var column = change.FirstColumn + i;
                    var by = amounts[j * change.Across + i];
                    if (by != 0f && column >= 0 && column < grid.Columns)
                    {
                        made.Add(Terrain.World.Crater(grid.Left + column * TileSize, grid.Bottom + row * TileSize, TileSize, -by));
                    }
                }
            }

            change.Craters = made.ToArray();
        }

        /// <summary>Takes a raise's craters out of the game, if it holds any.</summary>
        private static void Stop(TerrainChange change)
        {
            var craters = change.Craters;
            if (craters == null)
            {
                return;
            }

            change.Craters = null;
            for (var k = 0; k < craters.Length; k++)
            {
                Terrain.World.StopCrater(craters[k]);
            }
        }

        /// <summary>Makes a raise, or takes it back with the sign -1: the game's crater made or stopped, and the same at every corner of the grid inside the radius.</summary>
        private static void Raise(TerrainChange change, float sign)
        {
            if (sign > 0f)
            {
                Craters(change);
            }
            else
            {
                Stop(change);
            }

            if (change.Amounts != null)
            {
                RaiseEach(change, change.Amounts, sign);
                return;
            }

            var by = change.By * sign;
            var grid = Terrain.Grid;
            Corners(change.MinX, change.MaxX, grid.Left, grid.Columns, out var i0, out var i1);
            Corners(change.MinY, change.MaxY, grid.Bottom, grid.Rows, out var j0, out var j1);
            for (var j = j0; j <= j1; j++)
            {
                for (var i = i0; i <= i1; i++)
                {
                    var dx = grid.Left + i * TileSize - change.X;
                    var dy = grid.Bottom + j * TileSize - change.Y;
                    var d = Maths.Sqrt(dx * dx + dy * dy);
                    if (d < change.Radius)
                    {
                        grid.Raise(i, j, by * Angles.Cos(90f * d / change.Radius));
                    }
                }
            }
        }

        /// <summary>Moves the grid's corners by a grid raise's amounts, or back with the sign -1. A corner outside the map, or moved by 0, is left out.</summary>
        private static void RaiseEach(TerrainChange change, float[] amounts, float sign)
        {
            var grid = Terrain.Grid;
            for (var j = 0; j < change.Up; j++)
            {
                var row = change.FirstRow + j;
                if (row < 0 || row >= grid.Rows)
                {
                    continue;
                }

                for (var i = 0; i < change.Across; i++)
                {
                    var column = change.FirstColumn + i;
                    var by = amounts[j * change.Across + i] * sign;
                    if (by == 0f || column < 0 || column >= grid.Columns)
                    {
                        continue;
                    }

                    grid.Raise(column, row, by);
                }
            }
        }

        /// <summary>The first and last corner of a grid's columns or rows from min to max, kept inside the grid (an area outside it gives an edge corner, which the area then does not hold).</summary>
        private static void Corners(float min, float max, float origin, int count, out int first, out int last)
        {
            var low = Kept((min - origin) / TileSize, count - 1);
            first = Slots.Whole(low);
            if (first < low)
            {
                first++;
            }

            last = Slots.Whole(Kept((max - origin) / TileSize, count - 1));
        }

        /// <summary>A position in tiles, kept from 0 to the last corner; a NaN is kept as 0.</summary>
        private static float Kept(float tiles, float last)
        {
            if (!(tiles > 0f))
            {
                return 0f;
            }

            return tiles < last ? tiles : last;
        }

        /// <summary>Works out every cell whose centre a pathing change holds, from what it was before and the changes held, after the change was made or ended.</summary>
        private static void EachCell(TerrainChange change)
        {
            var paths = Terrain.Paths;
            var c0 = paths.ColumnOf(change.MinX);
            var c1 = paths.ColumnOf(change.MaxX);
            var r0 = paths.RowOf(change.MinY);
            var r1 = paths.RowOf(change.MaxY);
            var meeting = Meeting(change);
            for (var row = r0; row <= r1; row++)
            {
                var y = paths.CentreY(row);
                for (var column = c0; column <= c1; column++)
                {
                    var x = paths.CentreX(column);
                    if (!change.Contains(x, y))
                    {
                        continue;
                    }

                    var key = row * paths.Columns + column;
                    var now = paths.Flags(x, y);
                    if (!cellsBefore.TryGetValue(key, out var flags))
                    {
                        flags = now;
                        cellsBefore[key] = now;
                    }

                    var touched = false;
                    for (var k = 0; k < meeting.Count; k++)
                    {
                        var other = meeting[k];
                        if (other.Contains(x, y))
                        {
                            var kinds = other.ValueAt(x, y);
                            flags = other.On ? flags | kinds : flags & ~kinds;
                            touched = true;
                        }
                    }

                    if (!touched)
                    {
                        cellsBefore.Remove(key);
                    }

                    for (var kind = 1; kind <= 8; kind *= 2)
                    {
                        if (((now ^ flags) & kind) != 0)
                        {
                            Terrain.World.SetPathing(x, y, kind, (flags & kind) != 0);
                        }
                    }

                    paths.Store(column, row, flags);
                }
            }
        }

        /// <summary>Works out every corner a tile change holds, as cells are, after the change was made or ended; a corner already showing its tile is left as it is.</summary>
        private static void EachCorner(TerrainChange change)
        {
            var grid = Terrain.Grid;
            Corners(change.MinX, change.MaxX, grid.Left, grid.Columns, out var i0, out var i1);
            Corners(change.MinY, change.MaxY, grid.Bottom, grid.Rows, out var j0, out var j1);
            var meeting = Meeting(change);
            for (var j = j0; j <= j1; j++)
            {
                var y = grid.Bottom + j * TileSize;
                for (var i = i0; i <= i1; i++)
                {
                    var x = grid.Left + i * TileSize;
                    if (!change.Contains(x, y))
                    {
                        continue;
                    }

                    var key = j * grid.Columns + i;
                    var now = Terrain.World.TileAt(x, y);
                    if (!tilesBefore.TryGetValue(key, out var tile))
                    {
                        tile = now;
                        tilesBefore[key] = now;
                        variationsBefore[key] = Terrain.World.VariationAt(x, y);
                    }

                    var variation = variationsBefore[key];
                    var touched = false;
                    for (var k = 0; k < meeting.Count; k++)
                    {
                        var other = meeting[k];
                        if (other.Contains(x, y))
                        {
                            tile = other.ValueAt(x, y);
                            variation = -1;
                            touched = true;
                        }
                    }

                    if (!touched)
                    {
                        tilesBefore.Remove(key);
                        variationsBefore.Remove(key);
                    }

                    if (tile != now)
                    {
                        Terrain.World.PaintTile(x, y, tile, variation);
                    }
                }
            }
        }

        /// <summary>The changes held of a change's kind whose boxes meet its box, oldest first: the only ones that can hold its cells or corners, so many small changes, as procgen makes in steps, stay quick.</summary>
        private static List<TerrainChange> Meeting(TerrainChange change)
        {
            var meeting = new List<TerrainChange>();
            for (var k = 0; k < held.Count; k++)
            {
                var other = held[k];
                if (other.Kind == change.Kind && other.MinX <= change.MaxX && other.MaxX >= change.MinX && other.MinY <= change.MaxY && other.MaxY >= change.MinY)
                {
                    meeting.Add(other);
                }
            }

            return meeting;
        }

        /// <summary>Adds or takes off a blight change's blight over its circle or rect.</summary>
        private static void Blight(TerrainChange change, bool on)
        {
            if (change.Radius < 0f)
            {
                Terrain.World.Blight(change.MinX, change.MinY, change.MaxX, change.MaxY, on);
            }
            else
            {
                Terrain.World.Blight(change.X, change.Y, change.Radius, on);
            }
        }

        /// <summary>Tints the water by a packed colour.</summary>
        private static void Tint(int packed)
        {
            Terrain.World.TintWater((packed >> 24) & 255, (packed >> 16) & 255, (packed >> 8) & 255, packed & 255);
        }
    }
}
