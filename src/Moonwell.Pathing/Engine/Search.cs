using System.Collections.Generic;

namespace Moonwell.Engine
{
    /// <summary>
    /// The path search: A* over the game's 32 cells for a square of some cells, eight ways, never between two cells that
    /// touch only at a corner (the game's walkers don't, probe 11-09). Units are not in it (decision 244). The cell path is
    /// then cut to its turns: a turn is kept only where the straight line past it is not clear for the square. A target that
    /// cannot be reached gives the path to the reachable cell nearest it.
    /// </summary>
    internal sealed class Search
    {
        private const float Straight = 32f;
        private const float Diagonal = 45.254834f;

        /// <summary>The most cells the A* takes after the goal is found cut off, looking for the nearest spot.</summary>
        // shortcut: a cut-off area of more than FloodCap cells (about 1,000 by 1,000) is found only when the A* has gone over all the ground it can reach, as a map split by a river; a reach map per square size would answer at once.
        private const int NearestCap = 2048;

        /// <summary>The most cells the flood from the goal takes: a closed-in area bigger than this is not found cut off early.</summary>
        private const int FloodCap = 1024;

        private readonly Dictionary<int, bool> fits = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> open = new Dictionary<int, bool>();
        private readonly int cells;
        private readonly Movement movement;
        private readonly int startKey;

        private Search(int cells, Movement movement, int startKey)
        {
            this.cells = cells;
            this.movement = movement;
            this.startKey = startKey;
        }

        /// <summary>A path for a square of so many cells from one point to another.</summary>
        internal static Route Find(int cells, Movement movement, float fromX, float fromY, float toX, float toY)
        {
            return Find(cells, movement, fromX, fromY, toX, toY, false);
        }

        /// <summary>A path for a square of so many cells from one point to another; with walkEach, the turns found by walking every line from a turn with Clear, one after another, as before J173: the reference the band's shortcut is tested against (J181).</summary>
        internal static Route Find(int cells, Movement movement, float fromX, float fromY, float toX, float toY, bool walkEach)
        {
            var points = new List<Vector2> { new Vector2(fromX, fromY) };
            if (movement == Movement.Fly)
            {
                points.Add(new Vector2(toX, toY));
                return new Route(points, Terrain.IsFlyable(toX, toY));
            }

            var start = Cells.Key(Cells.Of(fromX), Cells.Of(fromY));
            var goal = Cells.Key(Cells.Of(toX), Cells.Of(toY));
            var search = new Search(cells, movement, start);
            var end = search.Run(goal, toX, toY, out var reached);
            var keys = new List<int>();
            for (var key = end; key != start; key = search.came[key])
            {
                keys.Add(key);
            }

            keys.Add(start);
            keys.Reverse();
            var last = reached ? new Vector2(toX, toY) : Middle(end);
            if (keys.Count == 1)
            {
                if (reached)
                {
                    points.Add(last);
                }

                return new Route(points, reached);
            }

            // A turn is kept at the cell before the first one the line from the last turn is not clear to (J173). The lines from
            // one turn are tested a chunk of cells at a time, the chunk doubling while they are clear, through Blocked.
            var anchor = points[0];
            var i = 2;
            var chunk = FirstChunk;
            while (i < keys.Count)
            {
                var to = System.Math.Min(i + chunk - 1, keys.Count - 1);
                var blocked = walkEach ? search.Walked(anchor, keys, last, i, to) : search.Blocked(anchor, keys, last, i, to);
                if (blocked < 0)
                {
                    i = to + 1;
                    chunk *= 2;
                    continue;
                }

                anchor = Middle(keys[blocked - 1]);
                points.Add(anchor);
                i = blocked + 1;
                chunk = FirstChunk;
            }

            points.Add(last);
            return new Route(points, reached);
        }

        /// <summary>The cells of the path whose lines from a turn are first tested together.</summary>
        private const int FirstChunk = 8;

        /// <summary>The widest a band round a line is let grow before its lines are tested in parts: 4 cells.</summary>
        private const float BandCap = 128f;

        /// <summary>How far a cell Clear asks can lie from its line: a step of 8 across a corner asks the two cells beside it, and 1 for rounding.</summary>
        private const float BandMargin = 9f;

