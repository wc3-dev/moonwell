using System;
using System.Collections.Generic;
using System.ComponentModel;
using Moonwell.Engine;

namespace Moonwell
{
    public static partial class Terrain
    {
        /// <summary>How far a unit moves before Trail lays its next piece.</summary>
        private const float TrailSpacing = 64f;

        /// <summary>How often Spread grows and Trail looks where its unit is, in seconds.</summary>
        private const float GrowStep = 0.25f;

        /// <summary>The map's list of tiles, four characters each, from the build; null in a world with none, where tiles are not checked.</summary>
        private static string? mapTiles;

        /// <summary>Gives the package the map's list of tiles, which the build writes.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void InstallTiles(string tiles)
        {
            mapTiles = tiles;
        }

        /// <summary>
        /// Raises the ground in a circle for as long as the scope holds it: by <paramref name="by"/> at the centre, less
        /// out to the radius, as the game's lasting crater does. <c>Height</c>, <c>Normal</c>, <c>Raycast</c> and physics
        /// follow it on every machine, from the call; the game draws it from the next moment. Raises add up where they
        /// meet, and ending one takes off exactly its own. A radius of 0 or less is refused.
        /// </summary>
        [ChangesState]
        public static TerrainChange Raise(float x, float y, float radius, float by, Scope scope)
        {
            return Round(TerrainChange.Raise, x, y, radius, 0, false, by, scope, "Terrain.Raise");
        }

        /// <summary>Lowers the ground in a circle for as long as the scope holds it, as <c>Raise</c> with the opposite amount.</summary>
        [ChangesState]
        public static TerrainChange Lower(float x, float y, float radius, float by, Scope scope)
        {
            return Round(TerrainChange.Raise, x, y, radius, 0, false, -by, scope, "Terrain.Lower");
        }

        /// <summary>
        /// Raises each corner of a grid by its own amount (a negative one lowers it, 0 leaves it) for as long as the scope
        /// holds it, as one change. The grid's first corner is the map's corner nearest (x, y); <paramref name="by"/> holds a
        /// corner each, row by row from the bottom left, <paramref name="columns"/> across and 128 apart. <c>Height</c> and
        /// the rest follow as for a single <c>Raise</c>. A corner outside the map is left out. The array is copied. A grid
        /// that is not rows of <paramref name="columns"/> is refused.
        /// <para>
        /// The game makes each new crater slower the more it holds (about 10 µs with none, 50 µs at 25,000 and 500 µs at
        /// 85,000, 3.0.1.24371), so a grid of more than some tens of thousands of corners is better raised by fewer, wider
        /// craters: with a <paramref name="spacing"/> above 1, a crater stands every so many corners, the array holds one
        /// amount a crater, and each moves the corners within its <paramref name="radius"/> by its amount x cos(90 degrees x
        /// distance / radius), as the game's crater does; a corner moves by the sum of the craters that reach it. The
        /// spacing 1 and radius 128 it takes unless set are a crater a corner, as above. A spacing below 1 or a radius of 0
        /// or less is refused.
        /// </para>
        /// </summary>
        [ChangesState]
        public static TerrainChange Raise(float x, float y, int columns, float[] by, Scope scope, int spacing = 1, float radius = 128f)
        {
            var amounts = new float[by.Length];
            for (var k = 0; k < by.Length; k++)
            {
                amounts[k] = by[k];
            }

            return OverGrid(TerrainChange.Raise, x, y, columns, amounts, null, scope, "Terrain.Raise", spacing, radius);
        }

        /// <summary>Rings that run out from a point over some seconds. Only for the look: <c>Height</c> does not see it, and the ground is as before after.</summary>
        [ChangesState]
        public static void Ripple(float x, float y, float radius, float depth, float seconds)
        {
            World.Ripple(x, y, radius, depth, seconds);
        }

        /// <summary>A wave that runs along the ground from one point to another over some seconds. Only for the look, as <c>Ripple</c>.</summary>
        [ChangesState]
        public static void Wave(float fromX, float fromY, float toX, float toY, float radius, float depth, float seconds)
        {
            World.Wave(fromX, fromY, toX, toY, radius, depth, seconds);
        }

