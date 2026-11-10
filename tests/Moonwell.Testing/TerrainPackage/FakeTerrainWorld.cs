using System;
using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>The fake game the terrain package's cases run against: ground height from a function, pathing flags per 32 by 32 cell, and an item that lands where the case says. It counts its calls.</summary>
    public sealed class FakeTerrainWorld : ITerrainWorld
    {
        /// <summary>The flag of a cell where units cannot walk.</summary>
        public const int NoWalk = 1;

        /// <summary>The flag of a cell where flying units cannot fly.</summary>
        public const int NoFly = 2;

        /// <summary>The flag of a cell where nothing can be built.</summary>
        public const int NoBuild = 4;

        /// <summary>The flag of a cell where ships cannot float.</summary>
        public const int NoFloat = 8;

        /// <summary>The flags of the cells that have any; a cell not in it allows everything.</summary>
        private readonly Dictionary<string, int> cells = new Dictionary<string, int>();

        /// <summary>The ground's height at a point; flat at 0 until a case sets it.</summary>
        public Func<float, float, float> Ground = (x, y) => 0f;

        /// <summary>How many times the height was read.</summary>
        public int HeightCalls;

        /// <summary>How many times a pathing flag was read.</summary>
        public int PathCalls;

        /// <summary>How many times an item was placed.</summary>
        public int PlaceCalls;

        /// <summary>The x of the last place the package asked for.</summary>
        public float AskedX;

        /// <summary>The y of the last place the package asked for.</summary>
        public float AskedY;

        /// <summary>Whether the item lands at LandsX, LandsY instead of where it was put.</summary>
        public bool Lands;

        /// <summary>The x the item lands at when Lands is true.</summary>
        public float LandsX;

        /// <summary>The y the item lands at when Lands is true.</summary>
        public float LandsY;

        /// <summary>Where something stands that an item will not lie on, as a tree or a building: an item put where this answers true lands 64 to the right. Null for nowhere.</summary>
        public Func<float, float, bool>? Occupied;

        /// <summary>The playable area's lower-left x.</summary>
        public float AreaMinX = -512f;

        /// <summary>The playable area's lower-left y.</summary>
        public float AreaMinY = -256f;

        /// <summary>The playable area's upper-right x.</summary>
        public float AreaMaxX = 512f;

        /// <summary>The playable area's upper-right y.</summary>
        public float AreaMaxY = 256f;

        /// <summary>Sets the flags of the 32 by 32 cell that holds a point.</summary>
        public void Block(float x, float y, int flags)
        {
            cells[Key(x, y)] = flags;
        }

        /// <summary>The ground's height at a point.</summary>
        public float LocationZ(float x, float y)
        {
            HeightCalls++;
            return Ground(x, y);
        }

        /// <summary>Whether units can walk at a point.</summary>
        public bool IsWalkable(float x, float y) => Allowed(x, y, NoWalk);

        /// <summary>Whether flying units can fly at a point.</summary>
        public bool IsFlyable(float x, float y) => Allowed(x, y, NoFly);

        /// <summary>Whether a building can be built at a point.</summary>
        public bool IsBuildable(float x, float y) => Allowed(x, y, NoBuild);

        /// <summary>Whether ships can float at a point.</summary>
        public bool IsFloatable(float x, float y) => Allowed(x, y, NoFloat);

        /// <summary>Puts the item down: where it was asked, or where the case says it lands, or 64 to the right of a place that is occupied.</summary>
        public void Place(float x, float y, out float landedX, out float landedY)
        {
            PlaceCalls++;
            AskedX = x;
            AskedY = y;
            landedX = Lands ? LandsX : x;
            landedY = Lands ? LandsY : y;
            if (!Lands && Occupied != null && Occupied(x, y))
            {
                landedX = x + 64f;
            }
        }

        /// <summary>The playable area the case set.</summary>
        public void PlayableArea(out float minX, out float minY, out float maxX, out float maxY)
        {
            minX = AreaMinX;
            minY = AreaMinY;
            maxX = AreaMaxX;
            maxY = AreaMaxY;
        }

        /// <summary>The lasting craters made, in order: x, y, radius, depth.</summary>
        public readonly List<float[]> Craters = new List<float[]>();

        /// <summary>The look-only deformations played, in order, by name.</summary>
        public readonly List<string> Played = new List<string>();

        /// <summary>The tiles painted at corners, by the corner's key; a corner not in it is Ldrt with variation 3.</summary>
        public readonly Dictionary<string, int> Tiles = new Dictionary<string, int>();

        /// <summary>The variations painted at corners, by the corner's key.</summary>
        public readonly Dictionary<string, int> Variations = new Dictionary<string, int>();

        /// <summary>How many corners were painted.</summary>
        public int Paints;

        /// <summary>How many pathing kinds were set in cells.</summary>
        public int PathSets;

        /// <summary>The blight calls, in order, as "on x y radius" or "off minX minY maxX maxY", in whole numbers.</summary>
        public readonly List<string> Blights = new List<string>();

        /// <summary>Where the ground reads blighted; nowhere until a case sets it.</summary>
        public Func<float, float, bool> Blighted = (x, y) => false;

        /// <summary>The water tints, in order, as "r g b a".</summary>
        public readonly List<string> Tints = new List<string>();

        /// <summary>Records a lasting crater.</summary>
        public void Crater(float x, float y, float radius, float depth)
        {
            Craters.Add(new[] { x, y, radius, depth });
        }

        /// <summary>Records a ripple.</summary>
        public void Ripple(float x, float y, float radius, float depth, float seconds) => Played.Add("ripple");

        /// <summary>Records a wave.</summary>
        public void Wave(float fromX, float fromY, float toX, float toY, float radius, float depth, float seconds) => Played.Add("wave");

        /// <summary>Records a shake.</summary>
        public void Shake(float x, float y, float radius, float by, float seconds) => Played.Add("shake");

        /// <summary>The tile at the nearest corner.</summary>
        public int TileAt(float x, float y) => Tiles.TryGetValue(CornerKey(x, y), out var tile) ? tile : Tile.LordaeronSummerDirt;

        /// <summary>The variation at the nearest corner.</summary>
        public int VariationAt(float x, float y) => Variations.TryGetValue(CornerKey(x, y), out var variation) ? variation : 3;

        /// <summary>Paints the nearest corner.</summary>
        public void PaintTile(float x, float y, int tile, int variation)
        {
            Paints++;
            Tiles[CornerKey(x, y)] = tile;
            Variations[CornerKey(x, y)] = variation;
        }

        /// <summary>Turns a kind of pathing on or off in a cell: the No flags are the kinds' bits.</summary>
        public void SetPathing(float x, float y, int kind, bool on)
        {
            PathSets++;
            cells.TryGetValue(Key(x, y), out var flags);
            cells[Key(x, y)] = on ? flags & ~kind : flags | kind;
        }

        /// <summary>Records blight in a circle.</summary>
        public void Blight(float x, float y, float radius, bool on) => Blights.Add((on ? "on " : "off ") + (int)x + " " + (int)y + " " + (int)radius);

        /// <summary>Records blight in a rect.</summary>
        public void Blight(float minX, float minY, float maxX, float maxY, bool on) => Blights.Add((on ? "on " : "off ") + (int)minX + " " + (int)minY + " " + (int)maxX + " " + (int)maxY);

        /// <summary>Whether a point reads blighted.</summary>
        public bool IsBlighted(float x, float y) => Blighted(x, y);

        /// <summary>Records a water tint.</summary>
        public void TintWater(int red, int green, int blue, int alpha) => Tints.Add(red + " " + green + " " + blue + " " + alpha);

        /// <summary>The key of the corner nearest a point.</summary>
        public static string CornerKey(float x, float y)
        {
            return (int)MathF.Round(x / 128f) + "," + (int)MathF.Round(y / 128f);
        }

        private bool Allowed(float x, float y, int flag)
        {
            PathCalls++;
            return !cells.TryGetValue(Key(x, y), out var flags) || (flags & flag) == 0;
        }

        private static string Key(float x, float y)
        {
            return (int)MathF.Floor(x / 32f) + "," + (int)MathF.Floor(y / 32f);
        }
    }
}
