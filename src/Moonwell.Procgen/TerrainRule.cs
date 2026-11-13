using System;

namespace Moonwell.Procgen
{
    /// <summary>
    /// A rule of a biome, from <c>Paint</c>, <c>Block</c>, <c>Forest</c>, <c>Wall</c> or <c>Scatter</c>: the heights and
    /// slopes of the corners it fits. A height is a share of the biome's range, 0 at <c>Lowest</c> and 1 at
    /// <c>Highest</c>; a slope is in degrees. Changing a field changes the plans made after it.
    /// </summary>
    public abstract class TerrainRule
    {
        internal TerrainRule(float fromHeight, float toHeight, float fromSlope, float toSlope)
        {
            FromHeight = fromHeight;
            ToHeight = toHeight;
            FromSlope = fromSlope;
            ToSlope = toSlope;
        }

        /// <summary>The lowest height it fits, a share of the biome's range.</summary>
        public float FromHeight { get; set; }

        /// <summary>The highest height it fits, a share of the biome's range.</summary>
        public float ToHeight { get; set; }

        /// <summary>The gentlest slope it fits, in degrees.</summary>
        public float FromSlope { get; set; }

        /// <summary>The steepest slope it fits, in degrees; 90 for any.</summary>
        public float ToSlope { get; set; }
    }

    /// <summary>A rule that paints a tile, from <c>Paint</c>.</summary>
    public sealed class PaintRule : TerrainRule
    {
        private int tile;

        internal PaintRule(int number, int tile, float fromHeight, float toHeight, float fromSlope, float toSlope, float cover)
            : base(fromHeight, toHeight, fromSlope, toSlope)
        {
            Number = number;
            Tile = tile;
            Cover = cover;
        }

        /// <summary>The tile it paints, one of <c>Tile</c>'s; 0 throws ArgumentException.</summary>
        public int Tile
        {
            get => tile;
            set
            {
                if (value == 0)
                {
                    throw new ArgumentException("A paint rule takes one of Tile's tiles, not 0");
                }

                tile = value;
            }
        }

        /// <summary>The share of the corners it paints, in patches about 512 across; 1 paints every corner that fits.</summary>
        public float Cover { get; set; }

        /// <summary>Its number among its biome's paint rules, from 1, which its patches' seed comes from.</summary>
        internal int Number { get; }
    }

    /// <summary>A rule that takes pathing, from <c>Block</c>.</summary>
    public sealed class BlockRule : TerrainRule
    {
        internal BlockRule(Pathing kinds, float fromHeight, float toHeight, float fromSlope, float toSlope)
            : base(fromHeight, toHeight, fromSlope, toSlope)
        {
            Kinds = kinds;
        }

        /// <summary>The kinds of pathing it takes.</summary>
        public Pathing Kinds { get; set; }
    }

    /// <summary>A rule that stands trees, from <c>Forest</c>, <c>Wall</c> or <c>Scatter</c>.</summary>
    public abstract class TreeRule : TerrainRule
    {
        private int type;
        private int variations;

        internal TreeRule(int number, int type, float fromHeight, float toHeight, float fromSlope, float toSlope, int onTile, int variations)
            : base(fromHeight, toHeight, fromSlope, toSlope)
        {
            Number = number;
            Type = type;
            OnTile = onTile;
            Variations = variations;
        }

        /// <summary>The destructable type of its trees; 0 throws ArgumentException.</summary>
        public int Type
        {
            get => type;
            set
            {
                if (value == 0)
                {
                    throw new ArgumentException("A tree rule takes a destructable type, not 0");
                }

                type = value;
            }
        }

        /// <summary>A tile the biome paints where a tree stands, or 0 for any corner.</summary>
        public int OnTile { get; set; }

        /// <summary>How many of the type's models its trees take from; fewer than 1 throws ArgumentException.</summary>
        public int Variations
        {
            get => variations;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException("A tree rule takes 1 variation or more, not " + value);
                }

                variations = value;
            }
        }

        /// <summary>Its number among its biome's tree rules, from 1, which its seed comes from.</summary>
        internal int Number { get; }
    }

    /// <summary>Patches of forest, from <c>Forest</c>.</summary>
    public sealed class ForestRule : TreeRule
    {
        internal ForestRule(int number, int type, float cover, float density, float fromHeight, float toHeight, float fromSlope, float toSlope, int onTile, int variations)
            : base(number, type, fromHeight, toHeight, fromSlope, toSlope, onTile, variations)
        {
            Cover = cover;
            Density = density;
        }

        /// <summary>The share of the corners in its patches.</summary>
        public float Cover { get; set; }

        /// <summary>The share of a patch's corners with a tree, 1 for a thick forest.</summary>
        public float Density { get; set; }
    }

    /// <summary>Winding forest walls, from <c>Wall</c>.</summary>
    public sealed class WallRule : TreeRule
    {
        private float spacing;
        private float width;

        internal WallRule(int number, int type, float spacing, float width, float gaps, float fromHeight, float toHeight, float fromSlope, float toSlope, int onTile, int variations)
            : base(number, type, fromHeight, toHeight, fromSlope, toSlope, onTile, variations)
        {
            Spacing = spacing;
            Width = width;
            Gaps = gaps;
        }

        /// <summary>About how far apart the walls are, in the game's units; 0 or less throws ArgumentException.</summary>
        public float Spacing
        {
            get => spacing;
            set => spacing = Above0(value, "spacing");
        }

        /// <summary>How many trees thick the walls are; 0 or less throws ArgumentException.</summary>
        public float Width
        {
            get => width;
            set => width = Above0(value, "width");
        }

        /// <summary>The share of their length left open, in gaps about 512 across.</summary>
        public float Gaps { get; set; }

        private static float Above0(float value, string name)
        {
            if (!(value > 0f))
            {
                throw new ArgumentException("A wall rule takes a " + name + " above 0, not " + value);
            }

            return value;
        }
    }

    /// <summary>Single trees, from <c>Scatter</c>.</summary>
    public sealed class ScatterRule : TreeRule
    {
        internal ScatterRule(int number, int type, float chance, float fromHeight, float toHeight, float fromSlope, float toSlope, int onTile, int variations)
            : base(number, type, fromHeight, toHeight, fromSlope, toSlope, onTile, variations)
        {
            Chance = chance;
        }

        /// <summary>The share of the corners that fit that get a tree.</summary>
        public float Chance { get; set; }
    }
}
