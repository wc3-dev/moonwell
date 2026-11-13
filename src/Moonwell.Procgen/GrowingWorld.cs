using System;
using System.Collections.Generic;

namespace Moonwell.Procgen
{
    /// <summary>
    /// A world of a <c>TerrainGenerator</c> made chunk by chunk as heroes come near, and each chunk taken back when every
    /// hero is far, from <c>Grow</c>. A chunk's ground is its part of <c>Plan(area)</c>: it holds its own corners and no
    /// other chunk does, so chunks meet in any order and come back the same. Chunks are made one at a time in steps, the
    /// nearest to a hero first, and taken back one a step, so no frame does more than one. Ending the scope it was grown in
    /// takes the whole world back.
    /// </summary>
    public sealed class GrowingWorld : Scope
    {
        private const float TileSize = 128f;

        /// <summary>The seconds between looks for heroes.</summary>
        private const float LookEvery = 0.25f;

        /// <summary>The seconds between steps, and the work a step does in microseconds: Make's by default (decision 285).</summary>
        private const float StepEvery = 0.02f;

        private const float StepBudget = 4000f;

        /// <summary>How near a new tree or a corner newly blocked to walking a unit's centre puts it back through the game's pathing: the corner's cells, 64 round it, and a large unit's half square.</summary>
        private const float TrapReach = 112f;

        private readonly TerrainGenerator generator;

        /// <summary>The world, as given to Grow: chunks are planned as pieces of it.</summary>
        private readonly Bounds area;

        /// <summary>The corners a chunk holds across and up.</summary>
        private readonly int size;

        /// <summary>The world's corners, and its chunks, across and up.</summary>
        private readonly int columns;

        private readonly int rows;
        private readonly int across;
        private readonly int up;

        /// <summary>The world's first corner, its lower left.</summary>
        private readonly Vector2 first;

        /// <summary>The chunk at each place of the grid (column + row * across) while it waits, is being made or is made.</summary>
        private readonly Chunk?[] grid;

        /// <summary>Whether the camps of each place of the grid are made: once, the first time its chunk is (decision 296).</summary>
        private readonly bool[] camped;

        /// <summary>The chunks of the grid, in the order they came.</summary>
        private readonly List<Chunk> live = new List<Chunk>();

        /// <summary>Where the living heroes were at the last look.</summary>
        private readonly List<Vector2> heroes = new List<Vector2>();

        /// <summary>The chunk being made, one at a time.</summary>
        private Chunk? making;

        /// <summary>The timer of the steps, while there is work.</summary>
        private Timer? stepper;

        internal GrowingWorld(TerrainGenerator generator, Bounds area, Scope scope, int size)
        {
            this.generator = generator;
            this.area = area;
            this.size = size;
            columns = (int)(area.Size.X / TileSize + 0.5f) + 1;
            rows = (int)(area.Size.Y / TileSize + 0.5f) + 1;
            across = (columns + size - 1) / size;
            up = (rows + size - 1) / size;
            first = new Vector2(Noise.Floor(area.Min.X / TileSize + 0.5f) * TileSize, Noise.Floor(area.Min.Y / TileSize + 0.5f) * TileSize);
            grid = new Chunk?[across * up];
            camped = new bool[across * up];
            scope.Adopt(this);
            Every(LookEvery, Look);
        }

        /// <summary>How near a hero comes to a chunk for it to be made, in the game's units from the chunk's edge.</summary>
        [LocalSafe]
        public float Near { get; set; } = 2048f;

        /// <summary>How far every hero goes from a made chunk for it to be taken back; kept above Near, so a hero on a chunk's edge does not make and take it back over and over.</summary>
        [LocalSafe]
        public float Far { get; set; } = 4096f;

        /// <summary>Runs once a chunk is made, after any unit its trees or blocked corners trapped is put back on free ground.</summary>
        public event Action<Chunk>? Made;

        /// <summary>Runs just before a made chunk's ground is taken back, while what is in its scope still stands.</summary>
        public event Action<Chunk>? TakingBack;

