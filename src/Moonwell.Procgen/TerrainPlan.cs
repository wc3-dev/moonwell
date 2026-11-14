using System.Collections.Generic;

namespace Moonwell.Procgen
{
    /// <summary>
    /// Ground worked out and not yet made: for each corner of a grid, 128 apart, how far to move it, the tile to paint,
    /// the pathing to take and the tree to stand on it. A <c>TerrainGenerator</c> gives one; a map may change it, and <c>Apply</c> makes it.
    /// </summary>
    public sealed class TerrainPlan
    {
        /// <summary>An empty plan of corners from a point: nothing moved, painted or blocked. Fewer than 1 column or row throws ArgumentException.</summary>
        public TerrainPlan(float x, float y, int columns, int rows)
        {
            X = x;
            Y = y;
            Heights = new Field(columns, rows);
            Tiles = new int[columns * rows];
            Blocked = new Pathing[columns * rows];
            Trees = new int[columns * rows];
            TreeVariations = new int[columns * rows];
        }

        /// <summary>The x of the first corner, the lower left.</summary>
        [LocalSafe]
        public float X { get; }

        /// <summary>The y of the first corner.</summary>
        [LocalSafe]
        public float Y { get; }

        /// <summary>The corners across.</summary>
        [LocalSafe]
        public int Columns => Heights.Columns;

        /// <summary>The corners up.</summary>
        [LocalSafe]
        public int Rows => Heights.Rows;

        /// <summary>How far each corner is moved from the ground as it was, in the game's units: above 0 up, below 0 down.</summary>
        [LocalSafe]
        public Field Heights { get; }

        /// <summary>The tile of each corner, one of <c>Tile</c>'s, row by row from the bottom left (<c>row * Columns + column</c>); 0 leaves the corner as it is.</summary>
        [LocalSafe]
        public int[] Tiles { get; }

        /// <summary>The kinds of pathing taken around each corner, laid out as <c>Tiles</c>; <c>Pathing.None</c> leaves the corner as it is.</summary>
        [LocalSafe]
        public Pathing[] Blocked { get; }

        /// <summary>The destructable type of the tree on each corner, laid out as <c>Tiles</c>; 0 for none.</summary>
        [LocalSafe]
        public int[] Trees { get; }

        /// <summary>Each tree's variation, the model of its type it is made with, laid out as <c>Tiles</c>.</summary>
        [LocalSafe]
        public int[] TreeVariations { get; }

        /// <summary>The generator's places whose centre is nearest one of its corners, in the order made: each place is held by the one plan of touching plans that holds that corner.</summary>
        [LocalSafe]
        public IReadOnlyList<Place> Places => places;

        private readonly List<Place> places = new List<Place>();

        /// <summary>
        /// For a plan too big for a crater a corner (decision 343): the wide craters' depths, row by row from the first
        /// <c>WideCraters.Inset</c> corners in, <c>WideAcross</c> across, and the ground they make at each corner; null otherwise.
        /// </summary>
        internal float[]? WideDepths;

        internal int WideAcross;

        internal float[]? Under;

        /// <summary>
        /// Makes the plan's ground for as long as the scope holds it (<c>Game.Instance</c> for the whole game): the heights,
        /// the tiles and the blocked corners, each one terrain change over the grid, whose first corner is the map's corner
        /// nearest (X, Y), then the trees, standing on the ground as it is then, then the units and buildings of its places. A
        /// part with nothing in it makes no change. Ending the scope takes the ground back and removes the trees, cut and dead
        /// ones too, and the units and buildings.
        /// </summary>
        [ChangesState]
        public void Apply(Scope scope)
        {
            var ground = new GroundScope(scope);
            var moved = false;
            var painted = false;
            var blocked = false;
            var by = Heights.Cells;
            if (WideDepths != null)
            {
                // The wide craters make Under; each corner's crater makes the rest, the plan's own and any change made to it since.
                Terrain.Raise(X + WideCraters.Inset * 128f, Y + WideCraters.Inset * 128f, WideAcross, WideDepths, ground, WideCraters.Spacing, WideCraters.Radius);
                by = new float[Tiles.Length];
                for (var k = 0; k < by.Length; k++)
                {
                    by[k] = Heights.Cells[k] - Under![k];
                }
            }

            for (var k = 0; k < Tiles.Length; k++)
            {
                moved = moved || by[k] != 0f;
                painted = painted || Tiles[k] != 0;
                blocked = blocked || Blocked[k] != Pathing.None;
            }

            if (moved)
            {
                Terrain.Raise(X, Y, Columns, by, ground);
            }

            if (painted)
            {
                Terrain.SetTile(X, Y, Columns, Tiles, ground);
            }

            if (blocked)
            {
                Terrain.Block(X, Y, Columns, Blocked, ground);
            }

            MakeTrees(0, Trees.Length, ground);
            MakePlaces(0, places.Count, ground);
        }

        internal void Hold(Place place)
        {
            places.Add(place);
        }

        /// <summary>Makes the units and buildings of the places in a range.</summary>
        internal void MakePlaces(int from, int to, Scope scope)
        {
            for (var k = from; k < to; k++)
            {
                places[k].Make(scope);
            }
        }

        /// <summary>Makes the trees of the corners in a range, each at the ground's height there.</summary>
        internal void MakeTrees(int from, int to, Scope scope)
        {
            for (var at = from; at < to; at++)
            {
                if (Trees[at] != 0)
                {
                    var x = X + at % Columns * 128f;
                    var y = Y + at / Columns * 128f;
                    Destructable.Create(Trees[at], x, y, Terrain.Height(x, y), scope, variation: TreeVariations[at]);
                }
            }
        }
    }
}