        /// <summary>
        /// The first of the path's cells from one to another whose line from the anchor is not clear (Clear), or -1 when all are.
        /// The lines all lie within the band round the line to the last of them, as wide as the farthest of their ends is from
        /// it: when every cell the band touches fits, so does every cell Clear asks on them, and they are clear without walking
        /// each (J173: walking each made a route 5,000 long about 50,000 steps). Else the cells are halved and each half tested
        /// so, down to two, which are walked.
        /// </summary>
        private int Blocked(Vector2 anchor, List<int> keys, Vector2 last, int from, int to)
        {
            var end = Point(keys, last, to);
            var width = 0f;
            for (var j = from; j <= to; j++)
            {
                var distance = FromLine(Point(keys, last, j), anchor, end);
                if (distance > width || float.IsNaN(distance))
                {
                    width = distance;
                }
            }

            // A NaN point keeps the width NaN, which fails the cap, so its lines are walked.
            if (width <= BandCap && BandFits(anchor, end, width + BandMargin))
            {
                return -1;
            }

            if (to - from < 2)
            {
                return Walked(anchor, keys, last, from, to);
            }

            var half = (from + to) / 2;
            var first = Blocked(anchor, keys, last, from, half);
            return first >= 0 ? first : Blocked(anchor, keys, last, half + 1, to);
        }

        /// <summary>The first of the path's cells from one to another whose line from the anchor is not clear, walking each line with Clear; -1 when all are.</summary>
        private int Walked(Vector2 anchor, List<int> keys, Vector2 last, int from, int to)
        {
            for (var j = from; j <= to; j++)
            {
                if (!Clear(anchor, Point(keys, last, j)))
                {
                    return j;
                }
            }

            return -1;
        }

        // The point of the path at one of its cells: the cell's middle, or for the last the route's end.
        private static Vector2 Point(List<int> keys, Vector2 last, int index)
        {
            return index == keys.Count - 1 ? last : Middle(keys[index]);
        }

        // How far a point is from the line between two others.
        private static float FromLine(Vector2 point, Vector2 a, Vector2 b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var length = dx * dx + dy * dy;
            var t = length > 0f ? ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / length : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            var ex = point.X - a.X - t * dx;
            var ey = point.Y - a.Y - t * dy;
            return System.MathF.Sqrt(ex * ex + ey * ey);
        }

