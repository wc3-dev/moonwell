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

        // shortcut: no limit on the cells searched, so a target cut off on a large open map searches all the ground it can reach; add a reach map per square size if that is ever slow.
        private readonly Dictionary<int, bool> fits = new Dictionary<int, bool>();
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

            var anchor = points[0];
            for (var i = 1; i < keys.Count; i++)
            {
                var next = i == keys.Count - 1 ? last : Middle(keys[i]);
                if (i > 1 && !search.Clear(anchor, next))
                {
                    anchor = Middle(keys[i - 1]);
                    points.Add(anchor);
                }
            }

            points.Add(last);
            return new Route(points, reached);
        }

        private readonly Dictionary<int, int> came = new Dictionary<int, int>();

        private int Run(int goal, float toX, float toY, out bool reached)
        {
            var cost = new Dictionary<int, float> { [startKey] = 0f };
            var closed = new HashSet<int>();
            var heap = new Heap();
            heap.Push(startKey, Estimate(startKey, goal));
            var best = startKey;
            var bestDistance = float.MaxValue;
            reached = false;
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

                for (var i = -1; i <= 1; i++)
                {
                    for (var j = -1; j <= 1; j++)
                    {
                        if (i == 0 && j == 0)
                        {
                            continue;
                        }

                        var next = Cells.Key(col + i, row + j);
                        if (closed.Contains(next) || !Fits(next))
                        {
                            continue;
                        }

                        var diagonal = i != 0 && j != 0;
                        if (diagonal && (!Fits(Cells.Key(col + i, row)) || !Fits(Cells.Key(col, row + j))))
                        {
                            continue;
                        }

                        var g = cost[key] + (diagonal ? Diagonal : Straight);
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

        /// <summary>Whether the square fits with its centre in a cell; the start's cell always does, as the unit is there.</summary>
        private bool Fits(int key)
        {
            if (key == startKey)
            {
                return true;
            }

            if (!fits.TryGetValue(key, out var open))
            {
                open = Cells.Fits(Cells.ColOf(key), Cells.RowOf(key), cells, movement);
                fits[key] = open;
            }

            return open;
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

        /// <summary>A binary heap of cell keys, least score first.</summary>
        private sealed class Heap
        {
            private readonly List<int> keys = new List<int>();
            private readonly List<float> scores = new List<float>();

            internal int Count => keys.Count;

            internal void Push(int key, float score)
            {
                keys.Add(key);
                scores.Add(score);
                var i = keys.Count - 1;
                while (i > 0)
                {
                    var parent = (i - 1) / 2;
                    if (scores[parent] <= scores[i])
                    {
                        break;
                    }

                    Swap(i, parent);
                    i = parent;
                }
            }

            internal int Pop()
            {
                var top = keys[0];
                var last = keys.Count - 1;
                Swap(0, last);
                keys.RemoveAt(last);
                scores.RemoveAt(last);
                var i = 0;
                while (true)
                {
                    var left = i * 2 + 1;
                    var right = left + 1;
                    var least = i;
                    if (left < keys.Count && scores[left] < scores[least])
                    {
                        least = left;
                    }

                    if (right < keys.Count && scores[right] < scores[least])
                    {
                        least = right;
                    }

                    if (least == i)
                    {
                        return top;
                    }

                    Swap(i, least);
                    i = least;
                }
            }

            private void Swap(int a, int b)
            {
                var key = keys[a];
                keys[a] = keys[b];
                keys[b] = key;
                var score = scores[a];
                scores[a] = scores[b];
                scores[b] = score;
            }
        }
    }
}