        /// <summary>Puts a unit back through the game's pathing at its own place when a new tree or a corner newly blocked to walking is in its way.</summary>
        private static void Free(TerrainPlan plan, unit handle, float x, float y)
        {
            var fromColumn = Math.Max(Noise.Floor((x - TrapReach - plan.X) / TileSize) + 1, 0);
            var toColumn = Math.Min(Noise.Floor((x + TrapReach - plan.X) / TileSize), plan.Columns - 1);
            var fromRow = Math.Max(Noise.Floor((y - TrapReach - plan.Y) / TileSize) + 1, 0);
            var toRow = Math.Min(Noise.Floor((y + TrapReach - plan.Y) / TileSize), plan.Rows - 1);
            for (var row = fromRow; row <= toRow; row++)
            {
                for (var column = fromColumn; column <= toColumn; column++)
                {
                    var at = row * plan.Columns + column;
                    if (plan.Trees[at] != 0 || (plan.Blocked[at] & Pathing.Walk) != 0)
                    {
                        Runtime.World.MoveUnit(handle, x, y);
                        return;
                    }
                }
            }
        }

        /// <summary>Finds the living heroes, marks the chunks every one is far from, drops waiting ones, and adds the chunks a hero is near.</summary>
        private void Look()
        {
            heroes.Clear();
            foreach (var handle in Runtime.World.UnitsInWorld())
            {
                if (Runtime.World.IsHero(handle) && Runtime.World.IsAlive(handle))
                {
                    heroes.Add(new Vector2(Runtime.World.UnitX(handle), Runtime.World.UnitY(handle)));
                }
            }

            for (var k = live.Count - 1; k >= 0; k--)
            {
                var chunk = live[k];
                chunk.Leaving = !IsNear(chunk, Far);
                if (chunk.Leaving && chunk.Scope == null)
                {
                    Drop(chunk);
                }
            }

            var width = size * TileSize;
            foreach (var hero in heroes)
            {
                var fromColumn = Math.Max(Noise.Floor((hero.X - Near - first.X + TileSize / 2f) / width), 0);
                var toColumn = Math.Min(Noise.Floor((hero.X + Near - first.X + TileSize / 2f) / width), across - 1);
                var fromRow = Math.Max(Noise.Floor((hero.Y - Near - first.Y + TileSize / 2f) / width), 0);
                var toRow = Math.Min(Noise.Floor((hero.Y + Near - first.Y + TileSize / 2f) / width), up - 1);
                for (var row = fromRow; row <= toRow; row++)
                {
                    for (var column = fromColumn; column <= toColumn; column++)
                    {
                        var at = row * across + column;
                        if (grid[at] == null && Within(Chunk.AreaOf(first, size, columns, rows, column, row), hero, Near))
                        {
                            var chunk = new Chunk(column, row, first, size, columns, rows);
                            grid[at] = chunk;
                            live.Add(chunk);
                        }
                    }
                }
            }

            if (stepper == null && HasWork())
            {
                stepper = Every(StepEvery, Step);
            }
        }

        /// <summary>Takes back one far chunk, or makes a step of the chunk being made, starting the nearest waiting one when none is; stops when there is nothing to do.</summary>
        private void Step()
        {
            for (var k = 0; k < live.Count; k++)
            {
                if (live[k].Leaving)
                {
                    TakeBack(live[k]);
                    return;
                }
            }

            if (making == null)
            {
                making = Nearest();
                if (making == null)
                {
                    stepper!.End();
                    stepper = null;
                    return;
                }

                var scope = new ChunkScope();
                Adopt(scope);
                making.Scope = scope;
                making.Work = generator.Steps(making.Plot, area, scope, false, out var plan);
                making.Plan = plan;
            }

            if (making.Work!.Run(StepBudget))
            {
                Finish(making);
            }
        }

        /// <summary>A chunk made: units in the way put back, its camps made the first time, then Made.</summary>
        private void Finish(Chunk chunk)
        {
            making = null;
            chunk.Work = null;
            chunk.IsMade = true;
            var plan = chunk.Plan!;
            var centre = chunk.Area.Center;
            var half = chunk.Area.Size / 2f;
            foreach (var handle in Runtime.World.UnitsInRange(centre.X, centre.Y, Maths.Sqrt(half.X * half.X + half.Y * half.Y) + TrapReach))
            {
                if (Runtime.World.IsAlive(handle) && !Runtime.World.IsHidden(handle) && !Runtime.World.CanFly(handle) && !ObjectTypes.IsHelper(Runtime.World.UnitTypeId(handle)))
                {
                    Free(plan, handle, Runtime.World.UnitX(handle), Runtime.World.UnitY(handle));
                }
            }

            var at = chunk.Row * across + chunk.Column;
            if (!camped[at])
            {
                camped[at] = true;
                plan.MakePlaces(0, plan.Places.Count, this);
            }

            try
            {
                Made?.Invoke(chunk);
            }
            catch (Exception error)
            {
                Errors.Report("GrowingWorld.Made", error);
            }
        }