        /// <summary>Shakes the ground in a circle, up or down by up to so much, for some seconds. Only for the look, as <c>Ripple</c>.</summary>
        [ChangesState]
        public static void Shake(float x, float y, float radius, float by, float seconds)
        {
            World.Shake(x, y, radius, by, seconds);
        }

        /// <summary>
        /// Takes kinds of pathing away in a rect for as long as the scope holds it, in every 32 by 32 cell whose centre is in
        /// it. <c>IsWalkable</c> and the other reads see it at once. Where pathing changes overlap, the newest held wins.
        /// </summary>
        [ChangesState]
        public static TerrainChange Block(Bounds area, Pathing kinds, Scope scope)
        {
            return Rect(TerrainChange.Paths, area, (int)kinds, false, scope);
        }

        /// <summary>Takes kinds of pathing away in a circle for as long as the scope holds it, as <c>Block(area, ...)</c>.</summary>
        [ChangesState]
        public static TerrainChange Block(float x, float y, float radius, Pathing kinds, Scope scope)
        {
            return Round(TerrainChange.Paths, x, y, radius, (int)kinds, false, 0f, scope, "Terrain.Block");
        }

        /// <summary>
        /// Takes each corner's own kinds of pathing away (<c>Pathing.None</c> leaves a corner) for as long as the scope
        /// holds it, as one change: in the 128 square around the corner, the 16 cells whose centres are in it. The grid
        /// is laid out as <c>Raise</c>'s over a grid.
        /// </summary>
        [ChangesState]
        public static TerrainChange Block(float x, float y, int columns, Pathing[] kinds, Scope scope)
        {
            var values = new int[kinds.Length];
            for (var k = 0; k < kinds.Length; k++)
            {
                values[k] = (int)kinds[k];
            }

            return OverGrid(TerrainChange.Paths, x, y, columns, null, values, scope, "Terrain.Block");
        }

        /// <summary>Gives kinds of pathing in a rect for as long as the scope holds it, as <c>Block</c> takes them.</summary>
        [ChangesState]
        public static TerrainChange Allow(Bounds area, Pathing kinds, Scope scope)
        {
            return Rect(TerrainChange.Paths, area, (int)kinds, true, scope);
        }

        /// <summary>Gives kinds of pathing in a circle for as long as the scope holds it, as <c>Block</c> takes them.</summary>
        [ChangesState]
        public static TerrainChange Allow(float x, float y, float radius, Pathing kinds, Scope scope)
        {
            return Round(TerrainChange.Paths, x, y, radius, (int)kinds, true, 0f, scope, "Terrain.Allow");
        }

        /// <summary>
        /// Paints a tile (one of <c>Tile</c>'s) on every corner within a radius for as long as the scope holds it; then each
        /// corner shows what it showed before, or the newest tile change still held there. Pathing does not change. The game
        /// draws only tiles in the map's list, so a development build reports another.
        /// </summary>
        [ChangesState]
        public static TerrainChange SetTile(float x, float y, int tile, float radius, Scope scope)
        {
            CheckTile(tile);
            return Round(TerrainChange.Tiles, x, y, radius, tile, false, 0f, scope, "Terrain.SetTile");
        }

        /// <summary>Paints a tile on every corner in a rect for as long as the scope holds it, as <c>SetTile(x, y, ...)</c>.</summary>
        [ChangesState]
        public static TerrainChange SetTile(Bounds area, int tile, Scope scope)
        {
            CheckTile(tile);
            return Rect(TerrainChange.Tiles, area, tile, false, scope);
        }

        /// <summary>
        /// Paints each corner of a grid with its own tile (0 leaves a corner) for as long as the scope holds it, as one
        /// change. The grid is laid out as <c>Raise</c>'s over a grid. A development build reports each tile the map's
        /// list lacks, once.
        /// </summary>
        [ChangesState]
        public static TerrainChange SetTile(float x, float y, int columns, int[] tiles, Scope scope)
        {
            var values = new int[tiles.Length];
            var seen = new List<int>();
            for (var k = 0; k < tiles.Length; k++)
            {
                var tile = tiles[k];
                values[k] = tile;
                if (tile != 0 && !seen.Contains(tile))
                {
                    seen.Add(tile);
                    CheckTile(tile);
                }
            }

            return OverGrid(TerrainChange.Tiles, x, y, columns, null, values, scope, "Terrain.SetTile");
        }

