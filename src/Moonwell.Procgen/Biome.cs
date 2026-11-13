using System;
using System.Collections.Generic;

namespace Moonwell.Procgen
{
    /// <summary>
    /// A kind of land: the shape of its hills and its rules for tiles, blocking and trees. A generator puts it in an area
    /// (<c>Use</c>) or in patches with others (<c>Mix</c>), and is one itself: its own settings and rules are the ground
    /// outside every biome. The presets (<c>Grassland</c>, <c>RockyHills</c>, <c>SnowyForest</c>, <c>Desert</c>) start
    /// with rules a map changes one by one. Each rule call gives its rule, whose fields can be changed later. A plan takes
    /// a biome as it is when the plan starts.
    /// </summary>
    public class Biome
    {
        // Auto-properties, not fields: a map's class may derive from this, and in Lua it would share a private field's name.
        internal List<PaintRule> PaintRules { get; } = new List<PaintRule>();

        internal List<BlockRule> BlockRules { get; } = new List<BlockRule>();

        internal List<TreeRule> TreeRules { get; } = new List<TreeRule>();

        /// <summary>The paint and tree rules made so far, removed ones too, so each rule keeps its seed.</summary>
        private int PaintsMade { get; set; }

        private int TreesMade { get; set; }

        /// <summary>
        /// How far below the ground as it was the ground goes at most, in the game's units; below 0 digs. The same for
        /// every area, so plans of one generator meet. With a range around 0, as it starts, the ground stays at its level on
        /// the whole and the edge fade is gentle; a range all above 0 makes a plateau, with a steep rim where it fades.
        /// </summary>
        public float Lowest { get; set; } = -150f;

        /// <summary>How far above the ground as it was the ground goes at most. Most areas reach close to Lowest and Highest, and a few corners sit flat on them.</summary>
        public float Highest { get; set; } = 150f;

        /// <summary>How far across a hill is, in the game's units.</summary>
        public float FeatureSize { get; set; } = 1536f;

        /// <summary>The layers of noise: each adds bumps half the size of the one before.</summary>
        public int Layers { get; set; } = 4;

        /// <summary>How strong each layer is beside the one before, from 0 (smooth) to 1 (rough).</summary>
        public float Roughness { get; set; } = 0.5f;

        /// <summary>The tiles its paint rules paint, each once, in order: the map declares them in its <c>Tiles</c> (<c>Tiles.AddRange(new SnowyForest().Tiles)</c>).</summary>
        public IReadOnlyList<int> Tiles
        {
            get
            {
                var tiles = new List<int>();
                foreach (var rule in PaintRules)
                {
                    if (!tiles.Contains(rule.Tile))
                    {
                        tiles.Add(rule.Tile);
                    }
                }

                return tiles;
            }
        }

        /// <summary>
        /// Paints a tile (one of <c>Tile</c>'s) on every corner whose height and slope are in the ranges. A height is a
        /// share of the range, 0 at <c>Lowest</c> and 1 at <c>Highest</c>; a slope is in degrees. A
        /// <paramref name="cover"/> below 1 paints patches about 512 across: 0.5 covers about half of the corners, and
        /// less covers fewer. Later rules paint over earlier ones. A tile of 0 throws ArgumentException.
        /// </summary>
        public PaintRule Paint(int tile, float fromHeight = 0f, float toHeight = 1f, float fromSlope = 0f, float toSlope = 90f, float cover = 1f)
        {
            var rule = new PaintRule(++PaintsMade, tile, fromHeight, toHeight, fromSlope, toSlope, cover);
            PaintRules.Add(rule);
            return rule;
        }

        /// <summary>Takes kinds of pathing from every corner whose height and slope are in the ranges, read as <c>Paint</c>'s. Rules add up.</summary>
        public BlockRule Block(Pathing kinds, float fromHeight = 0f, float toHeight = 1f, float fromSlope = 0f, float toSlope = 90f)
        {
            var rule = new BlockRule(kinds, fromHeight, toHeight, fromSlope, toSlope);
            BlockRules.Add(rule);
            return rule;
        }

        /// <summary>
        /// Stands patches of forest about 1024 across on the corners whose height, slope and tile fit, read as <c>Paint</c>'s,
        /// with <paramref name="onTile"/> a tile this biome paints there (0 for any corner). <paramref name="cover"/> is
        /// as <c>Paint</c>'s: 0.5 covers about half of the corners, and less covers fewer. <paramref name="density"/> is the
        /// share of a patch's corners that get a tree, 1 for a thick forest. A tree is a destructable of
        /// <paramref name="type"/> (<c>ObjectTypes.FourCC("LTlt")</c>) in one of its first <paramref name="variations"/>
        /// models (the game's tree walls have 10). Trees keep off level places, a corner has one tree at most, and a later
        /// tree rule takes a corner from an earlier one. A type of 0, or fewer than 1 variation, throws ArgumentException.
        /// </summary>
        public ForestRule Forest(int type, float cover = 0.5f, float density = 1f, float fromHeight = 0f, float toHeight = 1f, float fromSlope = 0f, float toSlope = 90f, int onTile = 0, int variations = 10)
        {
            var rule = new ForestRule(++TreesMade, type, cover, density, fromHeight, toHeight, fromSlope, toSlope, onTile, variations);
            TreeRules.Add(rule);
            return rule;
        }

        /// <summary>
        /// Stands forest walls along winding lines through the world, about <paramref name="spacing"/> apart (in the game's
        /// units) and <paramref name="width"/> trees thick, with a share <paramref name="gaps"/> of their length left open
        /// in gaps about 512 across, so they edge open ground and channel movement. The lines come from where the corners
        /// are in the world, so they go on across plans of one generator. Read as <c>Forest</c>'s otherwise. A spacing or
        /// width of 0 or less throws ArgumentException, as <c>Forest</c> does.
        /// </summary>
        public WallRule Wall(int type, float spacing = 2048f, float width = 2f, float gaps = 0.15f, float fromHeight = 0f, float toHeight = 1f, float fromSlope = 0f, float toSlope = 90f, int onTile = 0, int variations = 10)
        {
            var rule = new WallRule(++TreesMade, type, spacing, width, gaps, fromHeight, toHeight, fromSlope, toSlope, onTile, variations);
            TreeRules.Add(rule);
            return rule;
        }

        /// <summary>Stands single trees on a share <paramref name="chance"/> of the corners that fit, read as <c>Forest</c>'s.</summary>
        public ScatterRule Scatter(int type, float chance = 0.02f, float fromHeight = 0f, float toHeight = 1f, float fromSlope = 0f, float toSlope = 90f, int onTile = 0, int variations = 10)
        {
            var rule = new ScatterRule(++TreesMade, type, chance, fromHeight, toHeight, fromSlope, toSlope, onTile, variations);
            TreeRules.Add(rule);
            return rule;
        }

        /// <summary>Drops a rule, so the next plan goes without it; one this biome does not hold is ignored.</summary>
        public void Remove(TerrainRule rule)
        {
            if (rule is PaintRule paint)
            {
                PaintRules.Remove(paint);
            }
            else if (rule is BlockRule block)
            {
                BlockRules.Remove(block);
            }
            else if (rule is TreeRule tree)
            {
                TreeRules.Remove(tree);
            }
        }
    }
}
