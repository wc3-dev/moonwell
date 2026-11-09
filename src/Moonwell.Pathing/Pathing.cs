using System;
using System.Collections.Generic;
using System.ComponentModel;
using Moonwell.Engine;
using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>
    /// Where units fit and how they get about, by size. A unit takes a square of the game's 32 cells by its collision size:
    /// below 16 one cell, 16 to 31 two, 32 to 47 three, 48 and up four. Fit and nearest spot are the game's own answer, through
    /// a hidden helper of that square, so they see terrain, destructables, buildings and the units standing there. Paths are
    /// Moonwell's own search on the same cells and see terrain, destructables, buildings and pathing set by code, not units
    /// (decision 244). A map's generated Main installs the package with the helper types the build declares.
    /// </summary>
    public static class Pathing
    {
        /// <summary>The farthest from the point Spots looks, in cells.</summary>
        private const int SpotRings = 16;

        private static IPathingWorld? world;

        /// <summary>Installs the package on the game with the helper types the build declares: cells 1 to 4, each for ground, float and amphibious.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Install(int[] helperTypes)
        {
            foreach (var type in helperTypes)
            {
                ObjectTypes.HelperType(type);
            }

            Install(new NativePathingWorld(helperTypes));
        }

        /// <summary>Installs the package on a world; tests give a fake one.</summary>
        internal static void Install(IPathingWorld host)
        {
            world = host;
            Cells.Reset();
        }

        internal static IPathingWorld World => world ?? throw new InvalidOperationException("Moonwell.Pathing is not installed: the map's generated Main installs it when the map uses the package.");

        /// <summary>Whether something of a collision size can stand at a point: the game would leave it exactly there. Units standing there count. It moves a hidden helper, so it changes game state.</summary>
        [ChangesState]
        public static bool CanStand(float size, float x, float y, Movement movement = Movement.Ground)
        {
            if (movement == Movement.Fly)
            {
                return Terrain.IsFlyable(x, y);
            }

            World.Try(Cells.Across(size), movement, x, y, out var landedX, out var landedY);
            return Math.Abs(landedX - x) < 0.01f && Math.Abs(landedY - y) < 0.01f;
        }

        /// <summary>The spot nearest a point where something of a collision size can stand, as the game would put it there: the point itself when it fits, else the middle of a nearby cell. A flier's is the point itself.</summary>
        [ChangesState]
        public static Vector2 NearestSpot(float size, float x, float y, Movement movement = Movement.Ground)
        {
            if (movement == Movement.Fly)
            {
                return new Vector2(x, y);
            }

            World.Try(Cells.Across(size), movement, x, y, out var landedX, out var landedY);
            return new Vector2(landedX, landedY);
        }

        /// <summary>
        /// Up to count spots around a point where things of a collision size can stand, nearest first, none of their squares
        /// overlapping another's or anything standing there: the nearest spot to the point, then the middles of the cells
        /// around it, out to 16 cells. Fewer come back when fewer fit. Fliers get the point and the cells around it.
        /// </summary>
        [ChangesState]
        public static List<Vector2> Spots(float size, float x, float y, int count, Movement movement = Movement.Ground)
        {
            var cells = Cells.Across(size);
            var spots = new List<Vector2>();
            var taken = new List<int>();
            Take(NearestSpot(size, x, y, movement), cells, movement, spots, taken, count);
            var col = Cells.Of(x);
            var row = Cells.Of(y);
            var candidates = new List<Vector2>();
            for (var i = -SpotRings; i <= SpotRings; i++)
            {
                for (var j = -SpotRings; j <= SpotRings; j++)
                {
                    candidates.Add(new Vector2(Cells.Middle(col + i), Cells.Middle(row + j)));
                }
            }

            var point = new Vector2(x, y);
            candidates.Sort((a, b) => a.DistanceSquaredTo(point).CompareTo(b.DistanceSquaredTo(point)));
            foreach (var candidate in candidates)
            {
                if (spots.Count >= count)
                {
                    break;
                }

                if (CanStand(size, candidate.X, candidate.Y, movement))
                {
                    Take(candidate, cells, movement, spots, taken, count);
                }
            }

            return spots;
        }

        /// <summary>A path a unit of a collision size can walk from one point to another. Units are not in it; see Route.</summary>
        [ChangesState]
        public static Route FindPath(float size, float fromX, float fromY, float toX, float toY, Movement movement = Movement.Ground)
        {
            return Search.Find(Cells.Across(size), movement, fromX, fromY, toX, toY);
        }

        /// <summary>Asks the game again where destructables and buildings stand in an area, for a map that changed them where Moonwell did not see it. Pathing set by code is seen at once.</summary>
        public static void Refresh(Bounds area)
        {
            Cells.Refresh(area);
        }

        // Keeps a spot when its square overlaps none taken; a square's first cell along each axis is its key's.
        private static void Take(Vector2 spot, int cells, Movement movement, List<Vector2> spots, List<int> taken, int count)
        {
            if (spots.Count >= count)
            {
                return;
            }

            var first = cells / 2;
            var col = Cells.Of(spot.X) - first;
            var row = Cells.Of(spot.Y) - first;
            if (movement != Movement.Fly)
            {
                foreach (var key in taken)
                {
                    if (Math.Abs(Cells.ColOf(key) - col) < cells && Math.Abs(Cells.RowOf(key) - row) < cells)
                    {
                        return;
                    }
                }
            }

            taken.Add(Cells.Key(col, row));
            spots.Add(spot);
        }
    }
}
