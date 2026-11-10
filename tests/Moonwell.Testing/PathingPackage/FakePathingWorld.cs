using System;
using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>
    /// The fake game the pathing package's cases run against. A helper is put as probe 11-09 saw the game put a hidden unit:
    /// exactly where asked when its square is free, else at the middle of the nearest cell where it is, by distance. Its cells
    /// are the terrain fake's (walkable or floatable, and an item's Occupied) and the squares of the units a case stands.
    /// </summary>
    public sealed class FakePathingWorld : IPathingWorld
    {
        private readonly FakeTerrainWorld terrain;

        /// <summary>The squares of units standing, each its first column, first row and cells across.</summary>
        public List<int[]> Standing = new List<int[]>();

        /// <summary>Each unit's collision size; 31 for one not in it.</summary>
        public Dictionary<unit, float> Collisions = new Dictionary<unit, float>();

        /// <summary>Each unit's movement; Ground for one not in it.</summary>
        public Dictionary<unit, Movement> Movements = new Dictionary<unit, Movement>();

        /// <summary>The cells across and movement of the last helper put.</summary>
        public int LastCells;

        /// <summary>The movement of the last helper put.</summary>
        public Movement LastMovement;

        /// <summary>How many times a helper was put.</summary>
        public int Tries;

        public FakePathingWorld(FakeTerrainWorld terrain)
        {
            this.terrain = terrain;
        }

        /// <summary>Stands a unit's square, by collision size, at a point.</summary>
        public void Stand(float collision, float x, float y)
        {
            var cells = collision < 16f ? 1 : collision < 32f ? 2 : collision < 48f ? 3 : 4;
            Standing.Add(new[] { Cell(x) - cells / 2, Cell(y) - cells / 2, cells });
        }

        public void Try(int cells, Movement movement, float x, float y, out float landedX, out float landedY)
        {
            Tries++;
            LastCells = cells;
            LastMovement = movement;
            landedX = x;
            landedY = y;
            if (Fits(Cell(x), Cell(y), cells, movement))
            {
                return;
            }

            var best = float.MaxValue;
            for (var i = -16; i <= 16; i++)
            {
                for (var j = -16; j <= 16; j++)
                {
                    var col = Cell(x) + i;
                    var row = Cell(y) + j;
                    var mx = col * 32f + 16f;
                    var my = row * 32f + 16f;
                    var d = (mx - x) * (mx - x) + (my - y) * (my - y);
                    if (d < best && Fits(col, row, cells, movement))
                    {
                        best = d;
                        landedX = mx;
                        landedY = my;
                    }
                }
            }
        }

        /// <summary>Whether every unit counts as a living building.</summary>
        public bool AllBuildings;

        /// <summary>The places WatchDeathsAround was asked about, in order.</summary>
        public List<Vector2> Watched = new List<Vector2>();

        /// <summary>What Listen was given: a case calls it as the game would at a death.</summary>
        public Action<float, float>? Changed;

        public bool IsLiveBuilding(unit handle)
        {
            return AllBuildings;
        }

        public void PlaceOf(unit handle, out float x, out float y)
        {
            x = 0f;
            y = 0f;
        }

        public void Listen(Action<float, float> changed)
        {
            Changed = changed;
        }

        public void WatchDeathsAround(float x, float y)
        {
            Watched.Add(new Vector2(x, y));
        }

        public float CollisionOf(unit handle)
        {
            return Collisions.TryGetValue(handle, out var size) ? size : 31f;
        }

        public Movement MovementOf(unit handle)
        {
            return Movements.TryGetValue(handle, out var movement) ? movement : Movement.Ground;
        }

        private bool Fits(int col, int row, int cells, Movement movement)
        {
            var first = cells / 2;
            for (var i = col - first; i < col - first + cells; i++)
            {
                for (var j = row - first; j < row - first + cells; j++)
                {
                    var x = i * 32f + 16f;
                    var y = j * 32f + 16f;
                    var walk = movement != Movement.Float && terrain.IsWalkable(x, y);
                    var swim = movement != Movement.Ground && terrain.IsFloatable(x, y);
                    if (!(walk || swim) || (terrain.Occupied != null && terrain.Occupied(x, y)))
                    {
                        return false;
                    }

                    foreach (var square in Standing)
                    {
                        if (i >= square[0] && i < square[0] + square[2] && j >= square[1] && j < square[1] + square[2])
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static int Cell(float coordinate)
        {
            return (int)MathF.Floor(coordinate / 32f);
        }
    }
}
