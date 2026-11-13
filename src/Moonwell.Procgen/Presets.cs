namespace Moonwell.Procgen
{
    /// <summary>
    /// A preset biome: gentle green hills of Lordaeron Summer grass, dark grass in patches, rock where it is steep, a few
    /// groves and single trees (Summer Tree Wall). Its rules are fields to change, drop with <c>Remove</c> or add to.
    /// </summary>
    public sealed class Grassland : Biome
    {
        public Grassland()
        {
            Lowest = -100f;
            Highest = 120f;
            FeatureSize = 2048f;
            Roughness = 0.45f;
            Grass = Paint(Tile.LordaeronSummerGrass);
            DarkGrass = Paint(Tile.LordaeronSummerDarkGrass, cover: 0.4f);
            Rock = Paint(Tile.LordaeronSummerRock, fromSlope: 20f);
            Groves = Forest(ObjectTypes.FourCC("LTlt"), cover: 0.15f, density: 0.7f, toSlope: 20f);
            Trees = Scatter(ObjectTypes.FourCC("LTlt"), chance: 0.01f, toSlope: 20f);
        }

        public PaintRule Grass { get; }

        public PaintRule DarkGrass { get; }

        public PaintRule Rock { get; }

        public ForestRule Groves { get; }

        public ScatterRule Trees { get; }
    }

    /// <summary>
    /// A preset biome: high, rough Lordaeron Summer hills of rough dirt, grass in the low ground, rock on the slopes, the
    /// steepest blocked to walking and building, and a few single trees (Summer Tree Wall). Its rules are fields to change,
    /// drop with <c>Remove</c> or add to.
    /// </summary>
    public sealed class RockyHills : Biome
    {
        public RockyHills()
        {
            Lowest = -50f;
            Highest = 450f;
            FeatureSize = 1024f;
            Roughness = 0.6f;
            Dirt = Paint(Tile.LordaeronSummerRoughDirt);
            Grass = Paint(Tile.LordaeronSummerGrass, toHeight: 0.35f, cover: 0.6f);
            Rock = Paint(Tile.LordaeronSummerRock, fromSlope: 18f);
            Cliffs = Block(Pathing.Walk | Pathing.Build, fromSlope: 32f);
            Trees = Scatter(ObjectTypes.FourCC("LTlt"), chance: 0.015f, toSlope: 20f);
        }

        public PaintRule Dirt { get; }

        public PaintRule Grass { get; }

        public PaintRule Rock { get; }

        public BlockRule Cliffs { get; }

        public ScatterRule Trees { get; }
    }

    /// <summary>
    /// A preset biome: Lordaeron Winter snow on rolling hills, grassy snow in patches, rock where it is steep, thick
    /// forests and single trees between them (Snowy Tree Wall). Its rules are fields to change, drop with <c>Remove</c> or
    /// add to.
    /// </summary>
    public sealed class SnowyForest : Biome
    {
        public SnowyForest()
        {
            Lowest = -100f;
            Highest = 250f;
            Snow = Paint(Tile.LordaeronWinterSnow);
            GrassySnow = Paint(Tile.LordaeronWinterGrassySnow, cover: 0.35f);
            Rock = Paint(Tile.LordaeronWinterRock, fromSlope: 22f);
            Trees = Forest(ObjectTypes.FourCC("WTst"), cover: 0.5f, density: 0.8f, toSlope: 25f);
            Single = Scatter(ObjectTypes.FourCC("WTst"), chance: 0.01f, toSlope: 25f);
        }

        public PaintRule Snow { get; }

        public PaintRule GrassySnow { get; }

        public PaintRule Rock { get; }

        public ForestRule Trees { get; }

        public ScatterRule Single { get; }
    }

    /// <summary>
    /// A preset biome: low, smooth Barrens dunes of desert sand, dark sand in patches, pebbles on the slopes, rock where it
    /// is steep, and a rare tree (Barrens Tree Wall). Its rules are fields to change, drop with <c>Remove</c> or add to.
    /// </summary>
    public sealed class Desert : Biome
    {
        public Desert()
        {
            Lowest = -80f;
            Highest = 120f;
            FeatureSize = 2560f;
            Roughness = 0.35f;
            Sand = Paint(Tile.BarrensDesert);
            DarkSand = Paint(Tile.BarrensDarkDesert, cover: 0.35f);
            Pebbles = Paint(Tile.BarrensPebbles, fromSlope: 12f, cover: 0.5f);
            Rock = Paint(Tile.BarrensRock, fromSlope: 22f);
            Trees = Scatter(ObjectTypes.FourCC("BTtw"), chance: 0.004f, toSlope: 15f);
        }

        public PaintRule Sand { get; }

        public PaintRule DarkSand { get; }

        public PaintRule Pebbles { get; }

        public PaintRule Rock { get; }

        public ScatterRule Trees { get; }
    }
}
