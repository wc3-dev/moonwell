using Moonwell;
using Moonwell.Procgen;

namespace ProcgenFixture
{
    /// <summary>A map that makes its ground from a seed at the start. Its project references only the procgen package, which brings the terrain package with it.</summary>
    public sealed class ProcgenMap : Map
    {
        public ProcgenMap()
        {
            Name = "Procgen";
            Components.Add(new Ground());
        }
    }

    /// <summary>Hills over the playable area: grass with dirt on the upper half, steep ground blocked, and a level place in the middle.</summary>
    public sealed class Ground : GameComponent
    {
        public override void OnGameStart()
        {
            var hills = new TerrainGenerator(2026) { Highest = 400f };
            hills.Paint(Tile.LordaeronSummerGrass);
            hills.Paint(Tile.LordaeronSummerDirt, fromHeight: 0.5f);
            hills.Block(Pathing.Walk | Pathing.Build, fromSlope: 30f);
            hills.Level(0f, 0f, 384f);
            hills.Plan(Terrain.Playable).Apply(Game.Instance);
        }
    }
}
