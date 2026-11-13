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

    /// <summary>Hills over the playable area: grass with dirt on the upper half, steep ground blocked, a level place in the middle, and a road between two bases that a footman walks.</summary>
    public sealed class Ground : GameComponent
    {
        public override void OnGameStart()
        {
            var hills = new TerrainGenerator(2026) { Highest = 400f };
            hills.Paint(Tile.LordaeronSummerGrass);
            hills.Paint(Tile.LordaeronSummerDirt, fromHeight: 0.5f);
            hills.Block(Pathing.Walk | Pathing.Build, fromSlope: 30f);
            hills.Level(0f, 0f, 384f);
            var west = hills.Level(-2000f, 0f, 384f);
            var east = hills.Level(2000f, 0f, 384f);
            hills.RoadTile = Tile.LordaeronSummerDirt;
            var lane = hills.Road(west, east, through: new Vector2(0f, 1500f));
            hills.Join();
            hills.Plan(Terrain.Playable).Apply(Game.Instance);
            Unit.Create(ObjectTypes.FourCC("hfoo"), Player.Get(0), west.X, west.Y, Game.Instance).Add(new FollowRoute(lane.Toward(east)));
        }
    }

    /// <summary>The guide's unit sent along a route a point at a time.</summary>
    public sealed class FollowRoute : UnitComponent
    {
        private readonly Route route;
        private int next = 1;

        public FollowRoute(Route route) { this.route = route; }

        protected override void OnAdded()
        {
            Unit.OrderAt(Orders.Attack, route.Points[next].X, route.Points[next].Y);
            Every(0.25f, () =>
            {
                if (next == route.Points.Count - 1 || new Vector2(Unit.X, Unit.Y).DistanceTo(route.Points[next]) > 192f) return;
                next++;
                Unit.OrderAt(Orders.Attack, route.Points[next].X, route.Points[next].Y);
            });
        }
    }
}
