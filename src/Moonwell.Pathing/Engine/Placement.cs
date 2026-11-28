using System;
using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Engine
{
    /// <summary>A building type's placement, from the build's table: its cells and the ground they must and must not have.</summary>
    internal sealed class BuildingType
    {
        /// <summary>Its cells across and up.</summary>
        internal int Width;

        /// <summary>Its cells up.</summary>
        internal int Height;

        /// <summary>Which cells of its rectangle it takes, row by row from the lowest; null when it takes them all.</summary>
        internal bool[]? Cells;

        /// <summary>The pathing bits none of its cells may have (Placement's Blighted to Unfloatable).</summary>
        internal int Avoid;

        /// <summary>The pathing bits each of its cells must have.</summary>
        internal int Need;

        /// <summary>Whether it takes gold in, and so is kept from gold mines.</summary>
        internal bool TakesGold;

        /// <summary>Whether it is a gold mine.</summary>
        internal bool IsMine;

        internal bool Takes(int col, int row)
        {
            return Cells == null || Cells[row * Width + col];
        }
    }

    /// <summary>
    /// Where the game lets a building be laid down (game facts 11-28): its place moved onto the destructables' grid, every
    /// cell of its pathing texture inside the playable area, on ground its data allows (buildable where it must not be
    /// unbuildable, blighted at every cell where it needs blight), on no cell of another building or of a tree or other
    /// destructable (the item trick, which sees those), and, for a building that takes gold in, 768 or more from every gold
    /// mine. Units count when a builder's player is given: every ground unit's square on its cells but that player's own
    /// unpaused ones, which step aside when it is laid down. A footprint's cells are centred on its place: a farm at (0, 0)
    /// takes x and y -64 to 64.
    /// </summary>
    internal static class Placement
    {
        internal const int Blighted = 1;
        internal const int Unbuildable = 2;
        internal const int Unflyable = 4;
        internal const int Unwalkable = 8;
        internal const int Unamphibious = 16;
        internal const int Unfloatable = 32;

        /// <summary>How near a gold mine a building that takes gold in may not be: centre to centre, the nearest taken (probe 11-28).</summary>
        internal const float MineDistance = 768f;

        /// <summary>The farthest round a point Moonwell looks for units whose squares reach it: half of four cells.</summary>
        private const float UnitReach = 64f;

        private static Dictionary<int, BuildingType>? types;

        /// <summary>The largest half side of any building, so the buildings whose cells reach a place are found.</summary>
        private static float reach;

        /// <summary>Reads the build's table (BuildingTypes.Table in Moonwell.Build): entries split by semicolons, each the type's four-character id, its cells (<c>w x h</c>, and a colon and hex digits when it does not take them all), the bits it avoids, the bits it needs and its kind letter, split by commas.</summary>
        internal static void Read(string table)
        {
            types = new Dictionary<int, BuildingType>();
            reach = 0f;
            var shared = new Dictionary<string, BuildingType>();
            var start = 0;
            while (start + 4 < table.Length)
            {
                // The id is the first four characters, so a semicolon or comma in an id cannot end the entry.
                var end = table.IndexOf(';', start + 4);
                if (end < 0)
                {
                    end = table.Length;
                }

                var id = ObjectTypes.FourCC(table.Substring(start, 4));
                var fields = table.Substring(start + 4, end - start - 4).Split(',');
                var cells = fields[0];
                var type = new BuildingType { Avoid = int.Parse(fields[1]), Need = int.Parse(fields[2]), TakesGold = fields[3] == "g", IsMine = fields[3] == "m" };
                if (shared.TryGetValue(cells, out var same))
                {
                    type.Width = same.Width;
                    type.Height = same.Height;
                    type.Cells = same.Cells;
                }
                else
                {
                    ReadCells(cells, type);
                    shared[cells] = type;
                }

                types[id] = type;
                reach = Math.Max(reach, Math.Max(type.Width, type.Height) * Moonwell.Engine.Cells.Size / 2f);
                start = end + 1;
            }
        }

        /// <summary>Forgets the table, for a new install.</summary>
        internal static void Reset()
        {
            types = null;
            reach = 0f;
        }

        /// <summary>A building type's placement.</summary>
        /// <exception cref="ArgumentException">The table holds no building of that id, or the build wrote none.</exception>
        internal static BuildingType TypeOf(int typeId)
        {
            if (types == null || !types.TryGetValue(typeId, out var type))
            {
                throw new ArgumentException("the map's object data has no building type " + typeId + ", or the build wrote no building types (it writes them for a map whose code calls Paths.CanBuild, BuildSpots or BuildPlace)", nameof(typeId));
            }

            return type;
        }

        /// <summary>Where a building of so many cells along an axis stands when asked for a coordinate: the 64 grid toward 0, and 32 further from 0 when half its cells is odd.</summary>
        internal static float Snap(float coordinate, int cells)
        {
            var grid = coordinate >= 0f ? MathF.Floor(coordinate / 64f) * 64f : -MathF.Floor(-coordinate / 64f) * 64f;
            if ((cells / 2) % 2 == 1)
            {
                grid += coordinate >= 0f ? 32f : -32f;
            }

            return grid;
        }

        /// <summary>The first column of a footprint so many cells across centred on a place.</summary>
        internal static int First(float centre, int cells)
        {
            return Moonwell.Engine.Cells.Of(centre - cells * Moonwell.Engine.Cells.Size / 2f + Moonwell.Engine.Cells.Size / 2f);
        }

        /// <summary>What stands round some places: the cells of buildings and of the units that count, and the gold mines.</summary>
        internal sealed class Around
        {
            /// <summary>Blocked rectangles of cells, four numbers each: first column, first row, last column, last row; and a building's cells when it does not take its whole rectangle.</summary>
            internal readonly List<int> Rects = new List<int>();

            /// <summary>The cells of each rectangle that does not take them all, by its index in Rects / 4; null for full ones.</summary>
            internal readonly List<BuildingType?> Shapes = new List<BuildingType?>();

            /// <summary>The gold mines' places, x then y.</summary>
            internal readonly List<float> Mines = new List<float>();

            internal void Block(int fromCol, int fromRow, int toCol, int toRow, BuildingType? shape)
            {
                Rects.Add(fromCol);
                Rects.Add(fromRow);
                Rects.Add(toCol);
                Rects.Add(toRow);
                Shapes.Add(shape != null && shape.Cells != null ? shape : null);
            }

            internal void Building(BuildingType type, float x, float y)
            {
                var col = First(x, type.Width);
                var row = First(y, type.Height);
                Block(col, row, col + type.Width - 1, row + type.Height - 1, type);
            }

            internal bool Blocks(int col, int row)
            {
                for (var i = 0; i < Shapes.Count; i++)
                {
                    var fromCol = Rects[i * 4];
                    var fromRow = Rects[i * 4 + 1];
                    if (col < fromCol || row < fromRow || col > Rects[i * 4 + 2] || row > Rects[i * 4 + 3])
                    {
                        continue;
                    }

                    var shape = Shapes[i];
                    if (shape == null || shape.Takes(col - fromCol, row - fromRow))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Finds what stands round an area where buildings of a type would go: buildings whose cells may reach it, the units whose squares may, and, for a gold taker, the mines near enough to matter. Units count only with a builder's slot (0 or more).</summary>
        internal static Around Gather(BuildingType type, float minX, float minY, float maxX, float maxY, int builder)
        {
            var around = new Around();
            var standing = new List<Standing>();
            var half = Math.Max(type.Width, type.Height) * Moonwell.Engine.Cells.Size / 2f;
            var far = half + Math.Max(reach, UnitReach);
            if (type.TakesGold)
            {
                far = Math.Max(far, MineDistance);
            }

            Paths.World.UnitsIn(minX - far, minY - far, maxX + far, maxY + far, standing);
            foreach (var one in standing)
            {
                if (one.Building)
                {
                    if (types != null && types.TryGetValue(one.Type, out var other))
                    {
                        around.Building(other, one.X, one.Y);
                        if (other.IsMine)
                        {
                            around.Mines.Add(one.X);
                            around.Mines.Add(one.Y);
                        }
                    }

                    continue;
                }

                if (builder < 0 || one.Flies || (one.Owner == builder && !one.Paused))
                {
                    continue;
                }

                var cells = Moonwell.Engine.Cells.Across(one.Collision);
                var col = Moonwell.Engine.Cells.Of(one.X) - cells / 2;
                var row = Moonwell.Engine.Cells.Of(one.Y) - cells / 2;
                around.Block(col, row, col + cells - 1, row + cells - 1, null);
            }

            return around;
        }

        /// <summary>Whether a building of a type fits at a place on its grid, with what stands round it.</summary>
        internal static bool Fits(BuildingType type, float x, float y, Around around)
        {
            if (type.TakesGold)
            {
                for (var i = 0; i < around.Mines.Count; i += 2)
                {
                    var dx = around.Mines[i] - x;
                    var dy = around.Mines[i + 1] - y;
                    if (dx * dx + dy * dy < MineDistance * MineDistance)
                    {
                        return false;
                    }
                }
            }

            var firstCol = First(x, type.Width);
            var firstRow = First(y, type.Height);
            var playable = Terrain.Playable;
            if (firstCol * Moonwell.Engine.Cells.Size < playable.Min.X || firstRow * Moonwell.Engine.Cells.Size < playable.Min.Y
                || (firstCol + type.Width) * Moonwell.Engine.Cells.Size > playable.Max.X || (firstRow + type.Height) * Moonwell.Engine.Cells.Size > playable.Max.Y)
            {
                return false;
            }

            var asked = type.Avoid | type.Need;
            for (var j = 0; j < type.Height; j++)
            {
                for (var i = 0; i < type.Width; i++)
                {
                    if (!type.Takes(i, j))
                    {
                        continue;
                    }

                    var col = firstCol + i;
                    var row = firstRow + j;
                    if (around.Blocks(col, row))
                    {
                        return false;
                    }

                    var bits = Ground(col, row, asked);
                    if ((bits & type.Avoid) != 0 || (bits & type.Need) != type.Need || !Moonwell.Engine.Cells.ItemLies(col, row))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // The pathing bits a cell has, of those asked: the terrain's grid for the four kinds, and blight read at its middle.
        private static int Ground(int col, int row, int asked)
        {
            var x = Moonwell.Engine.Cells.Middle(col);
            var y = Moonwell.Engine.Cells.Middle(row);
            var bits = 0;
            if ((asked & Blighted) != 0 && Terrain.IsBlighted(x, y))
            {
                bits |= Blighted;
            }

            if ((asked & Unbuildable) != 0 && !Terrain.IsBuildable(x, y))
            {
                bits |= Unbuildable;
            }

            if ((asked & Unflyable) != 0 && !Terrain.IsFlyable(x, y))
            {
                bits |= Unflyable;
            }

            var walkable = (asked & (Unwalkable | Unamphibious)) != 0 && Terrain.IsWalkable(x, y);
            var water = (asked & (Unfloatable | Unamphibious)) != 0 && Terrain.IsWater(x, y);
            if ((asked & Unwalkable) != 0 && !walkable)
            {
                bits |= Unwalkable;
            }

            if ((asked & Unfloatable) != 0 && !water)
            {
                bits |= Unfloatable;
            }

            if ((asked & Unamphibious) != 0 && !walkable && !water)
            {
                bits |= Unamphibious;
            }

            return bits;
        }

        private static void ReadCells(string text, BuildingType type)
        {
            var colon = text.IndexOf(':');
            var size = colon < 0 ? text : text.Substring(0, colon);
            var x = size.IndexOf('x');
            type.Width = int.Parse(size.Substring(0, x));
            type.Height = int.Parse(size.Substring(x + 1));
            if (colon < 0)
            {
                return;
            }

            var cells = new bool[type.Width * type.Height];
            for (var i = 0; i < cells.Length; i++)
            {
                var digit = "0123456789abcdef".IndexOf(text[colon + 1 + i / 4]);
                cells[i] = (digit & (1 << (i % 4))) != 0;
            }

            type.Cells = cells;
        }
    }
}
