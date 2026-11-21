using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// The placed destructables Destructable.Named finds, each matched to its handle once, at the start, before any code can
    /// remove one: the nearest of its type to its war3map.doo place, as the game moves a destructable from there by its
    /// pathing texture (game facts, 10-27 and 11-08). A destructable removed later stays matched, so Named gives null for it
    /// and never a neighbour of its type.
    /// </summary>
    internal static class PlacedDestructables
    {
        /// <summary>How far from its place a placed destructable is looked for, on each axis: a rect of 32 found nothing in the game, one of 512 found it (game facts, 10-26).</summary>
        private const float Reach = 512.0f;

        /// <summary>The side of the squares the destructables are sorted into for the match.</summary>
        private const float Cell = 64.0f;

        /// <summary>How many squares from its own the match looks.</summary>
        private const int Rings = 9;

        private static Dictionary<string, destructable>? found;

        internal static void Reset()
        {
            found = null;
        }

        /// <summary>The handle matched to a placed destructable's name; null for a name the build wrote none of, or none was near.</summary>
        internal static destructable? Find(string name)
        {
            if (found == null)
            {
                Match();
            }

            return found!.TryGetValue(name, out var handle) ? handle : null;
        }

        /// <summary>Matches each placed destructable the build wrote, in its order, to the nearest of its type not yet taken; Runtime.Begin runs it before the setup.</summary>
        internal static void Match()
        {
            found = new Dictionary<string, destructable>();
            var world = Runtime.World;
            var names = new List<string>();
            var types = new List<int>();
            var xs = new List<float>();
            var ys = new List<float>();
            world.ReadPlacedDestructables(names, types, xs, ys);
            if (names.Count == 0)
            {
                return;
            }

            var wanted = new HashSet<int>(types);
            var squares = new Dictionary<int, List<destructable>>();
            var places = new Dictionary<destructable, float[]>();
            var kinds = new Dictionary<destructable, int>();
            foreach (var handle in world.DestructablesInWorld())
            {
                var kind = world.DestructableTypeId(handle);
                if (!wanted.Contains(kind))
                {
                    continue;
                }

                kinds[handle] = kind;

                var x = world.DestructableX(handle);
                var y = world.DestructableY(handle);
                places[handle] = new[] { x, y };
                var key = Key(Square(x), Square(y));
                if (!squares.TryGetValue(key, out var list))
                {
                    list = new List<destructable>();
                    squares[key] = list;
                }

                list.Add(handle);
            }

            var taken = new HashSet<destructable>();
            for (var i = 0; i < names.Count; i++)
            {
                var nearest = Nearest(squares, places, kinds, taken, types[i], xs[i], ys[i]);
                if (nearest != null)
                {
                    taken.Add(nearest);
                    found[names[i]] = nearest;
                }
            }
        }

        // The nearest destructable of the type within Reach of the place, not taken; the first listed of equals. The squares are
        // looked at in rings, stopping once the next ring can hold none nearer.
        private static destructable? Nearest(Dictionary<int, List<destructable>> squares, Dictionary<destructable, float[]> places, Dictionary<destructable, int> kinds, HashSet<destructable> taken, int type, float x, float y)
        {
            var column = Square(x);
            var row = Square(y);
            destructable? best = null;
            var bestDistance = 0.0f;
            for (var ring = 0; ring <= Rings; ring++)
            {
                for (var c = column - ring; c <= column + ring; c++)
                {
                    for (var r = row - ring; r <= row + ring; r++)
                    {
                        if (Math.Abs(c - column) != ring && Math.Abs(r - row) != ring)
                        {
                            continue;
                        }

                        if (!squares.TryGetValue(Key(c, r), out var list))
                        {
                            continue;
                        }

                        for (var k = 0; k < list.Count; k++)
                        {
                            var candidate = list[k];
                            var place = places[candidate];
                            var dx = place[0] - x;
                            var dy = place[1] - y;
                            if (taken.Contains(candidate) || kinds[candidate] != type || Math.Abs(dx) > Reach || Math.Abs(dy) > Reach)
                            {
                                continue;
                            }

                            var distance = dx * dx + dy * dy;
                            if (best == null || distance < bestDistance)
                            {
                                best = candidate;
                                bestDistance = distance;
                            }
                        }
                    }
                }

                // Every place in the next ring is more than ring * Cell away on an axis.
                if (best != null && bestDistance <= ring * Cell * (ring * Cell))
                {
                    break;
                }
            }

            return best;
        }

        private static int Square(float value)
        {
            return (int)MathF.Floor(value / Cell);
        }

        // A map is at most 256 by 256 tiles of 128, so a square's column and row stay within 1024 of 0.
        private static int Key(int column, int row)
        {
            return (column + 1024) * 2048 + row + 1024;
        }
    }
}
