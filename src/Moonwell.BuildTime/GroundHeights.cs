using War3Net.Build.Environment;

namespace Moonwell.BuildTime
{
    /// <summary>
    /// The ground's height at the map's 128 corners, from <c>war3map.w3e</c>, as the game reads it: one rule for the terrain grid
    /// the build writes for <c>Moonwell.Terrain</c> and for <see cref="MapReader.HeightAt"/>. A corner's ground is its height and
    /// cliff level (<c>Height x 128 + (CliffLevel - 2) x 128</c>), a ramp-flagged corner raised 32 for each of the (up to four)
    /// tiles it is a corner of that has, among its other three corners, a ramp-flagged corner whose cliff level is exactly one
    /// higher. A water-flagged corner reads the water's surface (<c>WaterHeight x 128 - 89.6</c>) where that is above its ground,
    /// and the ground elsewhere. A height is kept to a twentieth, and a point between corners blends the four corners of its
    /// tile bilinearly, as the grid does.
    /// </summary>
    internal static class GroundHeights
    {
        /// <summary>The width of a tile, between corners.</summary>
        public const float TileSize = 128f;

        /// <summary>The corners across: War3Net's width counts tiles, the grid the corners between and around them.</summary>
        public static int Columns(MapEnvironment environment) => (int)environment.Width + 1;

        /// <summary>The corners up.</summary>
        public static int Rows(MapEnvironment environment) => (int)environment.Height + 1;

        /// <summary>A corner's ground: its height and cliff level, raised on a ramp, whatever water is above it.</summary>
        public static float Ground(MapEnvironment environment, int index)
        {
            var tile = environment.TerrainTiles[index];
            var ground = (tile.Height * TileSize) + ((tile.CliffLevel - 2) * TileSize);
            return ground + (32f * HigherRampTiles(environment, Columns(environment), Rows(environment), index));
        }

        /// <summary>The water's surface over a corner, whether or not the corner has water.</summary>
        public static float Surface(TerrainTile tile) => (tile.WaterHeight * TileSize) - 89.6f;

        /// <summary>Whether a corner reads its water: it has the water flag and the surface is above its ground.</summary>
        public static bool IsWet(TerrainTile tile, float ground) => tile.IsWater && Surface(tile) > ground;

        /// <summary>The height the game reads at a corner, before it is kept to a twentieth: the water's surface or the ground.</summary>
        public static float Corner(MapEnvironment environment, int index, out bool water)
        {
            var tile = environment.TerrainTiles[index];
            var ground = Ground(environment, index);
            water = IsWet(tile, ground);
            return water ? Surface(tile) : ground;
        }

        /// <summary>A height kept to a twentieth, as the grid keeps it.</summary>
        public static float Kept(float z) => MathF.Round(z * 20f) / 20f;

        /// <summary>
        /// The four corners of a point's tile blended bilinearly, as the grid blends them; a point outside the grid reads its
        /// edge.
        /// </summary>
        /// <param name="columns">The corners across.</param>
        /// <param name="rows">The corners up.</param>
        /// <param name="left">The x of corner 0, 0.</param>
        /// <param name="bottom">The y of corner 0, 0.</param>
        /// <param name="x">The point's x.</param>
        /// <param name="y">The point's y.</param>
        /// <param name="corner">A corner's height by its index, row by row from the bottom left.</param>
        public static float Blend(int columns, int rows, float left, float bottom, float x, float y, Func<int, float> corner)
        {
            var u = Math.Clamp((x - left) / TileSize, 0f, columns - 1);
            var v = Math.Clamp((y - bottom) / TileSize, 0f, rows - 1);
            var i = Math.Min((int)MathF.Floor(u), Math.Max(columns - 2, 0));
            var j = Math.Min((int)MathF.Floor(v), Math.Max(rows - 2, 0));
            var right = i + 1 < columns ? i + 1 : i;
            var top = j + 1 < rows ? j + 1 : j;
            var fx = u - i;
            var fy = v - j;
            var z00 = corner((j * columns) + i);
            var z10 = corner((j * columns) + right);
            var z01 = corner((top * columns) + i);
            var z11 = corner((top * columns) + right);
            return ((1f - fy) * (((1f - fx) * z00) + (fx * z10))) + (fy * (((1f - fx) * z01) + (fx * z11)));
        }

        // A ramp corner is raised 32 for each tile around it (up to four) that has another corner with the ramp flag and
        // a cliff level exactly one higher. A corner outside the grid is not there to be that corner.
        private static int HigherRampTiles(MapEnvironment environment, int columns, int rows, int index)
        {
            var tile = environment.TerrainTiles[index];
            if (!tile.IsRamp)
            {
                return 0;
            }

            var column = index % columns;
            var row = index / columns;
            var count = 0;
            for (var tileRow = row - 1; tileRow <= row; tileRow++)
            {
                for (var tileColumn = column - 1; tileColumn <= column; tileColumn++)
                {
                    if (TileHasHigherRamp(environment, tile, column, row, tileColumn, tileRow, columns, rows))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        // The tile whose bottom-left corner is (tileColumn, tileRow): does one of its other corners read as a higher ramp?
        private static bool TileHasHigherRamp(MapEnvironment environment, TerrainTile tile, int column, int row, int tileColumn, int tileRow, int columns, int rows)
        {
            for (var y = tileRow; y <= tileRow + 1; y++)
            {
                for (var x = tileColumn; x <= tileColumn + 1; x++)
                {
                    if ((x == column && y == row) || x < 0 || x >= columns || y < 0 || y >= rows)
                    {
                        continue;
                    }

                    var other = environment.TerrainTiles[(y * columns) + x];
                    if (other.IsRamp && other.CliffLevel == tile.CliffLevel + 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
