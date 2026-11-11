using System.Collections.Generic;

namespace Moonwell.Engine
{
    /// <summary>
    /// The terrain changes a map holds, and how each is made and taken back. Pathing and tiles keep what the ground was
    /// before any change touched a cell or corner, and work out each one again from that and the changes still held, oldest
    /// first, so the newest wins. Blight can't be read back cell by cell, so ending one takes its blight off and makes every
    /// blight change still held again, oldest first. A raise is the game's crater, taken back by the opposite one.
    /// </summary>
    internal static class Changes
    {
        /// <summary>The width of a tile, between corners.</summary>
        private const float TileSize = 128f;

        /// <summary>The white tint, packed: the water as it started.</summary>
        private const int White = -1;

        /// <summary>The changes held now, but raises, in the order they were made.</summary>
        private static readonly List<TerrainChange> held = new List<TerrainChange>();

        /// <summary>A cell's four flags before any change touched it, by the cell's number.</summary>
        private static readonly Dictionary<int, int> cellsBefore = new Dictionary<int, int>();

        /// <summary>A corner's tile before any change touched it, by the corner's number.</summary>
        private static readonly Dictionary<int, int> tilesBefore = new Dictionary<int, int>();

        /// <summary>A corner's variation before any change touched it, by the corner's number.</summary>
        private static readonly Dictionary<int, int> variationsBefore = new Dictionary<int, int>();

        /// <summary>Forgets every change, at install.</summary>
        internal static void Reset()
        {
            held.Clear();
            cellsBefore.Clear();
            tilesBefore.Clear();
            variationsBefore.Clear();
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
                    Raise(change, 1f);
                    break;
                case TerrainChange.Paths:
                    held.Add(change);
                    EachCell(change);
                    break;
                case TerrainChange.Tiles:
                    held.Add(change);
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
            if (change.Kind == TerrainChange.Raise)
            {
                Raise(change, -1f);
                return;
            }

            held.Remove(change);
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

        /// <summary>Makes a raise, or takes it back with the sign -1: the game's crater, and the same at every corner of the grid inside the radius.</summary>
        private static void Raise(TerrainChange change, float sign)
        {
            if (change.Amounts != null)
            {
                RaiseEach(change, change.Amounts, sign);
                return;
            }

            var by = change.By * sign;
            Terrain.World.Crater(change.X, change.Y, change.Radius, -by);
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

        /// <summary>
        /// Makes a grid raise, or takes it back with the sign -1: a crater a corner, of one tile's radius, which moves the
        /// corner at its centre alone (the corners next to it are at the radius, where a crater moves nothing), and the
        /// same in the grid. A corner outside the map, or moved by 0, is left out.
        /// </summary>
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

                var y = grid.Bottom + row * TileSize;
                for (var i = 0; i < change.Across; i++)
                {
                    var column = change.FirstColumn + i;
                    var by = amounts[j * change.Across + i] * sign;
                    if (by == 0f || column < 0 || column >= grid.Columns)
                    {
                        continue;
                    }

                    Terrain.World.Crater(grid.Left + column * TileSize, y, TileSize, -by);
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
                    for (var k = 0; k < held.Count; k++)
                    {
                        var other = held[k];
                        if (other.Kind == TerrainChange.Paths && other.Contains(x, y))
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
                    for (var k = 0; k < held.Count; k++)
                    {
                        var other = held[k];
                        if (other.Kind == TerrainChange.Tiles && other.Contains(x, y))
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