        /// <summary>The tile at the corner nearest a point, one of <c>Tile</c>'s.</summary>
        [LocalSafe]
        public static int Tile(float x, float y)
        {
            return World.TileAt(x, y);
        }

        /// <summary>The tile at the corner nearest a point, as <c>Tile(x, y)</c>.</summary>
        [LocalSafe]
        public static int Tile(Vector2 point)
        {
            return Tile(point.X, point.Y);
        }

        /// <summary>
        /// Blights the ground in a circle for as long as the scope holds it. When it ends, the blight goes, but for the
        /// blight of other changes still held and of buildings; blight the map was made with goes too.
        /// </summary>
        [ChangesState]
        public static TerrainChange Blight(float x, float y, float radius, Scope scope)
        {
            return Round(TerrainChange.Blights, x, y, radius, 0, true, 0f, scope, "Terrain.Blight");
        }

        /// <summary>Blights the ground in a rect for as long as the scope holds it, as <c>Blight(x, y, ...)</c>.</summary>
        [ChangesState]
        public static TerrainChange Blight(Bounds area, Scope scope)
        {
            return Rect(TerrainChange.Blights, area, 0, true, scope);
        }

        /// <summary>
        /// Takes blight off the ground in a circle for as long as the scope holds it; then blight changes still held come
        /// back. The game can't take off blight a building made, so a development build reports a cleanse whose centre stays
        /// blighted.
        /// </summary>
        [ChangesState]
        public static TerrainChange Cleanse(float x, float y, float radius, Scope scope)
        {
            return Round(TerrainChange.Blights, x, y, radius, 0, false, 0f, scope, "Terrain.Cleanse");
        }

        /// <summary>Takes blight off the ground in a rect for as long as the scope holds it, as <c>Cleanse(x, y, ...)</c>.</summary>
        [ChangesState]
        public static TerrainChange Cleanse(Bounds area, Scope scope)
        {
            return Rect(TerrainChange.Blights, area, 0, false, scope);
        }

        /// <summary>Whether a point is blighted.</summary>
        [LocalSafe]
        public static bool IsBlighted(float x, float y)
        {
            return World.IsBlighted(x, y);
        }

        /// <summary>Whether a point is blighted, as <c>IsBlighted(x, y)</c>.</summary>
        [LocalSafe]
        public static bool IsBlighted(Vector2 point)
        {
            return IsBlighted(point.X, point.Y);
        }

        /// <summary>
        /// Tints the map's water by a colour for as long as the scope holds it: the game multiplies its own water colour by
        /// it, so white leaves it and red turns blue water dark. The newest tint held shows, and white when none is.
        /// </summary>
        [ChangesState]
        public static TerrainChange WaterTint(Color color, Scope scope)
        {
            var packed = (color.R << 24) | (color.G << 16) | (color.B << 8) | color.A;
            return Changes.Make(new TerrainChange(TerrainChange.Water, 0f, 0f, 0f, 0f, 0f, 0f, 0f, packed, true, 0f), scope);
        }

        /// <summary>
        /// Grows a change from nothing to a radius at a speed (units a second), every quarter second: <paramref name="make"/>
        /// is given the radius reached and a scope, makes the change at that radius in it, and the change before ends. The
        /// last stays until the scope given ends, which ends it all.
        /// </summary>
        [ChangesState]
        public static void Spread(float radius, float speed, Action<float, Scope> make, Scope scope)
        {
            if (!(radius > 0f && speed > 0f))
            {
                Errors.Refuse("Terrain.Spread", "the radius and the speed must be above 0, not " + radius + " and " + speed);
                return;
            }

            var reach = 0f;
            Piece? before = null;
            Timer? timer = null;
            Action grow = () =>
            {
                reach = Math.Min(radius, reach + speed * GrowStep);
                var piece = new Piece();
                scope.Adopt(piece);
                make(reach, piece);
                before?.End();
                before = piece;
                if (reach >= radius)
                {
                    timer?.End();
                }
            };
            timer = scope.Every(GrowStep, grow);
            grow();
        }

