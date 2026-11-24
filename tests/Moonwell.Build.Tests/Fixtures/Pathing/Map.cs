using Moonwell;

namespace PathingFixture
{
    /// <summary>A map that asks a path and a fit when the game starts, and a path by unit type, which makes the build write the unit types' table.</summary>
    public sealed class PathingMap : Map
    {
        public PathingMap()
        {
            Name = "Pathing";
            Components.Add(new Walker());
        }
    }

    public sealed class Walker : GameComponent
    {
        public override void OnGameStart()
        {
            var route = Paths.FindPath(31f, 0f, 0f, 256f, 0f);
            var footman = Paths.FindPathFor(ObjectTypes.FourCC("hfoo"), 0f, 0f, 256f, 0f);
            if (route.Reaches && footman.Reaches && Paths.CanStand(31f, 256f, 0f))
            {
                Player.One.Print("Reaches " + route.Length);
            }
        }
    }
}