        /// <summary>Takes a chunk's ground back, TakingBack first when it was made.</summary>
        private void TakeBack(Chunk chunk)
        {
            if (chunk.IsMade)
            {
                try
                {
                    TakingBack?.Invoke(chunk);
                }
                catch (Exception error)
                {
                    Errors.Report("GrowingWorld.TakingBack", error);
                }
            }

            Drop(chunk);
        }

        /// <summary>Takes a chunk off the grid and ends its scope, taking back what was made of it.</summary>
        private void Drop(Chunk chunk)
        {
            live.Remove(chunk);
            grid[chunk.Row * across + chunk.Column] = null;
            if (making == chunk)
            {
                making = null;
            }

            chunk.IsMade = false;
            chunk.Work = null;
            chunk.Scope?.End();
        }

        /// <summary>Whether a chunk waits to be made or leaves.</summary>
        private bool HasWork()
        {
            foreach (var chunk in live)
            {
                if (chunk.Leaving || chunk.Scope == null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The waiting chunk nearest a hero, the one that came first of equals; null when none waits.</summary>
        private Chunk? Nearest()
        {
            Chunk? best = null;
            var bestDistance = float.MaxValue;
            foreach (var chunk in live)
            {
                if (chunk.Scope != null)
                {
                    continue;
                }

                var distance = float.MaxValue;
                foreach (var hero in heroes)
                {
                    distance = Math.Min(distance, hero.DistanceTo(chunk.Area.Clamp(hero)));
                }

                if (best == null || distance < bestDistance)
                {
                    best = chunk;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>Whether a hero is within a distance of a chunk.</summary>
        private bool IsNear(Chunk chunk, float distance)
        {
            foreach (var hero in heroes)
            {
                if (Within(chunk.Area, hero, distance))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Within(Bounds bounds, Vector2 point, float distance)
        {
            return point.DistanceTo(bounds.Clamp(point)) <= distance;
        }

        /// <summary>The scope of a chunk's ground, ended when it is taken back.</summary>
        private sealed class ChunkScope : Scope
        {
        }
    }

    /// <summary>
    /// A chunk of a <c>GrowingWorld</c>, given to <c>Made</c> and <c>TakingBack</c>: a square of the world's corners, made
    /// when a hero comes near and taken back when every hero is far. A chunk made again is a new one, with the same ground.
    /// </summary>
    public sealed class Chunk
    {
        internal Chunk(int column, int row, Vector2 first, int size, int columns, int rows)
        {
            Column = column;
            Row = row;
            var from = new Vector2(first.X + column * size * 128f, first.Y + row * size * 128f);
            Plot = Bounds.FromCorners(from, new Vector2(first.X + (Math.Min((column + 1) * size, columns) - 1) * 128f, first.Y + (Math.Min((row + 1) * size, rows) - 1) * 128f));
            Area = AreaOf(first, size, columns, rows, column, row);
        }

        /// <summary>Its column in the world's grid of chunks, from 0 at the left.</summary>
        [LocalSafe]
        public int Column { get; }

        /// <summary>Its row, from 0 at the bottom.</summary>
        [LocalSafe]
        public int Row { get; }

        /// <summary>The ground it holds: its corners and the 64 round each, so the areas of touching chunks meet.</summary>
        [LocalSafe]
        public Bounds Area { get; }

        /// <summary>The scope its ground is held in, ended when it is taken back: what the map makes in it goes with it. Null until it starts being made.</summary>
        [LocalSafe]
        public Scope? Scope { get; internal set; }

        /// <summary>Whether its ground is made and not yet taken back.</summary>
        [LocalSafe]
        public bool IsMade { get; internal set; }

        /// <summary>From its first corner to its last, the area planned.</summary>
        internal Bounds Plot { get; }

        internal TerrainPlan? Plan { get; set; }

        internal Work? Work { get; set; }

        /// <summary>Whether every hero was far at the last look.</summary>
        internal bool Leaving { get; set; }

        /// <summary>The ground a chunk of the grid holds.</summary>
        internal static Bounds AreaOf(Vector2 first, int size, int columns, int rows, int column, int row)
        {
            var from = new Vector2(first.X + column * size * 128f - 64f, first.Y + row * size * 128f - 64f);
            var to = new Vector2(first.X + Math.Min((column + 1) * size, columns) * 128f - 64f, first.Y + Math.Min((row + 1) * size, rows) * 128f - 64f);
            return Bounds.FromCorners(from, to);
        }
    }
}