        /// <summary>Whether the square fits in every cell that has a point within a distance of the line between two points: a column at a time, the rows from the line's lowest to its highest in the columns' reach, widened by the distance.</summary>
        private bool BandFits(Vector2 a, Vector2 b, float distance)
        {
            var left = System.Math.Min(a.X, b.X);
            var right = System.Math.Max(a.X, b.X);
            var lastCol = Cells.Of(right + distance);
            for (var col = Cells.Of(left - distance); col <= lastCol; col++)
            {
                var x0 = System.Math.Max(col * Cells.Size - distance, left);
                var x1 = System.Math.Min(col * Cells.Size + Cells.Size + distance, right);
                var y0 = a.Y;
                var y1 = b.Y;
                if (b.X != a.X)
                {
                    y0 = a.Y + (b.Y - a.Y) * (x0 - a.X) / (b.X - a.X);
                    y1 = a.Y + (b.Y - a.Y) * (x1 - a.X) / (b.X - a.X);
                }

                var lastRow = Cells.Of(System.Math.Max(y0, y1) + distance);
                for (var row = Cells.Of(System.Math.Min(y0, y1) - distance); row <= lastRow; row++)
                {
                    if (!Fits(Cells.Key(col, row)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private readonly Dictionary<int, int> came = new Dictionary<int, int>();

        // A* from the start, with a flood from the goal a step for every two of its steps (decision 248): when the flood runs
        // out before the two meet, the goal is cut off, and the A* goes on only to NearestCap cells for the nearest spot it
        // finds. The flood stops at FloodCap cells, so a reachable goal costs the A* at most that much more (probe 3 in the
        // game: a step for each step made a route round a long wall 0.47 s where it had been 0.15 s).
        private int Run(int goal, float toX, float toY, out bool reached)
        {
            var cost = new Dictionary<int, float> { [startKey] = 0f };
            var closed = new HashSet<int>();
            var heap = new Heap();
            heap.Push(startKey, Estimate(startKey, goal));
            var best = startKey;
            var bestDistance = float.MaxValue;
            reached = false;
            var flooded = new HashSet<int> { goal };
            var flood = new List<int> { goal };
            var floodNext = 0;
            var cutOff = !Fits(goal);
            var settled = cutOff;
            var afterCutOff = 0;
            while (heap.Count > 0)
            {
                var key = heap.Pop();
                if (!closed.Add(key))
                {
                    continue;
                }

                if (key == goal)
                {
                    reached = true;
                    return key;
                }

                if (!settled && closed.Count % 2 == 0)
                {
                    if (flooded.Contains(key) || floodNext >= FloodCap)
                    {
                        settled = true;
                    }
                    else if (floodNext < flood.Count)
                    {
                        var floodKey = flood[floodNext++];
                        if (closed.Contains(floodKey))
                        {
                            settled = true;
                        }
                        else
                        {
                            Spread(floodKey, flooded, flood);
                        }
                    }
                    else
                    {
                        settled = true;
                        cutOff = true;
                    }
                }

                if (cutOff && ++afterCutOff > NearestCap)
                {
                    break;
                }

                var col = Cells.ColOf(key);
                var row = Cells.RowOf(key);
                var dx = Cells.Middle(col) - toX;
                var dy = Cells.Middle(row) - toY;
                var distance = dx * dx + dy * dy;
                if (distance < bestDistance && (key != startKey || Fits(key)))
                {
                    bestDistance = distance;
                    best = key;
                }

                // The cells beside it, asked once: a step across a corner needs the two of them it passes (J173).
                var here = cost[key];
                var west = Fits(Cells.Key(col - 1, row));
                var east = Fits(Cells.Key(col + 1, row));
                var south = Fits(Cells.Key(col, row - 1));
                var north = Fits(Cells.Key(col, row + 1));
                for (var i = -1; i <= 1; i++)
                {
                    for (var j = -1; j <= 1; j++)
                    {
                        if (i == 0 && j == 0)
                        {
                            continue;
                        }

                        var next = Cells.Key(col + i, row + j);
                        if (closed.Contains(next))
                        {
                            continue;
                        }

                        var diagonal = i != 0 && j != 0;
                        var beside = i < 0 ? west : east;
                        var above = j < 0 ? south : north;
                        var fits = diagonal ? Fits(next) && beside && above : i != 0 ? beside : above;
                        if (!fits)
                        {
                            continue;
                        }

                        var g = here + (diagonal ? Diagonal : Straight);
                        if (!cost.TryGetValue(next, out var old) || g < old)
                        {
                            cost[next] = g;
                            came[next] = key;
                            heap.Push(next, g + Estimate(next, goal));
                        }
                    }
                }
            }

            return best;
        }

        // Adds the cells a square can step to from a cell, by the same rules as the A*, to the flood.
        private void Spread(int key, HashSet<int> flooded, List<int> flood)
        {
            var col = Cells.ColOf(key);
            var row = Cells.RowOf(key);
            for (var i = -1; i <= 1; i++)
            {
                for (var j = -1; j <= 1; j++)
                {
                    var next = Cells.Key(col + i, row + j);
                    if ((i == 0 && j == 0) || flooded.Contains(next) || !Fits(next))
                    {
                        continue;
                    }

                    if (i != 0 && j != 0 && (!Fits(Cells.Key(col + i, row)) || !Fits(Cells.Key(col, row + j))))
                    {
                        continue;
                    }

                    flooded.Add(next);
                    flood.Add(next);
                }
            }
        }

        /// <summary>Whether the square fits with its centre in a cell; the start's cell always does, as the unit is there.</summary>
        private bool Fits(int key)
        {
            if (key == startKey)
            {
                return true;
            }

            if (!fits.TryGetValue(key, out var fit))
            {
                fit = true;
                var col = Cells.ColOf(key) - cells / 2;
                var row = Cells.RowOf(key) - cells / 2;
                for (var i = col; i < col + cells && fit; i++)
                {
                    for (var j = row; j < row + cells && fit; j++)
                    {
                        fit = Open(Cells.Key(i, j));
                    }
                }

                fits[key] = fit;
            }

            return fit;
        }

        // Whether one cell is open, asked once a search: neighbouring squares share most of their cells.
        private bool Open(int key)
        {
            if (!open.TryGetValue(key, out var answer))
            {
                answer = Cells.Open(Cells.ColOf(key), Cells.RowOf(key), movement);
                open[key] = answer;
            }

            return answer;
        }

        /// <summary>Whether the square can go straight from one point to another: every cell its centre passes fits, checked every 8, and a step across a corner needs both cells beside it.</summary>
        private bool Clear(Vector2 from, Vector2 to)
        {
            var steps = (int)System.MathF.Ceiling(from.DistanceTo(to) / 8f);
            var col = Cells.Of(from.X);
            var row = Cells.Of(from.Y);
            for (var s = 1; s <= steps; s++)
            {
                var t = (float)s / steps;
                var nextCol = Cells.Of(from.X + (to.X - from.X) * t);
                var nextRow = Cells.Of(from.Y + (to.Y - from.Y) * t);
                if (nextCol == col && nextRow == row)
                {
                    continue;
                }

                if (!Fits(Cells.Key(nextCol, nextRow)))
                {
                    return false;
                }

                if (nextCol != col && nextRow != row && (!Fits(Cells.Key(nextCol, row)) || !Fits(Cells.Key(col, nextRow))))
                {
                    return false;
                }

                col = nextCol;
                row = nextRow;
            }

            return true;
        }

        /// <summary>The octile distance between two cells, which never overestimates the eight-way cost.</summary>
        private static float Estimate(int from, int to)
        {
            var dx = System.Math.Abs(Cells.ColOf(from) - Cells.ColOf(to));
            var dy = System.Math.Abs(Cells.RowOf(from) - Cells.RowOf(to));
            var low = dx < dy ? dx : dy;
            var high = dx < dy ? dy : dx;
            return low * Diagonal + (high - low) * Straight;
        }

        private static Vector2 Middle(int key)
        {
            return new Vector2(Cells.Middle(Cells.ColOf(key)), Cells.Middle(Cells.RowOf(key)));
        }

        /// <summary>
        /// A binary heap of cell keys, least score first. A key moving up or down is held aside and put once where it stops,
        /// with the same comparisons as swapping it each step, so keys come off in the same order. In Lua its lists are read
        /// and written in their slots (At, Put, Append, Drop), with no bounds check and no version: they never hold null, no
        /// foreach walks them, and every index is below the count (J173: the lists' checked reads and writes were most of a
        /// search round a wall).
        /// </summary>
        private sealed class Heap
        {
            private readonly List<int> keys = new List<int>();
            private readonly List<float> scores = new List<float>();
            private int count;

            internal int Count => count;

            internal void Push(int key, float score)
            {
                var keys = this.keys;
                var scores = this.scores;
                var i = count++;
                Append(keys, i, key);
                Append(scores, i, score);
                while (i > 0)
                {
                    var parent = (i - 1) / 2;
                    var parentScore = At(scores, parent);
                    if (parentScore <= score)
                    {
                        break;
                    }

                    Put(keys, i, At(keys, parent));
                    Put(scores, i, parentScore);
                    i = parent;
                }

                Put(keys, i, key);
                Put(scores, i, score);
            }

            internal int Pop()
            {
                var keys = this.keys;
                var scores = this.scores;
                var top = At(keys, 0);
                var last = --count;
                var key = At(keys, last);
                var score = At(scores, last);
                Drop(keys, last);
                Drop(scores, last);
                if (last == 0)
                {
                    return top;
                }

                var i = 0;
                while (true)
                {
                    var left = i * 2 + 1;
                    var least = -1;
                    var leastScore = score;
                    if (left < last && At(scores, left) < leastScore)
                    {
                        least = left;
                        leastScore = At(scores, left);
                    }

                    var right = left + 1;
                    if (right < last && At(scores, right) < leastScore)
                    {
                        least = right;
                        leastScore = At(scores, right);
                    }

                    if (least < 0)
                    {
                        break;
                    }

                    Put(keys, i, At(keys, least));
                    Put(scores, i, leastScore);
                    i = least;
                }

                Put(keys, i, key);
                Put(scores, i, score);
                return top;
            }

            // Raw: the slot of a List item in Lua. Safe: the index is below the count and the item a number.
            /// <summary>The item at an index below the count.</summary>
            /// @CSharpLua.Template = "{0}[({1}) + 1]"
            internal static T At<T>(List<T> list, int index)
            {
                return list[index];
            }

            // Raw: a write to the slot of a List item in Lua, with no version counted. Safe: the index is below the count.
            /// <summary>Sets the item at an index below the count.</summary>
            /// @CSharpLua.Template = "{0}[({1}) + 1] = {2}"
            internal static void Put<T>(List<T> list, int index, T item)
            {
                list[index] = item;
            }

            // Raw: a write to the slot after the last in Lua, with no version counted. Safe: the index is the count, so the
            // table keeps no hole.
            /// <summary>Adds an item at the end of a list whose count is index.</summary>
            /// @CSharpLua.Template = "{0}[({1}) + 1] = {2}"
            internal static void Append<T>(List<T> list, int index, T item)
            {
                list.Add(item);
            }

            // Raw: the last slot cleared in Lua, with no version counted. Safe: the index is the last, so the table keeps no hole.
            /// <summary>Takes the last item, at index, off a list.</summary>
            /// @CSharpLua.Template = "{0}[({1}) + 1] = nil"
            internal static void Drop<T>(List<T> list, int index)
            {
                list.RemoveAt(index);
            }
        }
    }
}
