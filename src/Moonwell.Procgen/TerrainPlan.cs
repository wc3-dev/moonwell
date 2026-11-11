namespace Moonwell.Procgen
{
    /// <summary>
    /// Ground worked out and not yet made: for each corner of a grid, 128 apart, how far to move it, the tile to paint
    /// and the pathing to take. A <c>TerrainGenerator</c> gives one; a map may change it, and <c>Apply</c> makes it.
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

        /// <summary>
        /// Makes the plan's ground for as long as the scope holds it (<c>Game.Instance</c> for the whole game): the heights,
        /// the tiles and the blocked corners, each one terrain change over the grid, whose first corner is the map's corner
        /// nearest (X, Y). A part with nothing in it makes no change. Ending the scope takes the ground back.
        /// </summary>
        [ChangesState]
        public void Apply(Scope scope)
        {
            var moved = false;
            var painted = false;
            var blocked = false;
            for (var k = 0; k < Tiles.Length; k++)
            {
                moved = moved || Heights.Cells[k] != 0f;
                painted = painted || Tiles[k] != 0;
                blocked = blocked || Blocked[k] != Pathing.None;
            }

            if (moved)
            {
                Terrain.Raise(X, Y, Columns, Heights.Cells, scope);
            }

            if (painted)
            {
                Terrain.SetTile(X, Y, Columns, Tiles, scope);
            }

            if (blocked)
            {
                Terrain.Block(X, Y, Columns, Blocked, scope);
            }
        }
    }
}
