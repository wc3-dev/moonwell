using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
    public static class Paths
    {
        /// <summary>The farthest from the point Spots looks, in cells.</summary>
        private const int SpotRings = 16;

        private static IPathingWorld? world;

        /// <summary>Each unit type's collision size, by its id, as UnitTypes took them in.</summary>
        private static Dictionary<int, float> typeSizes = new Dictionary<int, float>();

        /// <summary>Each unit type's movement, by its id, as UnitTypes took them in.</summary>
        private static Dictionary<int, Movement> typeMovements = new Dictionary<int, Movement>();

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

        /// <summary>
        /// Takes in the unit types' collision sizes and movements the build read from the map's object data, for
        /// <c>CollisionOf</c>, <c>MovementOf</c> and <c>FindPathFor</c>: one entry for each type, the entries split by commas,
        /// each the type's four-character id, its movement's letter (g ground, f float, a amphibious, y fly) and its collision
        /// size as invariant text. The generated Main calls it after Install, only in a map whose code asks a type's size.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void UnitTypes(string table)
        {
            var start = 0;
            while (start < table.Length)
            {
                // The id is the first four characters, so a comma in an id cannot end the entry.
                var end = table.IndexOf(',', start + 5);
                if (end < 0)
                {
                    end = table.Length;
                }

                var id = ObjectTypes.FourCC(table.Substring(start, 4));
                var letter = table[start + 4];
                typeMovements[id] = letter == 'y' ? Movement.Fly : letter == 'f' ? Movement.Float : letter == 'a' ? Movement.Amphibious : Movement.Ground;
                typeSizes[id] = float.Parse(table.Substring(start + 5, end - start - 5), CultureInfo.InvariantCulture);
                start = end + 1;
            }
        }

        /// <summary>
        /// Takes in the building types' placement the build read from the map's object data, for <c>CanBuild</c>,
        /// <c>BuildSpots</c> and <c>BuildPlace</c>: each type's id, its pathing texture's cells and the ground they must and must
        /// not have. The generated Main calls it after Install, only in a map whose code asks where a building fits.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Buildings(string table)
        {
            Placement.Read(table);
        }

        /// <summary>Installs the package on a world; tests give a fake one.</summary>
        internal static void Install(IPathingWorld host)
        {
#if !MOONWELL_LUA
            host = Worlds.Wrap(host);
#endif
            world = host;
            typeSizes = new Dictionary<int, float>();
            typeMovements = new Dictionary<int, Movement>();
            Cells.Reset();
            Placement.Reset();
            host.Listen(Cells.Changed);
            PathingHook.Changed = Cells.Changed;
            PathingHook.UnitChanged = Cells.UnitChanged;
        }

        internal static IPathingWorld World => world ?? throw new InvalidOperationException("Moonwell.Pathing is not installed: the map's generated Main installs it when the map uses the package.");

        /// <summary>Whether something of a collision size can stand at a point: the game would leave it exactly there. Units standing there count. It moves a hidden helper, so it changes game state.</summary>
        [ChangesState]
        public static bool CanStand(float size, float x, float y, Movement movement = Movement.Ground)
        {
            return CanStand(size, x, y, movement, null);
        }

        // As the public CanStand, with a unit whose own square does not count: a unit's own call.
        internal static bool CanStand(float size, float x, float y, Movement movement, unit? leftOut)
        {
            if (movement == Movement.Fly)
            {
                return Terrain.IsFlyable(x, y);
            }

            World.Try(Cells.Across(size), movement, x, y, leftOut, out var landedX, out var landedY);
            return Math.Abs(landedX - x) < 0.01f && Math.Abs(landedY - y) < 0.01f;
        }

        /// <summary>The spot nearest a point where something of a collision size can stand, as the game would put it there: the point itself when it fits, else the middle of a nearby cell. A flier's is the point itself.</summary>
        [ChangesState]
        public static Vector2 NearestSpot(float size, float x, float y, Movement movement = Movement.Ground)
        {
            return NearestSpot(size, x, y, movement, null);
        }

        // As the public NearestSpot, with a unit whose own square does not count.
        internal static Vector2 NearestSpot(float size, float x, float y, Movement movement, unit? leftOut)
        {
            if (movement == Movement.Fly)
            {
                return new Vector2(x, y);
            }

            World.Try(Cells.Across(size), movement, x, y, leftOut, out var landedX, out var landedY);
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

        /// <summary>
        /// A path a unit of a type can walk from one point to another, by the type's collision size and movement as the map's
        /// object data gives them: the path a unit of it would get from <c>unit.FindPath</c>, with no unit made. Units are not
        /// in it; see Route.
        /// </summary>
        /// <exception cref="ArgumentException">The map's object data has no unit type of that id.</exception>
        [ChangesState]
        public static Route FindPathFor(int unitTypeId, float fromX, float fromY, float toX, float toY)
        {
            return FindPath(CollisionOf(unitTypeId), fromX, fromY, toX, toY, MovementOf(unitTypeId));
        }

        /// <summary>A unit type's collision size, as the map's object data gives it (the standard unit's, the map's own change of it, or the map's new type's), with no unit of it made. A unit's own collision can change in the game; this is its type's.</summary>
        /// <exception cref="ArgumentException">The map's object data has no unit type of that id.</exception>
        [LocalSafe]
        public static float CollisionOf(int unitTypeId)
        {
            if (!typeSizes.TryGetValue(unitTypeId, out var size))
            {
                throw NoType(unitTypeId);
            }

            return size;
        }

        /// <summary>How units of a type get about, from the map's object data: foot, horse, hover and none walk the ground, float floats, amphibious does either, fly flies.</summary>
        /// <exception cref="ArgumentException">The map's object data has no unit type of that id.</exception>
        [LocalSafe]
        public static Movement MovementOf(int unitTypeId)
        {
            if (!typeMovements.TryGetValue(unitTypeId, out var movement))
            {
                throw NoType(unitTypeId);
            }

            return movement;
        }

        private static ArgumentException NoType(int unitTypeId)
        {
            return new ArgumentException("the map's object data has no unit type " + unitTypeId + ", or the build wrote no unit types (it writes them for a map whose code calls Paths.CollisionOf, MovementOf or FindPathFor)", nameof(unitTypeId));
        }

        /// <summary>
        /// Whether a building of a type can be laid down at a point, as the game's placement answers a worker's build order
        /// (game facts 11-28): every cell of its pathing texture inside the playable area, on ground its object data allows
        /// (buildable for most, blighted for those that need blight), clear of other buildings' cells (their unbuildable ring
        /// too), trees and other destructables, and, for a town hall that takes gold in, 768 or more from every gold mine.
        /// The point is moved onto the building's grid first, as the game does (see BuildPlace). Units count only with a
        /// builder's player: then every ground unit on its cells refuses it but that player's own unpaused ones, which the game
        /// pushes aside when the building is laid down. Without a player no unit counts. The player's gold, its tech and
        /// whether a worker can reach the place are not asked.
        /// </summary>
        /// <exception cref="ArgumentException">The map's object data has no building of that type.</exception>
        [ChangesState]
        public static bool CanBuild<T>(float x, float y, Player? builder = null)
            where T : Unit
        {
            return CanBuild(ObjectTypes.IdOf<T>(), x, y, builder);
        }

        /// <summary>Whether a building of a type, by its id, can be laid down at a point; as <c>CanBuild&lt;T&gt;</c>.</summary>
        /// <exception cref="ArgumentException">The map's object data has no building of that id.</exception>
        [ChangesState]
        public static bool CanBuild(int typeId, float x, float y, Player? builder = null)
        {
            var type = Placement.TypeOf(typeId);
            var placeX = Placement.Snap(x, type.Width);
            var placeY = Placement.Snap(y, type.Height);
            var around = Placement.Gather(type, placeX, placeY, placeX, placeY, builder == null ? -1 : builder.Slot);
            return Placement.Fits(type, placeX, placeY, around);
        }

        /// <summary>Where a building of a type asked at a point stands: along each axis the 64 grid toward 0, and 32 further from 0 when half its cells that way is odd, as the game moves a building and a destructable (a Farm asked at (100, -100) stands at (64, -64), a Burrow at (96, -96)).</summary>
        /// <exception cref="ArgumentException">The map's object data has no building of that type.</exception>
        [LocalSafe]
        public static Vector2 BuildPlace<T>(float x, float y)
            where T : Unit
        {
            return BuildPlace(ObjectTypes.IdOf<T>(), x, y);
        }

        /// <summary>Where a building of a type, by its id, asked at a point stands; as <c>BuildPlace&lt;T&gt;</c>.</summary>
        /// <exception cref="ArgumentException">The map's object data has no building of that id.</exception>
        [LocalSafe]
        public static Vector2 BuildPlace(int typeId, float x, float y)
        {
            var type = Placement.TypeOf(typeId);
            return new Vector2(Placement.Snap(x, type.Width), Placement.Snap(y, type.Height));
        }

        /// <summary>
        /// Up to count places nearest a point where buildings of a type can be laid down, nearest first, as Spots is for units:
        /// each on the building's grid, each answering CanBuild, and none on another's cells, out to 16 steps of the grid (1,024)
        /// each way. Fewer come back when fewer fit. Units count as for CanBuild.
        /// </summary>
        /// <exception cref="ArgumentException">The map's object data has no building of that type.</exception>
        [ChangesState]
        public static List<Vector2> BuildSpots<T>(float x, float y, int count, Player? builder = null)
            where T : Unit
        {
            return BuildSpots(ObjectTypes.IdOf<T>(), x, y, count, builder);
        }

        /// <summary>Up to count places nearest a point where buildings of a type, by its id, can be laid down; as <c>BuildSpots&lt;T&gt;</c>.</summary>
        /// <exception cref="ArgumentException">The map's object data has no building of that id.</exception>
        [ChangesState]
        public static List<Vector2> BuildSpots(int typeId, float x, float y, int count, Player? builder = null)
        {
            var type = Placement.TypeOf(typeId);
            var spots = new List<Vector2>();
            if (count <= 0)
            {
                return spots;
            }

            var baseX = Placement.Snap(x, type.Width);
            var baseY = Placement.Snap(y, type.Height);
            var span = SpotRings * 64f;
            var around = Placement.Gather(type, baseX - span, baseY - span, baseX + span, baseY + span, builder == null ? -1 : builder.Slot);
            var candidates = new List<Vector2>();
            for (var i = -SpotRings; i <= SpotRings; i++)
            {
                for (var j = -SpotRings; j <= SpotRings; j++)
                {
                    candidates.Add(new Vector2(baseX + i * 64f, baseY + j * 64f));
                }
            }

            // Ties go lowest first, then leftmost, so .NET and the game's Lua give the same order.
            var point = new Vector2(x, y);
            candidates.Sort((a, b) =>
            {
                var order = a.DistanceSquaredTo(point).CompareTo(b.DistanceSquaredTo(point));
                return order != 0 ? order : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
            });
            foreach (var candidate in candidates)
            {
                if (spots.Count >= count)
                {
                    break;
                }

                if (Placement.Fits(type, candidate.X, candidate.Y, around))
                {
                    spots.Add(candidate);
                    around.Building(type, candidate.X, candidate.Y);
                }
            }

            return spots;
        }

        /// <summary>Asks the game again where destructables and buildings stand in an area, for a map that changed them where Moonwell did not see it. Pathing set by code is seen at once.</summary>
        public static void Refresh(Bounds area)
        {
            Cells.Refresh(area);
        }

        /// <summary>
        /// A wall walkers and ships cannot cross, from one end to the other, for as long as the scope holds it: the 32 cells
        /// along the line lose walking and floating through <c>Terrain.Block</c>, one block for each run of cells in a row or
        /// column. Fliers cross it. It is one cell thick; a slanting wall's cells touch at their corners, which no unit
        /// squeezes between (probe 11-09).
        /// </summary>
        [ChangesState]
        public static void Wall(float x1, float y1, float x2, float y2, Scope scope)
        {
            var col1 = Cells.Of(x1);
            var row1 = Cells.Of(y1);
            var cols = Cells.Of(x2) - col1;
            var rows = Cells.Of(y2) - row1;
            var across = Math.Abs(cols) >= Math.Abs(rows);
            var steps = Math.Max(Math.Abs(cols), Math.Abs(rows));
            var runStart = 0;
            for (var i = 1; i <= steps + 1; i++)
            {
                if (i <= steps && Minor(i, steps, across ? rows : cols) == Minor(runStart, steps, across ? rows : cols))
                {
                    continue;
                }

                // The run is cells runStart to i - 1 along the long axis, at one place along the short one.
                var a = Major(runStart, steps, across ? cols : rows);
                var b = Major(i - 1, steps, across ? cols : rows);
                var minor = Minor(runStart, steps, across ? rows : cols);
                var fromCol = across ? col1 + Math.Min(a, b) : col1 + minor;
                var toCol = across ? col1 + Math.Max(a, b) : col1 + minor;
                var fromRow = across ? row1 + minor : row1 + Math.Min(a, b);
                var toRow = across ? row1 + minor : row1 + Math.Max(a, b);
                var area = Bounds.FromCorners(new Vector2(fromCol * Cells.Size + 8f, fromRow * Cells.Size + 8f), new Vector2(toCol * Cells.Size + 24f, toRow * Cells.Size + 24f));
                Terrain.Block(area, Pathing.Walk | Pathing.Float, scope);
                runStart = i;
            }
        }

        // The cell along the long axis at a step of a line that many steps long.
        private static int Major(int step, int steps, int span)
        {
            return steps == 0 ? 0 : span * step / steps;
        }

        // The cell along the short axis at a step, rounded to the nearest.
        private static int Minor(int step, int steps, int span)
        {
            return steps == 0 ? 0 : (int)Math.Floor(span * step / (float)steps + 0.5f);
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