        /// <summary>
        /// Lays a change behind a moving unit: at once, then each time it has moved 64 from the last piece (looked at every
        /// quarter second), <paramref name="make"/> is given its place and a scope that ends after some seconds, and makes a piece
        /// there in it. It stops when the scope given ends, which ends every piece.
        /// </summary>
        [ChangesState]
        public static void Trail(Unit unit, float seconds, Action<float, float, Scope> make, Scope scope)
        {
            var lastX = 0f;
            var lastY = 0f;
            var laid = false;
            Action look = () =>
            {
                if (unit.IsGone)
                {
                    return;
                }

                var x = unit.X;
                var y = unit.Y;
                var dx = x - lastX;
                var dy = y - lastY;
                if (laid && dx * dx + dy * dy < TrailSpacing * TrailSpacing)
                {
                    return;
                }

                laid = true;
                lastX = x;
                lastY = y;
                var piece = new Piece();
                scope.Adopt(piece);
                piece.After(seconds, piece.End);
                make(x, y, piece);
            };
            scope.Every(GrowStep, look);
            look();
        }

        /// <summary>A round change, refused for a radius of 0 or less.</summary>
        private static TerrainChange Round(int kind, float x, float y, float radius, int value, bool on, float by, Scope scope, string label)
        {
            var change = new TerrainChange(kind, x, y, radius, x - radius, y - radius, x + radius, y + radius, value, on, by);
            if (!(radius > 0f))
            {
                Errors.Refuse(label, "the radius must be above 0, not " + radius);
                change.End();
                return change;
            }

            return Changes.Make(change, scope);
        }

        /// <summary>A change over a rect.</summary>
        private static TerrainChange Rect(int kind, Bounds area, int value, bool on, Scope scope)
        {
            var centre = area.Center;
            return Changes.Make(new TerrainChange(kind, centre.X, centre.Y, -1f, area.Min.X, area.Min.Y, area.Max.X, area.Max.Y, value, on, 0f), scope);
        }

        /// <summary>A change over a grid of corners, its first the map's corner nearest (x, y); refused for a grid that is not rows of columns.</summary>
        private static TerrainChange OverGrid(int kind, float x, float y, int columns, float[]? amounts, int[]? values, Scope scope, string label, int spacing = 1, float radius = -1f)
        {
            var length = amounts != null ? amounts.Length : values!.Length;
            var fits = columns >= 1 && length >= columns && length % columns == 0;
            var spaced = spacing >= 1 && (kind != TerrainChange.Raise || radius > 0f);
            var grid = Grid;
            var change = new TerrainChange(kind, grid.Left, grid.Bottom, Nearest((x - grid.Left) / 128f), Nearest((y - grid.Bottom) / 128f), fits && spaced ? columns : 0, fits && spaced ? length / columns : 0, amounts, values, spaced ? spacing : 1, radius);
            if (!fits)
            {
                Errors.Refuse(label, "a grid of " + length + " corners is not rows of " + columns);
                change.End();
                return change;
            }

            if (!spaced)
            {
                Errors.Refuse(label, "craters a spacing of " + spacing + " corners apart and a radius of " + radius + ": the spacing is 1 or more and the radius above 0");
                change.End();
                return change;
            }

            return Changes.Make(change, scope);
        }

        /// <summary>The whole number nearest a value, a negative one too; 0 for a NaN.</summary>
        private static int Nearest(float value)
        {
            if (value >= 0f)
            {
                return Slots.Whole(value + 0.5f);
            }

            return value < 0f ? -Slots.Whole(0.5f - value) : 0;
        }

        /// <summary>Reports, in a development build, a tile the map's list lacks: the game would read it back but not draw it.</summary>
        private static void CheckTile(int tile)
        {
            if (mapTiles == null)
            {
                return;
            }

            var id = Names.IdText(tile);
            for (var at = 0; at + 4 <= mapTiles.Length; at += 4)
            {
                if (mapTiles.Substring(at, 4) == id)
                {
                    return;
                }
            }

            Dev.Report("Terrain.SetTile", "the map's tiles (" + mapTiles + ") lack " + id + ": the game paints it but draws one of the map's own; add it to the map class's Tiles");
        }

        /// <summary>A scope Spread and Trail make each piece in.</summary>
        private sealed class Piece : Scope
        {
        }
    }
}
